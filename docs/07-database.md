# Database Design

## Source-of-truth policy

MSSQL is the durable source of truth for customer, authentication and all future financial state. Redis is not a replacement for MSSQL and is never sufficient to guarantee money correctness.

The initial persistence implementation uses explicit parameterized `Microsoft.Data.SqlClient` commands rather than EF Core or a generic repository framework. This keeps transaction boundaries and concurrency-sensitive SQL visible.

## Authentication schema

The initial schema is defined in `database/001_authentication_schema.sql`.

### Customers

Purpose: intentionally small end-customer identity/contact/lifecycle record.

Key fields:

- `Id` — primary key;
- `CountryCode` — two-letter registration country;
- `PhoneNumber` — normalized international phone number;
- `Email` — optional contact email;
- `Status` — customer lifecycle state;
- `CreatedAt`;
- `RowVersion`.

Important constraints:

- `PhoneNumber` has a unique constraint. This is the final concurrency guarantee against duplicate registrations; application existence checks are not treated as sufficient.
- `Status` is constrained to supported enum values.

### CustomerCredentials

Purpose: password and login-lockout state separated from `Customers`.

Key fields:

- `CustomerId` — PK/FK to `Customers`;
- `PasswordHash`;
- `PasswordSalt`;
- `PasswordHashVersion`;
- `FailedLoginCount`;
- `LockedUntil`;
- `PasswordChangedAt`;
- `RowVersion`.

Raw passwords are never stored.

### CustomerSessions

Purpose: server-side device/session lifecycle independent of short-lived JWT access tokens.

Key fields:

- `Id`;
- `CustomerId`;
- `DeviceId`;
- `CreatedAt`;
- `LastActivityAt`;
- `ExpiresAt`;
- `RevokedAt`;
- `RowVersion`.

Indexes support customer/session expiration lookup.

### RefreshTokens

Purpose: single-use refresh-token rotation state.

Key fields:

- `Id`;
- `SessionId`;
- `TokenHash` — SHA-256 hash, never raw token;
- `CreatedAt`;
- `ExpiresAt`;
- `ConsumedAt`;
- `RevokedAt`;
- `ReplacedByTokenId` — self-reference to rotation successor;
- `RowVersion`.

Important constraints:

- `TokenHash` is unique;
- token belongs to a session through FK;
- replacement token uses a self-FK;
- expiration must be after creation.

## Transaction boundaries

### Registration

`Customer` and `CustomerCredential` are inserted in one short `READ COMMITTED` transaction.

```text
BEGIN
  INSERT Customer
  INSERT CustomerCredential
COMMIT
```

OTP generation and FakeCommunication HTTP calls happen only after the SQL transaction has completed.

### Successful login

Credential lockout-reset state, new `CustomerSession` and first `RefreshToken` hash are written in one SQL transaction.

### Refresh rotation

Refresh rotation is implemented as database compare-and-set behavior:

```text
BEGIN
  INSERT replacement refresh token

  UPDATE old refresh token
     SET ConsumedAt = ...,
         ReplacedByTokenId = replacement
   WHERE Id = ...
     AND SessionId = ...
     AND TokenHash = ...
     AND ConsumedAt IS NULL
     AND RevokedAt IS NULL

  IF affected rows != 1
      ROLLBACK and report lost rotation race

  UPDATE session LastActivityAt
COMMIT
```

The replacement is inserted first because the old token has a foreign key to `ReplacedByTokenId`. If the conditional consume loses a concurrency race, the entire transaction rolls back, including the replacement insert.

The Application handler treats a lost rotation race as token reuse/replay and revokes the associated session/token family.

### Session revoke

Session `RevokedAt` and all refresh-token records for the session are updated in one transaction. The operation is idempotent through `COALESCE(RevokedAt, @RevokedAt)` semantics.

## Domain materialization

Infrastructure does not use reflection to mutate private setters. Persisted rows are rehydrated through controlled domain `Restore(...)` factories:

- `Customer.Restore`;
- `CustomerCredential.Restore`;
- `CustomerSession.Restore`;
- `RefreshToken.Restore`.

These factories validate persisted lifecycle consistency and reject obviously corrupted state rather than silently accepting it.

## Redis OTP state

Redis stores only transient registration OTP challenge state.

Keys:

```text
finwallet:registration:otp:{customerId}
finwallet:registration:otp-cooldown:{customerId}
```

The OTP key is a Redis hash containing:

- `digest` — customer-bound HMAC-SHA256 digest;
- `attempts` — failed verification count.

Fixed policy:

- TTL: 5 minutes;
- maximum failed attempts: 5;
- resend cooldown: 30 seconds.

Lua scripts atomically implement:

- cooldown check + challenge replacement + TTL;
- digest comparison + attempt increment + challenge deletion on success/exhaustion.

Redis does not contain the raw OTP. Redis unavailability fails registration verification safely and cannot activate a customer by itself.

## Future financial schema

Later phases will add at minimum:

- Wallets;
- BankAccounts;
- FinancialTransactions;
- LedgerAccounts;
- LedgerJournals;
- LedgerEntries;
- IdempotencyRecords;
- OutboxMessages;
- InboxMessages;
- FraudEvents;
- Merchants;
- ReconciliationRuns;
- ReconciliationIssues;
- AuditEvents.

Financial schema changes must preserve double-entry balance invariants and durable idempotency guarantees.
