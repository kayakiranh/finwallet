/*
TR: FinWallet müşteri ve authentication state'i için başlangıç MSSQL şemasıdır. Finansal source-of-truth SQL Server'dır; Redis bu tabloların yerine geçmez.
EN: Initial MSSQL schema for FinWallet customer and authentication state. SQL Server is the durable source of truth; Redis never replaces these tables.
*/

SET XACT_ABORT ON;
GO

CREATE TABLE dbo.Customers
(
    Id UNIQUEIDENTIFIER NOT NULL,
    CountryCode CHAR(2) NOT NULL,
    PhoneNumber VARCHAR(16) NOT NULL,
    Email NVARCHAR(320) NULL,
    Status TINYINT NOT NULL,
    CreatedAt DATETIMEOFFSET(7) NOT NULL,
    RowVersion ROWVERSION NOT NULL,

    CONSTRAINT PK_Customers PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UQ_Customers_PhoneNumber UNIQUE (PhoneNumber),
    CONSTRAINT CK_Customers_Status CHECK (Status IN (1, 2, 3, 4))
);
GO

CREATE TABLE dbo.CustomerCredentials
(
    CustomerId UNIQUEIDENTIFIER NOT NULL,
    PasswordHash VARCHAR(128) NOT NULL,
    PasswordSalt VARCHAR(64) NOT NULL,
    PasswordHashVersion INT NOT NULL,
    FailedLoginCount INT NOT NULL CONSTRAINT DF_CustomerCredentials_FailedLoginCount DEFAULT (0),
    LockedUntil DATETIMEOFFSET(7) NULL,
    PasswordChangedAt DATETIMEOFFSET(7) NOT NULL,
    RowVersion ROWVERSION NOT NULL,

    CONSTRAINT PK_CustomerCredentials PRIMARY KEY CLUSTERED (CustomerId),
    CONSTRAINT FK_CustomerCredentials_Customers FOREIGN KEY (CustomerId) REFERENCES dbo.Customers(Id),
    CONSTRAINT CK_CustomerCredentials_HashVersion CHECK (PasswordHashVersion > 0),
    CONSTRAINT CK_CustomerCredentials_FailedLoginCount CHECK (FailedLoginCount >= 0)
);
GO

CREATE TABLE dbo.CustomerSessions
(
    Id UNIQUEIDENTIFIER NOT NULL,
    CustomerId UNIQUEIDENTIFIER NOT NULL,
    DeviceId NVARCHAR(128) NOT NULL,
    CreatedAt DATETIMEOFFSET(7) NOT NULL,
    LastActivityAt DATETIMEOFFSET(7) NOT NULL,
    ExpiresAt DATETIMEOFFSET(7) NOT NULL,
    RevokedAt DATETIMEOFFSET(7) NULL,
    RowVersion ROWVERSION NOT NULL,

    CONSTRAINT PK_CustomerSessions PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_CustomerSessions_Customers FOREIGN KEY (CustomerId) REFERENCES dbo.Customers(Id),
    CONSTRAINT CK_CustomerSessions_Expiration CHECK (ExpiresAt > CreatedAt),
    CONSTRAINT CK_CustomerSessions_Activity CHECK (LastActivityAt >= CreatedAt)
);
GO

CREATE INDEX IX_CustomerSessions_CustomerId_ExpiresAt
    ON dbo.CustomerSessions(CustomerId, ExpiresAt)
    INCLUDE (RevokedAt, LastActivityAt);
GO

CREATE TABLE dbo.RefreshTokens
(
    Id UNIQUEIDENTIFIER NOT NULL,
    SessionId UNIQUEIDENTIFIER NOT NULL,
    TokenHash CHAR(64) NOT NULL,
    CreatedAt DATETIMEOFFSET(7) NOT NULL,
    ExpiresAt DATETIMEOFFSET(7) NOT NULL,
    ConsumedAt DATETIMEOFFSET(7) NULL,
    RevokedAt DATETIMEOFFSET(7) NULL,
    ReplacedByTokenId UNIQUEIDENTIFIER NULL,
    RowVersion ROWVERSION NOT NULL,

    CONSTRAINT PK_RefreshTokens PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UQ_RefreshTokens_TokenHash UNIQUE (TokenHash),
    CONSTRAINT FK_RefreshTokens_CustomerSessions FOREIGN KEY (SessionId) REFERENCES dbo.CustomerSessions(Id),
    CONSTRAINT CK_RefreshTokens_Expiration CHECK (ExpiresAt > CreatedAt)
);
GO

ALTER TABLE dbo.RefreshTokens
ADD CONSTRAINT FK_RefreshTokens_ReplacedByToken
    FOREIGN KEY (ReplacedByTokenId) REFERENCES dbo.RefreshTokens(Id);
GO

CREATE INDEX IX_RefreshTokens_SessionId_ExpiresAt
    ON dbo.RefreshTokens(SessionId, ExpiresAt)
    INCLUDE (ConsumedAt, RevokedAt, ReplacedByTokenId);
GO

/*
TR: Refresh rotation sırasında uygulama `ConsumedAt IS NULL AND RevokedAt IS NULL` koşuluyla UPDATE yapmalı ve etkilenen satır sayısını compare-and-set sonucu olarak kullanmalıdır.
EN: During refresh rotation the application must UPDATE with `ConsumedAt IS NULL AND RevokedAt IS NULL` and use the affected-row count as the compare-and-set result.
*/
