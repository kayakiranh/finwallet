# Database Design

## Source-of-truth policy

MSSQL is the durable source of truth for customer, authentication and financial state. Redis is not a replacement for MSSQL and is never sufficient to guarantee money correctness.

Persistence uses explicit parameterized `Microsoft.Data.SqlClient` commands rather than EF Core or a generic repository framework. This keeps transaction boundaries and concurrency-sensitive SQL visible.

## Authentication schema

`database/001_authentication_schema.sql` defines:

- `Customers`;
- `CustomerCredentials`;
- `CustomerSessions`;
- `RefreshTokens`.

Phone uniqueness, login lock state, session lifecycle and single-use refresh-token rotation are enforced with database constraints and conditional updates. Raw passwords and raw refresh tokens are never stored.

## Financial account schema

`database/002_financial_accounts_schema.sql` introduces the durable Wallet and BankAccount foundation.

### Wallets

Key fields:

- `Id` — internal Wallet primary key;
- `CustomerId` — owner FK to Customers;
- `Currency` — TRY/USD/EUR enum value;
- `AvailableBalance` — non-negative `DECIMAL(19,4)`;
- `BlockedBalance` — non-negative `DECIMAL(19,4)`;
- `Status` — Active/Blocked/Closed;
- `CreatedAt`;
- `RowVersion`.

Important constraints:

- `(CustomerId, Currency)` is unique: one wallet per customer/currency;
- balances cannot be negative at database level;
- `(Id, CustomerId, Currency)` is additionally unique so BankAccount can reference wallet ownership and currency as one composite relationship.

`Wallet.Restore(...)` rehydrates durable state without reflection and rejects negative persisted balances or invalid lifecycle state.

### BankAccounts

A FinWallet `BankAccount` is not a Wallet and is not the financial Ledger. It is the durable internal link between an owned Wallet and an external-bank account.

Key fields:

- `Id` — FinWallet internal BankAccount ID;
- `CustomerId` — owner customer;
- `WalletId` — linked internal wallet;
- `Currency` — same currency as linked wallet;
- `ExternalAccountId` — provider-generated account ID, nullable while opening has not reached the provider;
- `ExternalIban` — provider IBAN-like value, nullable together with ExternalAccountId;
- `Status` — Opening/Active/Rejected/Blocked/Closed;
- `CreatedAt`;
- `UpdatedAt`;
- `RowVersion`.

Database invariants:

- `WalletId` is unique: one BankAccount per Wallet;
- composite FK `(WalletId, CustomerId, Currency) -> Wallets(Id, CustomerId, Currency)` prevents linking another customer's wallet or a different-currency wallet;
- ExternalAccountId and ExternalIban must either both be null or both be present;
- Active/Blocked/Closed states require an external account link;
- ExternalAccountId and ExternalIban are individually unique when non-null;
- `UpdatedAt >= CreatedAt`.

### BankAccount concurrency

Creating a BankAccount is race-safe:

- application may first check for an existing wallet link;
- the database `UNIQUE(WalletId)` is the final guarantee;
- `TryInsertAsync` converts duplicate-key races into a false result;
- the use case reloads the winning durable BankAccount rather than creating a second external account.

Lifecycle/provider updates use compare-and-set against both `Status` and `UpdatedAt`:

```sql
UPDATE dbo.BankAccounts
SET ExternalAccountId = @ExternalAccountId,
    ExternalIban = @ExternalIban,
    Status = @Status,
    UpdatedAt = @UpdatedAt
WHERE Id = @Id
  AND CustomerId = @CustomerId
  AND WalletId = @WalletId
  AND Currency = @Currency
  AND Status = @ExpectedStatus
  AND UpdatedAt = @ExpectedUpdatedAt;
```

Using `UpdatedAt` in addition to status matters because provider identity can be attached while the lifecycle remains `Opening`. A stale `Opening` snapshot therefore cannot overwrite a newer `Opening` snapshot.

## Bank account opening transaction boundary

External HTTP never runs inside a FinWallet SQL transaction.

```text
1. Load owned Wallet
2. Find or insert durable BankAccount(Opening)
3. SQL operation completes
4. Call external bank using deterministic provider RequestKey
5. Validate provider account identity/currency
6. Apply provider state in memory
7. CAS update BankAccount using expected Status + UpdatedAt
```

The provider request key is derived from the durable internal BankAccount ID. If the provider creates the account but FinWallet loses the response, the next attempt sends the same key and receives the same provider account rather than creating a duplicate.

## Authentication transaction boundaries

### Registration

Customer + CustomerCredential are inserted in one short transaction. OTP/provider communication occurs only after commit.

### Successful login

Credential reset, CustomerSession and initial RefreshToken hash are persisted atomically.

### Refresh rotation

The old refresh token is consumed with compare-and-set semantics. A lost concurrent rotation rolls back the replacement insert and is treated as replay/reuse.

### Session revoke

Session and associated refresh tokens are revoked in one transaction with idempotent timestamp semantics.

## Domain materialization

Infrastructure rehydrates through controlled factories rather than reflection:

- `Customer.Restore`;
- `CustomerCredential.Restore`;
- `CustomerSession.Restore`;
- `RefreshToken.Restore`;
- `Wallet.Restore`;
- `BankAccount.Restore`.

## Redis OTP state

Redis stores only transient registration OTP challenge state. It never contains the raw OTP and cannot activate a customer by itself. Redis remains non-authoritative for financial correctness.

## Remaining financial schema

Later phases still need at minimum:

- FinancialTransactions;
- Ledger persistence for the already implemented double-entry domain;
- IdempotencyRecords;
- OutboxMessages / InboxMessages;
- FraudEvents;
- Merchants;
- ReconciliationRuns / ReconciliationIssues;
- AuditEvents.

All future financial schema changes must preserve double-entry invariants, durable idempotency and concurrency-safe state transitions.
