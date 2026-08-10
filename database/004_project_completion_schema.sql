/*
TR: FinWallet v1 tamamlama şemasıdır. Banka operasyon detayları, merchant/purchase metadata, fraud review, outbox/inbox ve reconciliation state'ini durable MSSQL kaynağına ekler.
EN: FinWallet v1 completion schema. Adds durable MSSQL state for bank-operation details, merchant/purchase metadata, fraud review, outbox/inbox and reconciliation.
*/

SET XACT_ABORT ON;
GO

ALTER TABLE dbo.FinancialTransactions DROP CONSTRAINT CK_FinancialTransactions_Type;
GO
ALTER TABLE dbo.FinancialTransactions
ADD CONSTRAINT CK_FinancialTransactions_Type CHECK (Type IN (1, 2, 3, 4, 5, 6));
GO

CREATE TABLE dbo.Merchants
(
    Id NVARCHAR(64) NOT NULL,
    Name NVARCHAR(128) NOT NULL,
    Category NVARCHAR(64) NOT NULL,
    Status TINYINT NOT NULL,
    CreatedAt DATETIMEOFFSET(7) NOT NULL,

    CONSTRAINT PK_Merchants PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT CK_Merchants_Status CHECK (Status IN (1, 2))
);
GO

INSERT INTO dbo.Merchants (Id, Name, Category, Status, CreatedAt)
VALUES
    (N'MRC-COFFEE-001', N'FinWallet Coffee', N'Coffee', 1, SYSUTCDATETIME()),
    (N'MRC-ELECTRONICS-001', N'FinWallet Electronics', N'Electronics', 1, SYSUTCDATETIME()),
    (N'MRC-TRAVEL-001', N'FinWallet Travel', N'Travel', 1, SYSUTCDATETIME());
GO

CREATE TABLE dbo.FinancialTransactionDetails
(
    FinancialTransactionId UNIQUEIDENTIFIER NOT NULL,
    ParentTransactionId UNIQUEIDENTIFIER NULL,
    BankAccountId UNIQUEIDENTIFIER NULL,
    ExternalTransactionId UNIQUEIDENTIFIER NULL,
    MerchantId NVARCHAR(64) NULL,
    CampaignReference UNIQUEIDENTIFIER NULL,
    CampaignId NVARCHAR(64) NULL,
    CampaignSponsorType TINYINT NULL,
    OriginalAmount DECIMAL(19,4) NULL,
    DiscountAmount DECIMAL(19,4) NULL,
    CutoffReference UNIQUEIDENTIFIER NULL,
    ProcessingDate DATE NULL,
    SettlementDate DATE NULL,
    CorrelationId NVARCHAR(64) NULL,
    ExternalReference NVARCHAR(128) NULL,
    ProviderState TINYINT NULL,
    NextAttemptAt DATETIMEOFFSET(7) NULL,
    CreatedAt DATETIMEOFFSET(7) NOT NULL,
    RowVersion ROWVERSION NOT NULL,

    CONSTRAINT PK_FinancialTransactionDetails PRIMARY KEY CLUSTERED (FinancialTransactionId),
    CONSTRAINT FK_FinancialTransactionDetails_Transaction FOREIGN KEY (FinancialTransactionId) REFERENCES dbo.FinancialTransactions(Id),
    CONSTRAINT FK_FinancialTransactionDetails_Parent FOREIGN KEY (ParentTransactionId) REFERENCES dbo.FinancialTransactions(Id),
    CONSTRAINT FK_FinancialTransactionDetails_BankAccount FOREIGN KEY (BankAccountId) REFERENCES dbo.BankAccounts(Id),
    CONSTRAINT FK_FinancialTransactionDetails_Merchant FOREIGN KEY (MerchantId) REFERENCES dbo.Merchants(Id),
    CONSTRAINT CK_FinancialTransactionDetails_Sponsor CHECK (CampaignSponsorType IS NULL OR CampaignSponsorType IN (1, 2)),
    CONSTRAINT CK_FinancialTransactionDetails_ProviderState CHECK (ProviderState IS NULL OR ProviderState IN (1, 2, 3)),
    CONSTRAINT CK_FinancialTransactionDetails_Amounts CHECK
    (
        (OriginalAmount IS NULL OR OriginalAmount > 0)
        AND (DiscountAmount IS NULL OR DiscountAmount >= 0)
        AND (OriginalAmount IS NULL OR DiscountAmount IS NULL OR DiscountAmount <= OriginalAmount)
    ),
    CONSTRAINT CK_FinancialTransactionDetails_Dates CHECK
    (
        ProcessingDate IS NULL OR SettlementDate IS NULL OR SettlementDate >= ProcessingDate
    ),
    CONSTRAINT CK_FinancialTransactionDetails_Parent CHECK
    (
        ParentTransactionId IS NULL OR ParentTransactionId <> FinancialTransactionId
    )
);
GO

