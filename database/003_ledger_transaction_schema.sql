/*
TR: FinWallet double-entry ledger, finansal transaction ve durable idempotency temel şemasıdır. Ledger journal/entry kayıtları para hareketiyle aynı MSSQL transaction içinde yazılmalıdır.
EN: Foundation schema for FinWallet double-entry ledger, financial transactions and durable idempotency. Ledger journals/entries must be written inside the same MSSQL transaction as the money movement.
*/

SET XACT_ABORT ON;
GO

/* Destination wallet currency doğrulaması için composite FK hedefi. */
ALTER TABLE dbo.Wallets
ADD CONSTRAINT UQ_Wallets_Id_Currency UNIQUE (Id, Currency);
GO

CREATE TABLE dbo.LedgerAccounts
(
    Id UNIQUEIDENTIFIER NOT NULL,
    Code NVARCHAR(128) NOT NULL,
    Currency TINYINT NOT NULL,
    Type TINYINT NOT NULL,
    Status TINYINT NOT NULL,
    CreatedAt DATETIMEOFFSET(7) NOT NULL,
    RowVersion ROWVERSION NOT NULL,

    CONSTRAINT PK_LedgerAccounts PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UQ_LedgerAccounts_Code UNIQUE (Code),
    CONSTRAINT UQ_LedgerAccounts_Id_Currency UNIQUE (Id, Currency),
    CONSTRAINT CK_LedgerAccounts_Currency CHECK (Currency IN (1, 2, 3)),
    CONSTRAINT CK_LedgerAccounts_Type CHECK (Type IN (1, 2, 3, 4, 5)),
    CONSTRAINT CK_LedgerAccounts_Status CHECK (Status IN (1, 2))
);
GO

CREATE INDEX IX_LedgerAccounts_Currency_Type_Status
    ON dbo.LedgerAccounts(Currency, Type, Status)
    INCLUDE (Code);
GO

CREATE TABLE dbo.FinancialTransactions
(
    Id UNIQUEIDENTIFIER NOT NULL,
    CustomerId UNIQUEIDENTIFIER NOT NULL,
    Type TINYINT NOT NULL,
    Status TINYINT NOT NULL,
    SourceWalletId UNIQUEIDENTIFIER NULL,
    DestinationWalletId UNIQUEIDENTIFIER NULL,
    Currency TINYINT NOT NULL,
    Amount DECIMAL(19,4) NOT NULL,
    CreatedAt DATETIMEOFFSET(7) NOT NULL,
    FinalizedAt DATETIMEOFFSET(7) NULL,
    ReversedAt DATETIMEOFFSET(7) NULL,
    FailureCode NVARCHAR(64) NULL,
    RowVersion ROWVERSION NOT NULL,

    CONSTRAINT PK_FinancialTransactions PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_FinancialTransactions_Customers FOREIGN KEY (CustomerId) REFERENCES dbo.Customers(Id),
    CONSTRAINT FK_FinancialTransactions_SourceWallet FOREIGN KEY (SourceWalletId, CustomerId, Currency)
        REFERENCES dbo.Wallets(Id, CustomerId, Currency),
    CONSTRAINT FK_FinancialTransactions_DestinationWallet FOREIGN KEY (DestinationWalletId, Currency)
        REFERENCES dbo.Wallets(Id, Currency),
    CONSTRAINT CK_FinancialTransactions_Type CHECK (Type IN (1, 2, 3, 4, 5)),
    CONSTRAINT CK_FinancialTransactions_Status CHECK (Status IN (1, 2, 3, 4)),
    CONSTRAINT CK_FinancialTransactions_Currency CHECK (Currency IN (1, 2, 3)),
    CONSTRAINT CK_FinancialTransactions_Amount CHECK (Amount > 0),
    CONSTRAINT CK_FinancialTransactions_Lifecycle CHECK
    (
        (Status = 1 AND FinalizedAt IS NULL AND ReversedAt IS NULL AND FailureCode IS NULL)
        OR
        (Status = 2 AND FinalizedAt IS NOT NULL AND ReversedAt IS NULL AND FailureCode IS NULL)
        OR
        (Status = 3 AND FinalizedAt IS NOT NULL AND ReversedAt IS NULL AND FailureCode IS NOT NULL)
        OR
        (Status = 4 AND FinalizedAt IS NOT NULL AND ReversedAt IS NOT NULL AND FailureCode IS NULL)
    ),
    CONSTRAINT CK_FinancialTransactions_Times CHECK
    (
        (FinalizedAt IS NULL OR FinalizedAt >= CreatedAt)
        AND
        (ReversedAt IS NULL OR (FinalizedAt IS NOT NULL AND ReversedAt >= FinalizedAt))
    ),
    CONSTRAINT CK_FinancialTransactions_WalletTransfer CHECK
    (
        Type <> 1
        OR
        (
            SourceWalletId IS NOT NULL
            AND DestinationWalletId IS NOT NULL
            AND SourceWalletId <> DestinationWalletId
        )
    )
);
GO

CREATE INDEX IX_FinancialTransactions_Customer_CreatedAt
    ON dbo.FinancialTransactions(CustomerId, CreatedAt DESC)
    INCLUDE (Type, Status, Currency, Amount, SourceWalletId, DestinationWalletId, FinalizedAt);
GO

