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
- wallet lifecycle is durable;
- balances use `DECIMAL(19,4)`.

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

Audit timestamps are separated: `CreatedAt`, original `FinalizedAt`, and later optional `ReversedAt`. Reversal never overwrites the original finalization timestamp.

For WalletTransfer, the database requires non-null, distinct source/destination wallets. Source ownership/currency and destination currency are enforced with composite foreign keys.

### Financial decimal rule

`FinancialAmountRules` centralizes the `DECIMAL(19,4)` contract:

- at most four decimal places;
- maximum absolute amount `999999999999999.9999`.

Transfer request amount and post-transfer balances are validated before SQL parameter execution so financial overflow/scale errors are not delegated to the database provider.

### IdempotencyRecords

Durable financial idempotency is keyed by:

```text
Scope + CustomerId + IdempotencyKey
```

Wallet-transfer scope is `WALLET_TRANSFER`.

The request fingerprint is SHA-256 over canonical:

```text
sourceWalletId:N | destinationWalletId:N | amount:G29
```

Consequences:

- same key + same canonical request waits for/replays the same financial result;
- same key + different canonical request is an explicit conflict;
- the database, not Redis, is final authority;
- replay response uses immutable FinancialTransaction fields, not current wallet balances that may have changed after the original transfer.

The posting store runs at Serializable isolation and looks up the unique idempotency row using `UPDLOCK, HOLDLOCK`. The missing-key range is therefore protected while the first request claims it. A concurrent duplicate waits and then observes the committed Completed record instead of applying money twice.

A Processing row may contain the in-flight FinancialTransaction `ResourceId`. Synchronous wallet-transfer posting normally commits Processing -> Completed in the same SQL transaction; therefore a crash before COMMIT rolls the claim back with the financial state.

### LedgerAccounts

Wallet transfer uses stable ledger-account codes:

```text
WALLET-LIABILITY:{walletId:N}
```

These are Liability accounts in the Wallet currency. The atomic transfer store finds/creates them inside the same SQL transaction used for posting and verifies existing code/currency/type/status consistency.

### Wallet transfer accounting

A transfer changes two customer-liability accounts:

```text
Debit   source wallet liability       amount
Credit  destination wallet liability  amount
```

Source liability decreases while destination liability increases. The journal contains no balancing plug/system account because this is a pure internal liability reclassification.

### Atomic WalletTransfer posting

`SqlWalletTransferPostingStore` owns the full synchronous posting transaction. It deliberately replaces the unsafe idea of independently committing a journal repository.

Sequence:

```text
BEGIN TRANSACTION (SERIALIZABLE)
-> lock/claim idempotency key range
-> if Completed + same request: load immutable FinancialTransaction and replay
-> insert Processing idempotency claim
-> lock source/destination Wallet rows in deterministic GUID order
-> validate source ownership, wallet status, currency and balance
-> apply Wallet domain Debit/Credit in memory
-> validate post-transfer DECIMAL(19,4) capacity
-> find/create wallet Liability ledger accounts
-> insert Processing FinancialTransaction
-> create and Domain.Post balanced LedgerJournal
-> insert LedgerJournal + LedgerEntries
-> re-read SQL entry totals and verify Debit == Credit
-> update both Wallet balances
-> finalize FinancialTransaction as Completed
-> finalize IdempotencyRecord as Completed
-> COMMIT
```

Any exception before COMMIT causes the SQL transaction to roll back as one unit. There is no state where wallet money moved but ledger/idempotency did not, or vice versa.

Wallet rows are locked in deterministic GUID order. This reduces deadlock risk for opposite-direction concurrent transfers such as A->B and B->A.

Destination wallet must currently be Active for WalletTransfer. Source wallet must be Active and owned by the authenticated customer. Source/destination currencies must match.

### Debit = Credit defense in depth

The Domain `LedgerJournal.Post(...)` checks balance before persistence. After entry inserts, the posting store also performs an SQL aggregate check of persisted rows inside the same transaction. COMMIT is forbidden if:

- fewer than two entries exist;
- total Debit <= 0;
- total Credit <= 0;
- total Debit != total Credit.

Composite foreign keys additionally prevent entry currency from differing from its Journal or LedgerAccount.

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

## Remaining transfer work

The atomic posting store is implemented but no public transfer endpoint is exposed yet. Before exposing it, the Application transfer handler will add server-derived fraud/velocity/device/beneficiary signals and combine internal FraudEngine + external FakeFraud decisions outside the financial SQL transaction.

Later phases still need Outbox/Inbox, FraudEvents, Merchants, ReconciliationRuns/ReconciliationIssues and AuditEvents.
