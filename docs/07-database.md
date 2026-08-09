# Database Design

## Source-of-truth policy

MSSQL is the durable source of truth for customer, authentication and financial state. Redis is not a replacement for MSSQL and is never sufficient to guarantee money correctness.

Persistence uses explicit parameterized `Microsoft.Data.SqlClient` commands rather than EF Core or a generic repository framework. Transaction boundaries and concurrency-sensitive SQL remain visible.

## Authentication schema

`database/001_authentication_schema.sql` defines Customers, CustomerCredentials, CustomerSessions and RefreshTokens. Raw passwords and raw refresh tokens are never stored.

## Financial account schema

`database/002_financial_accounts_schema.sql` defines durable Wallets and BankAccounts.

### Wallet invariants

- one Wallet per `(CustomerId, Currency)`;
- non-negative available/blocked balances;
- ownership FK to Customer;
- `RowVersion` available for later balance-concurrency workflows.

### BankAccount invariants

- one BankAccount per Wallet;
- internal BankAccount ID and external provider Account ID remain separate;
- composite FK `(WalletId, CustomerId, Currency) -> Wallets(Id, CustomerId, Currency)` prevents cross-owner/cross-currency links;
- provider AccountId and IBAN-like value must be present together;
- lifecycle changes use expected `Status + UpdatedAt` compare-and-set.

External bank HTTP calls never run inside a FinWallet SQL transaction.

## Ledger / financial transaction schema

`database/003_ledger_transaction_schema.sql` introduces:

- `LedgerAccounts`;
- `FinancialTransactions`;
- `IdempotencyRecords`;
- `LedgerJournals`;
- `LedgerEntries`.

### FinancialTransactions

A FinancialTransaction is the durable business-operation record; it is separate from HTTP requests, Wallet balance rows and LedgerJournal accounting rows.

Stable type values:

1. WalletTransfer
2. BankDeposit
3. BankWithdrawal
4. Refund
5. Reversal

Stable lifecycle values:

1. Processing
2. Completed
3. Failed
4. Reversed

Audit timestamps are intentionally separated:

- `CreatedAt` — transaction creation;
- `FinalizedAt` — original Completed/Failed finalization;
- `ReversedAt` — later reversal time, if any.

Reversal does not overwrite the original finalization timestamp.

For WalletTransfer, the database requires non-null, distinct source/destination wallets. Source wallet ownership/currency is enforced with a composite FK; destination currency is enforced with `(DestinationWalletId, Currency)` FK.

### IdempotencyRecords

Durable financial idempotency is keyed by:

```text
Scope + CustomerId + IdempotencyKey
```

The row also contains a canonical SHA-256 request fingerprint. Same key + different fingerprint will be treated as a conflict by the transfer engine.

Status values are Processing/Completed/Failed. A Processing record may already contain the in-flight FinancialTransaction `ResourceId`; this lets concurrent duplicate requests identify the same operation before it becomes final. `ResultCode` remains null until final state.

Redis may later accelerate short-lived duplicate detection, but MSSQL remains the final idempotency authority.

### LedgerAccounts

Ledger accounts are economic accounting accounts, not Wallet rows. Examples:

- wallet liability account;
- bank settlement asset;
- merchant payable liability;
- platform revenue;
- platform expense.

`Code` is globally unique and bounded to 128 characters. `SqlLedgerAccountStore` provides concurrency-safe get-or-create semantics and verifies that an existing code has the expected currency/accounting type.

Creating a structural ledger account does not move money and may occur before the financial posting transaction. Posting journals cannot occur through this store.

### LedgerJournals and LedgerEntries

Each FinancialTransaction can have one journal. Reversal transactions use their own journal and `ReversesJournalId` points to the original journal; a journal can be reversed at most once.

Ledger entry currency is enforced twice with composite foreign keys:

```text
LedgerEntries(JournalId, Currency)
    -> LedgerJournals(Id, Currency)

LedgerEntries(AccountId, Currency)
    -> LedgerAccounts(Id, Currency)
```

An entry therefore cannot silently use a currency different from its journal or ledger account.

Positive amount, Debit/Credit side and sequence constraints are enforced by MSSQL.

### Debit = Credit commit rule

Debit/credit equality is a cross-row invariant and cannot be expressed safely as a normal CHECK constraint. The upcoming financial posting store must use one SQL transaction:

```text
lock/load wallets
-> validate balances/currency
-> insert/update FinancialTransaction
-> insert LedgerJournal
-> insert LedgerEntries
-> SQL SUM(Debit) / SUM(Credit) verification
-> update Wallet balances
-> finalize IdempotencyRecord
-> COMMIT
```

If debit and credit totals are not equal, the transaction must roll back before any wallet balance or idempotency result becomes final.

A standalone ledger repository that commits journals independently from wallet balance updates is deliberately not provided.

## Domain materialization

Infrastructure uses controlled factories rather than reflection where persisted private-set state must be rehydrated:

- Customer.Restore;
- CustomerCredential.Restore;
- CustomerSession.Restore;
- RefreshToken.Restore;
- Wallet.Restore;
- BankAccount.Restore;
- LedgerAccount.Restore;
- FinancialTransaction.Restore.

## Redis OTP state

Redis stores transient registration OTP challenge state only. It cannot activate a customer or establish financial truth by itself.

## Remaining financial persistence

The next persistence slice will implement the atomic WalletTransfer posting store using the 003 schema. Later phases still need Outbox/Inbox, FraudEvents, Merchants, ReconciliationRuns/ReconciliationIssues and AuditEvents.