CREATE TABLE dbo.IdempotencyRecords
(
    Id UNIQUEIDENTIFIER NOT NULL,
    Scope NVARCHAR(64) NOT NULL,
    CustomerId UNIQUEIDENTIFIER NOT NULL,
    IdempotencyKey NVARCHAR(128) NOT NULL,
    RequestHash CHAR(64) NOT NULL,
    ResourceId UNIQUEIDENTIFIER NULL,
    Status TINYINT NOT NULL,
    ResultCode NVARCHAR(64) NULL,
    CreatedAt DATETIMEOFFSET(7) NOT NULL,
    UpdatedAt DATETIMEOFFSET(7) NOT NULL,
    RowVersion ROWVERSION NOT NULL,

    CONSTRAINT PK_IdempotencyRecords PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_IdempotencyRecords_Customers FOREIGN KEY (CustomerId) REFERENCES dbo.Customers(Id),
    CONSTRAINT UQ_IdempotencyRecords_Scope_Customer_Key UNIQUE (Scope, CustomerId, IdempotencyKey),
    CONSTRAINT CK_IdempotencyRecords_Status CHECK (Status IN (1, 2, 3)),
    CONSTRAINT CK_IdempotencyRecords_RequestHash CHECK (LEN(RequestHash) = 64),
    CONSTRAINT CK_IdempotencyRecords_UpdatedAt CHECK (UpdatedAt >= CreatedAt),
    CONSTRAINT CK_IdempotencyRecords_Completion CHECK
    (
        (Status = 1 AND ResourceId IS NULL AND ResultCode IS NULL)
        OR
        (Status IN (2, 3) AND ResultCode IS NOT NULL)
    )
);
GO

CREATE INDEX IX_IdempotencyRecords_Customer_CreatedAt
    ON dbo.IdempotencyRecords(CustomerId, CreatedAt DESC)
    INCLUDE (Scope, IdempotencyKey, ResourceId, Status, ResultCode);
GO

CREATE TABLE dbo.LedgerJournals
(
    Id UNIQUEIDENTIFIER NOT NULL,
    TransactionReference UNIQUEIDENTIFIER NOT NULL,
    Currency TINYINT NOT NULL,
    Status TINYINT NOT NULL,
    CreatedAt DATETIMEOFFSET(7) NOT NULL,
    PostedAt DATETIMEOFFSET(7) NULL,
    ReversesJournalId UNIQUEIDENTIFIER NULL,
    RowVersion ROWVERSION NOT NULL,

    CONSTRAINT PK_LedgerJournals PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_LedgerJournals_Transactions FOREIGN KEY (TransactionReference) REFERENCES dbo.FinancialTransactions(Id),
    CONSTRAINT FK_LedgerJournals_Reverses FOREIGN KEY (ReversesJournalId) REFERENCES dbo.LedgerJournals(Id),
    CONSTRAINT UQ_LedgerJournals_TransactionReference UNIQUE (TransactionReference),
    CONSTRAINT UQ_LedgerJournals_Id_Currency UNIQUE (Id, Currency),
    CONSTRAINT CK_LedgerJournals_Currency CHECK (Currency IN (1, 2, 3)),
    CONSTRAINT CK_LedgerJournals_Status CHECK (Status IN (1, 2)),
    CONSTRAINT CK_LedgerJournals_Posting CHECK
    (
        (Status = 1 AND PostedAt IS NULL)
        OR
        (Status = 2 AND PostedAt IS NOT NULL AND PostedAt >= CreatedAt)
    ),
    CONSTRAINT CK_LedgerJournals_Reversal CHECK (ReversesJournalId IS NULL OR ReversesJournalId <> Id)
);
GO

CREATE UNIQUE INDEX UX_LedgerJournals_ReversesJournalId
    ON dbo.LedgerJournals(ReversesJournalId)
    WHERE ReversesJournalId IS NOT NULL;
GO

CREATE TABLE dbo.LedgerEntries
(
    Id UNIQUEIDENTIFIER NOT NULL,
    JournalId UNIQUEIDENTIFIER NOT NULL,
    SequenceNumber SMALLINT NOT NULL,
    AccountId UNIQUEIDENTIFIER NOT NULL,
    Side TINYINT NOT NULL,
    Amount DECIMAL(19,4) NOT NULL,
    Currency TINYINT NOT NULL,

    CONSTRAINT PK_LedgerEntries PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_LedgerEntries_JournalCurrency FOREIGN KEY (JournalId, Currency)
        REFERENCES dbo.LedgerJournals(Id, Currency),
    CONSTRAINT FK_LedgerEntries_AccountCurrency FOREIGN KEY (AccountId, Currency)
        REFERENCES dbo.LedgerAccounts(Id, Currency),
    CONSTRAINT UQ_LedgerEntries_Journal_Sequence UNIQUE (JournalId, SequenceNumber),
    CONSTRAINT CK_LedgerEntries_Sequence CHECK (SequenceNumber > 0),
    CONSTRAINT CK_LedgerEntries_Side CHECK (Side IN (1, 2)),
    CONSTRAINT CK_LedgerEntries_Amount CHECK (Amount > 0),
    CONSTRAINT CK_LedgerEntries_Currency CHECK (Currency IN (1, 2, 3))
);
GO

CREATE INDEX IX_LedgerEntries_Account_Journal
    ON dbo.LedgerEntries(AccountId, JournalId)
    INCLUDE (Side, Amount, Currency, SequenceNumber);
GO

/*
TR: Debit=Credit toplam invariant'ı satırlar arası olduğu için CHECK constraint ile ifade edilemez. Transfer posting store journal+entry insertlerinden sonra aynı SQL transaction içinde toplamları yeniden okuyup eşitlik doğrulanmadan COMMIT yapmayacaktır.
EN: Debit=Credit is a cross-row invariant and cannot be expressed as a CHECK constraint. The transfer posting store will re-read totals inside the same SQL transaction after journal/entry inserts and will not COMMIT unless they are equal.
*/