CREATE UNIQUE INDEX UX_FinancialTransactionDetails_ExternalTransactionId
    ON dbo.FinancialTransactionDetails(ExternalTransactionId)
    WHERE ExternalTransactionId IS NOT NULL;
GO

CREATE INDEX IX_FinancialTransactionDetails_Due
    ON dbo.FinancialTransactionDetails(NextAttemptAt, ProviderState)
    INCLUDE (FinancialTransactionId, BankAccountId, ExternalTransactionId, ProcessingDate, SettlementDate);
GO

CREATE INDEX IX_FinancialTransactionDetails_ParentTransactionId
    ON dbo.FinancialTransactionDetails(ParentTransactionId)
    WHERE ParentTransactionId IS NOT NULL;
GO

CREATE TABLE dbo.FraudEvents
(
    Id UNIQUEIDENTIFIER NOT NULL,
    CustomerId UNIQUEIDENTIFIER NOT NULL,
    Operation NVARCHAR(64) NOT NULL,
    IdempotencyKey NVARCHAR(128) NOT NULL,
    RequestHash CHAR(64) NOT NULL,
    TransactionId UNIQUEIDENTIFIER NULL,
    InternalDecision TINYINT NOT NULL,
    ExternalDecision TINYINT NULL,
    FinalDecision TINYINT NOT NULL,
    ReasonCodes NVARCHAR(1024) NULL,
    ReviewStatus TINYINT NOT NULL,
    CreatedAt DATETIMEOFFSET(7) NOT NULL,
    ReviewedAt DATETIMEOFFSET(7) NULL,
    ReviewedBy NVARCHAR(128) NULL,
    RowVersion ROWVERSION NOT NULL,

    CONSTRAINT PK_FraudEvents PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_FraudEvents_Customers FOREIGN KEY (CustomerId) REFERENCES dbo.Customers(Id),
    CONSTRAINT FK_FraudEvents_Transactions FOREIGN KEY (TransactionId) REFERENCES dbo.FinancialTransactions(Id),
    CONSTRAINT UQ_FraudEvents_Operation_Customer_Idempotency UNIQUE (Operation, CustomerId, IdempotencyKey),
    CONSTRAINT CK_FraudEvents_RequestHash CHECK (LEN(RequestHash) = 64),
    CONSTRAINT CK_FraudEvents_InternalDecision CHECK (InternalDecision IN (1, 2, 3)),
    CONSTRAINT CK_FraudEvents_ExternalDecision CHECK (ExternalDecision IS NULL OR ExternalDecision IN (1, 2, 3)),
    CONSTRAINT CK_FraudEvents_FinalDecision CHECK (FinalDecision IN (1, 2, 3)),
    CONSTRAINT CK_FraudEvents_ReviewStatus CHECK (ReviewStatus IN (1, 2, 3, 4)),
    CONSTRAINT CK_FraudEvents_ReviewAudit CHECK
    (
        (ReviewStatus = 1 AND ReviewedAt IS NULL AND ReviewedBy IS NULL)
        OR
        (ReviewStatus IN (2, 3) AND ReviewedAt IS NOT NULL AND ReviewedBy IS NOT NULL)
        OR
        (ReviewStatus = 4)
    )
);
GO

CREATE INDEX IX_FraudEvents_ReviewStatus_CreatedAt
    ON dbo.FraudEvents(ReviewStatus, CreatedAt)
    INCLUDE (CustomerId, Operation, FinalDecision, TransactionId);
GO

CREATE TABLE dbo.OutboxMessages
(
    Id UNIQUEIDENTIFIER NOT NULL,
    MessageType NVARCHAR(128) NOT NULL,
    AggregateId UNIQUEIDENTIFIER NULL,
    PayloadJson NVARCHAR(MAX) NOT NULL,
    CorrelationId NVARCHAR(64) NULL,
    CreatedAt DATETIMEOFFSET(7) NOT NULL,
    AvailableAt DATETIMEOFFSET(7) NOT NULL,
    ProcessedAt DATETIMEOFFSET(7) NULL,
    AttemptCount INT NOT NULL CONSTRAINT DF_OutboxMessages_AttemptCount DEFAULT (0),
    LastErrorCode NVARCHAR(128) NULL,
    RowVersion ROWVERSION NOT NULL,

    CONSTRAINT PK_OutboxMessages PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT CK_OutboxMessages_AttemptCount CHECK (AttemptCount >= 0),
    CONSTRAINT CK_OutboxMessages_AvailableAt CHECK (AvailableAt >= CreatedAt)
);
GO

