/*
TR: FinWallet Wallet ve BankAccount durable state'i için MSSQL şemasıdır. Bakiye source-of-truth ileride Ledger ile reconcile edilir; Redis bu tabloların yerine geçmez.
EN: MSSQL schema for durable FinWallet Wallet and BankAccount state. Balance state is reconciled with the Ledger in later phases; Redis never replaces these tables.
*/

SET XACT_ABORT ON;
GO

CREATE TABLE dbo.Wallets
(
    Id UNIQUEIDENTIFIER NOT NULL,
    CustomerId UNIQUEIDENTIFIER NOT NULL,
    Currency TINYINT NOT NULL,
    AvailableBalance DECIMAL(19,4) NOT NULL CONSTRAINT DF_Wallets_AvailableBalance DEFAULT (0),
    BlockedBalance DECIMAL(19,4) NOT NULL CONSTRAINT DF_Wallets_BlockedBalance DEFAULT (0),
    Status TINYINT NOT NULL,
    CreatedAt DATETIMEOFFSET(7) NOT NULL,
    RowVersion ROWVERSION NOT NULL,

    CONSTRAINT PK_Wallets PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_Wallets_Customers FOREIGN KEY (CustomerId) REFERENCES dbo.Customers(Id),
    CONSTRAINT UQ_Wallets_CustomerId_Currency UNIQUE (CustomerId, Currency),
    CONSTRAINT UQ_Wallets_Id_CustomerId_Currency UNIQUE (Id, CustomerId, Currency),
    CONSTRAINT CK_Wallets_Currency CHECK (Currency IN (1, 2, 3)),
    CONSTRAINT CK_Wallets_Status CHECK (Status IN (1, 2, 3)),
    CONSTRAINT CK_Wallets_AvailableBalance CHECK (AvailableBalance >= 0),
    CONSTRAINT CK_Wallets_BlockedBalance CHECK (BlockedBalance >= 0)
);
GO

CREATE INDEX IX_Wallets_CustomerId_Status
    ON dbo.Wallets(CustomerId, Status)
    INCLUDE (Currency, AvailableBalance, BlockedBalance);
GO

CREATE TABLE dbo.BankAccounts
(
    Id UNIQUEIDENTIFIER NOT NULL,
    CustomerId UNIQUEIDENTIFIER NOT NULL,
    WalletId UNIQUEIDENTIFIER NOT NULL,
    Currency TINYINT NOT NULL,
    ExternalAccountId UNIQUEIDENTIFIER NULL,
    ExternalIban NVARCHAR(64) NULL,
    Status TINYINT NOT NULL,
    CreatedAt DATETIMEOFFSET(7) NOT NULL,
    UpdatedAt DATETIMEOFFSET(7) NOT NULL,
    RowVersion ROWVERSION NOT NULL,

    CONSTRAINT PK_BankAccounts PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_BankAccounts_Customers FOREIGN KEY (CustomerId) REFERENCES dbo.Customers(Id),
    CONSTRAINT FK_BankAccounts_WalletOwnership FOREIGN KEY (WalletId, CustomerId, Currency)
        REFERENCES dbo.Wallets(Id, CustomerId, Currency),
    CONSTRAINT UQ_BankAccounts_WalletId UNIQUE (WalletId),
    CONSTRAINT CK_BankAccounts_Currency CHECK (Currency IN (1, 2, 3)),
    CONSTRAINT CK_BankAccounts_Status CHECK (Status IN (1, 2, 3, 4, 5)),
    CONSTRAINT CK_BankAccounts_UpdatedAt CHECK (UpdatedAt >= CreatedAt),
    CONSTRAINT CK_BankAccounts_ExternalPair CHECK
    (
        (ExternalAccountId IS NULL AND ExternalIban IS NULL)
        OR
        (ExternalAccountId IS NOT NULL AND ExternalIban IS NOT NULL)
    ),
    CONSTRAINT CK_BankAccounts_ExternalRequiredForFinalStates CHECK
    (
        Status NOT IN (2, 4, 5)
        OR ExternalAccountId IS NOT NULL
    )
);
GO

CREATE UNIQUE INDEX UX_BankAccounts_ExternalAccountId
    ON dbo.BankAccounts(ExternalAccountId)
    WHERE ExternalAccountId IS NOT NULL;
GO

CREATE UNIQUE INDEX UX_BankAccounts_ExternalIban
    ON dbo.BankAccounts(ExternalIban)
    WHERE ExternalIban IS NOT NULL;
GO

CREATE INDEX IX_BankAccounts_CustomerId_Status
    ON dbo.BankAccounts(CustomerId, Status)
    INCLUDE (WalletId, Currency, ExternalAccountId, UpdatedAt);
GO

/*
TR: BankAccount -> Wallet composite FK, internal account'ın başka müşterinin veya farklı currency'deki wallet'ına bağlanmasını DB seviyesinde engeller.
EN: The BankAccount -> Wallet composite FK prevents an internal bank account from linking to another customer's wallet or to a wallet with a different currency at database level.
*/
