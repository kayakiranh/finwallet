/*
TR: Finansal işlem öncesi fraud değerlendirmelerini durable ve sorgulanabilir biçimde saklar. Ham DeviceId, JWT, telefon, IBAN veya provider exception mesajı tutulmaz.
EN: Stores pre-financial-operation fraud evaluations durably and queryably. Raw DeviceId, JWT, phone, IBAN or provider exception messages are never stored.
*/

SET XACT_ABORT ON;
GO

CREATE TABLE dbo.FraudEvents
(
    Id UNIQUEIDENTIFIER NOT NULL,
    CustomerId UNIQUEIDENTIFIER NOT NULL,
    SessionId UNIQUEIDENTIFIER NOT NULL,
    TransactionType TINYINT NOT NULL,
    SourceWalletId UNIQUEIDENTIFIER NULL,
    DestinationWalletId UNIQUEIDENTIFIER NULL,
    Currency TINYINT NOT NULL,
    Amount DECIMAL(19,4) NOT NULL,
    CountryCode VARCHAR(3) NOT NULL,
    DeviceReference CHAR(64) NOT NULL,
    IsNewDevice BIT NOT NULL,
    TransactionCountLastFiveMinutes INT NOT NULL,
    AmountLastTwentyFourHours DECIMAL(19,4) NOT NULL,
    IsKnownBeneficiary BIT NOT NULL,
    InternalDecision TINYINT NOT NULL,
    ExternalEvaluationStatus TINYINT NOT NULL,
    ExternalDecision TINYINT NULL,
    FinalDecision TINYINT NULL,
    ExternalProviderReference UNIQUEIDENTIFIER NULL,
    ExternalRiskScore SMALLINT NULL,
    ExternalReasonCodes NVARCHAR(2000) NULL,
    ExternalFailureCode NVARCHAR(64) NULL,
    CreatedAt DATETIMEOFFSET(7) NOT NULL,

    CONSTRAINT PK_FraudEvents PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_FraudEvents_Customers FOREIGN KEY (CustomerId) REFERENCES dbo.Customers(Id),
    CONSTRAINT FK_FraudEvents_Sessions FOREIGN KEY (SessionId) REFERENCES dbo.CustomerSessions(Id),
    CONSTRAINT FK_FraudEvents_SourceWallet FOREIGN KEY (SourceWalletId, CustomerId, Currency)
        REFERENCES dbo.Wallets(Id, CustomerId, Currency),
    CONSTRAINT FK_FraudEvents_DestinationWallet FOREIGN KEY (DestinationWalletId, Currency)
        REFERENCES dbo.Wallets(Id, Currency),
    CONSTRAINT CK_FraudEvents_TransactionType CHECK (TransactionType IN (1, 2, 3, 4, 5)),
    CONSTRAINT CK_FraudEvents_Currency CHECK (Currency IN (1, 2, 3)),
    CONSTRAINT CK_FraudEvents_Amount CHECK (Amount > 0),
    CONSTRAINT CK_FraudEvents_CountryCode CHECK (LEN(CountryCode) BETWEEN 2 AND 3),
    CONSTRAINT CK_FraudEvents_DeviceReference CHECK (LEN(DeviceReference) = 64),
    CONSTRAINT CK_FraudEvents_Velocity CHECK (TransactionCountLastFiveMinutes >= 0),
    CONSTRAINT CK_FraudEvents_AggregateAmount CHECK (AmountLastTwentyFourHours >= 0),
    CONSTRAINT CK_FraudEvents_InternalDecision CHECK (InternalDecision IN (1, 2, 3)),
    CONSTRAINT CK_FraudEvents_ExternalStatus CHECK (ExternalEvaluationStatus IN (1, 2, 3)),
    CONSTRAINT CK_FraudEvents_ExternalDecision CHECK (ExternalDecision IS NULL OR ExternalDecision IN (1, 2, 3)),
    CONSTRAINT CK_FraudEvents_FinalDecision CHECK (FinalDecision IS NULL OR FinalDecision IN (1, 2, 3)),
    CONSTRAINT CK_FraudEvents_RiskScore CHECK (ExternalRiskScore IS NULL OR ExternalRiskScore BETWEEN 0 AND 100),
    CONSTRAINT CK_FraudEvents_ReasonCodes CHECK (ExternalReasonCodes IS NULL OR ISJSON(ExternalReasonCodes) = 1),
    CONSTRAINT CK_FraudEvents_WalletTransfer CHECK
    (
        TransactionType <> 1
        OR
        (
            SourceWalletId IS NOT NULL
            AND DestinationWalletId IS NOT NULL
            AND SourceWalletId <> DestinationWalletId
        )
    ),
    CONSTRAINT CK_FraudEvents_ExternalState CHECK
    (
        (
            ExternalEvaluationStatus = 1
            AND InternalDecision = 3
            AND ExternalDecision IS NULL
            AND FinalDecision = 3
            AND ExternalProviderReference IS NULL
            AND ExternalRiskScore IS NULL
            AND ExternalReasonCodes IS NULL
            AND ExternalFailureCode IS NULL
        )
        OR
        (
            ExternalEvaluationStatus = 2
            AND ExternalDecision IS NOT NULL
            AND FinalDecision IS NOT NULL
            AND ExternalProviderReference IS NOT NULL
            AND ExternalRiskScore IS NOT NULL
            AND ExternalReasonCodes IS NOT NULL
            AND ExternalFailureCode IS NULL
        )
        OR
        (
            ExternalEvaluationStatus = 3
            AND ExternalDecision IS NULL
            AND FinalDecision IS NULL
            AND ExternalProviderReference IS NULL
            AND ExternalRiskScore IS NULL
            AND ExternalReasonCodes IS NULL
            AND ExternalFailureCode IS NOT NULL
        )
    )
);
GO

CREATE INDEX IX_FraudEvents_Customer_CreatedAt
    ON dbo.FraudEvents(CustomerId, CreatedAt DESC)
    INCLUDE (TransactionType, Currency, Amount, InternalDecision, ExternalEvaluationStatus, FinalDecision);
GO

CREATE INDEX IX_FraudEvents_FinalDecision_CreatedAt
    ON dbo.FraudEvents(FinalDecision, CreatedAt DESC)
    INCLUDE (CustomerId, TransactionType, Currency, Amount)
    WHERE FinalDecision IS NOT NULL;
GO

ALTER TABLE dbo.FinancialTransactions
ADD FraudEventId UNIQUEIDENTIFIER NULL;
GO

ALTER TABLE dbo.FinancialTransactions
ADD CONSTRAINT FK_FinancialTransactions_FraudEvents
    FOREIGN KEY (FraudEventId) REFERENCES dbo.FraudEvents(Id);
GO

CREATE UNIQUE INDEX UX_FinancialTransactions_FraudEventId
    ON dbo.FinancialTransactions(FraudEventId)
    WHERE FraudEventId IS NOT NULL;
GO

/*
TR: FraudEvent para hareketinden önce ayrı durable audit kaydı olarak yazılır. Allow kararında atomic posting store aynı FraudEventId değerini FinancialTransactions satırına bağlar. Review/Deny/Unavailable event'lerinde FinancialTransaction oluşmaz.
EN: FraudEvent is written as a separate durable audit record before money movement. For an Allow decision, the atomic posting store links the same FraudEventId to FinancialTransactions. Review/Deny/Unavailable events do not create a FinancialTransaction.
*/