CREATE INDEX IX_OutboxMessages_Pending
    ON dbo.OutboxMessages(ProcessedAt, AvailableAt, CreatedAt)
    INCLUDE (MessageType, AggregateId, AttemptCount)
    WHERE ProcessedAt IS NULL;
GO

CREATE TABLE dbo.InboxMessages
(
    Id UNIQUEIDENTIFIER NOT NULL,
    Source NVARCHAR(64) NOT NULL,
    MessageId NVARCHAR(128) NOT NULL,
    PayloadHash CHAR(64) NOT NULL,
    ReceivedAt DATETIMEOFFSET(7) NOT NULL,
    ProcessedAt DATETIMEOFFSET(7) NULL,
    RowVersion ROWVERSION NOT NULL,

    CONSTRAINT PK_InboxMessages PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UQ_InboxMessages_Source_MessageId UNIQUE (Source, MessageId),
    CONSTRAINT CK_InboxMessages_PayloadHash CHECK (LEN(PayloadHash) = 64)
);
GO

CREATE INDEX IX_InboxMessages_Unprocessed
    ON dbo.InboxMessages(ReceivedAt)
    INCLUDE (Source, MessageId)
    WHERE ProcessedAt IS NULL;
GO

CREATE TABLE dbo.ReconciliationRuns
(
    Id UNIQUEIDENTIFIER NOT NULL,
    Scope TINYINT NOT NULL,
    Status TINYINT NOT NULL,
    StartedAt DATETIMEOFFSET(7) NOT NULL,
    CompletedAt DATETIMEOFFSET(7) NULL,
    IssueCount INT NOT NULL CONSTRAINT DF_ReconciliationRuns_IssueCount DEFAULT (0),
    RowVersion ROWVERSION NOT NULL,

    CONSTRAINT PK_ReconciliationRuns PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT CK_ReconciliationRuns_Scope CHECK (Scope IN (1, 2, 3)),
    CONSTRAINT CK_ReconciliationRuns_Status CHECK (Status IN (1, 2, 3)),
    CONSTRAINT CK_ReconciliationRuns_IssueCount CHECK (IssueCount >= 0),
    CONSTRAINT CK_ReconciliationRuns_Times CHECK (CompletedAt IS NULL OR CompletedAt >= StartedAt)
);
GO

CREATE TABLE dbo.ReconciliationIssues
(
    Id UNIQUEIDENTIFIER NOT NULL,
    RunId UNIQUEIDENTIFIER NOT NULL,
    IssueType TINYINT NOT NULL,
    TransactionId UNIQUEIDENTIFIER NULL,
    WalletId UNIQUEIDENTIFIER NULL,
    BankAccountId UNIQUEIDENTIFIER NULL,
    ExternalTransactionId UNIQUEIDENTIFIER NULL,
    Currency TINYINT NULL,
    ExpectedAmount DECIMAL(19,4) NULL,
    ActualAmount DECIMAL(19,4) NULL,
    Details NVARCHAR(1024) NOT NULL,
    CreatedAt DATETIMEOFFSET(7) NOT NULL,
    ResolvedAt DATETIMEOFFSET(7) NULL,
    RowVersion ROWVERSION NOT NULL,

    CONSTRAINT PK_ReconciliationIssues PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_ReconciliationIssues_Run FOREIGN KEY (RunId) REFERENCES dbo.ReconciliationRuns(Id),
    CONSTRAINT FK_ReconciliationIssues_Transaction FOREIGN KEY (TransactionId) REFERENCES dbo.FinancialTransactions(Id),
    CONSTRAINT FK_ReconciliationIssues_Wallet FOREIGN KEY (WalletId) REFERENCES dbo.Wallets(Id),
    CONSTRAINT FK_ReconciliationIssues_BankAccount FOREIGN KEY (BankAccountId) REFERENCES dbo.BankAccounts(Id),
    CONSTRAINT CK_ReconciliationIssues_Type CHECK (IssueType IN (1, 2, 3, 4, 5, 6, 7)),
    CONSTRAINT CK_ReconciliationIssues_Currency CHECK (Currency IS NULL OR Currency IN (1, 2, 3))
);
GO

CREATE INDEX IX_ReconciliationIssues_RunId
    ON dbo.ReconciliationIssues(RunId, CreatedAt)
    INCLUDE (IssueType, TransactionId, WalletId, BankAccountId, ExternalTransactionId, ResolvedAt);
GO

/*
TR: Reconciliation kayıtları yalnız mismatch raporlar. Bu şema veya reconciliation use-case'i Wallet/Ledger bakiyesini otomatik düzeltmez.
EN: Reconciliation records report mismatches only. Neither this schema nor the reconciliation use case automatically repairs Wallet/Ledger balances.
*/
