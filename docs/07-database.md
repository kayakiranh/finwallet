# Veritabanı Tasarımı / Database Design

## Türkçe

### Source of truth
MSSQL customer, authentication/session ve financial state için durable source of truth'tür. Redis MSSQL'nin alternatifi değildir ve para doğruluğunu tek başına garanti edemez.

Persistence explicit parameterized `Microsoft.Data.SqlClient` kullanır. Özellikle financial transaction/locking kodunda SQL transaction boundary görünür tutulur.

### Authentication schema
`database/001_authentication_schema.sql`:
- Customers;
- CustomerCredentials;
- CustomerSessions;
- RefreshTokens.

Raw password ve raw refresh token saklanmaz.

### Financial accounts schema
`database/002_financial_accounts_schema.sql`:
- Wallets;
- BankAccounts.

Wallet invariant'ları:
- `(CustomerId, Currency)` unique;
- non-negative available/blocked balances;
- customer ownership FK;
- durable lifecycle;
- `DECIMAL(19,4)` balance.

BankAccount invariant'ları:
- wallet başına tek BankAccount;
- internal ve provider AccountId ayrı;
- wallet/customer/currency composite relationship;
- provider account ID ve IBAN-like değer tutarlı;
- lifecycle update'leri expected status + `UpdatedAt` compare-and-set kullanır.

### Ledger/transaction schema
`database/003_ledger_transaction_schema.sql`:
- LedgerAccounts;
- FinancialTransactions;
- IdempotencyRecords;
- LedgerJournals;
- LedgerEntries.

**FinancialTransaction** business operation kaydıdır; Wallet row veya LedgerJournal ile aynı kavram değildir.

Stabil transaction type'ları:
1. WalletTransfer
2. BankDeposit
3. BankWithdrawal
4. Refund
5. Reversal

Lifecycle:
1. Processing
2. Completed
3. Failed
4. Reversed

### Financial decimal kuralı
Financial amount `DECIMAL(19,4)` uyumlu olmalıdır:
- maksimum 4 decimal place;
- maksimum absolute amount `999999999999999.9999`.

Application boundary DB overflow'a güvenmek yerine amount/balance kapasitesini SQL'e gitmeden önce doğrular.

### Durable idempotency
Identity:
```text
Scope + CustomerId + IdempotencyKey
```
Wallet-transfer scope: `WALLET_TRANSFER`.

Request fingerprint canonical source/destination/amount değerlerinden SHA-256 ile üretilir.

Davranış:
- same key + same payload -> wait/replay aynı transaction;
- same key + different payload -> conflict;
- final authority MSSQL;
- replay current wallet balance değil immutable transaction alanları döndürür.

Posting store Serializable isolation altında idempotency range'i `UPDLOCK, HOLDLOCK` ile korur.

### Wallet transfer accounting
Wallet Liability ledger account code:
```text
WALLET-LIABILITY:{walletId:N}
```

Muhasebe:
```text
Debit   source wallet liability       amount
Credit  destination wallet liability  amount
```
Bu pure internal liability reclassification'dır.

### Atomic posting — uygulanmış
`SqlWalletTransferPostingStore` aynı MSSQL transaction içinde:
```text
BEGIN SERIALIZABLE
-> claim/replay idempotency
-> wallet row locks (deterministic GUID order)
-> validate ownership/status/currency/balance
-> calculate domain debit/credit
-> find/create ledger accounts
-> insert Processing FinancialTransaction
-> create/post LedgerJournal + entries
-> SQL aggregate Debit/Credit validation
-> update wallet balances
-> finalize transaction
-> finalize idempotency
-> COMMIT
```
Her exception commit öncesi bütün finansal state'i rollback eder.

### Debit = Credit defense in depth
Domain `LedgerJournal.Post(...)` balance kontrolü yapar. Persistence ayrıca inserted entry toplamlarını SQL aggregate ile kontrol eder. En az iki entry, positive totals ve exact equality sağlanmadan commit edilmez.

### Domain rehydration
Persisted entity state reflection ile private setter zorlanarak değil, kontrollü `Restore` factory'leriyle materialize edilir.

### Redis state
Redis registration OTP gibi transient challenge state saklar. Wallet/Ledger/FinancialTransaction truth Redis'e taşınmaz.

### Güncel durum
Public `POST /api/v1/transfers` endpoint'i **uygulanmıştır** ve yukarıdaki posting store'u Application fraud/session orchestration üzerinden kullanır. Önceki dokümandaki “public transfer endpoint yok” notu artık geçerli değildir.

### Kalan database işleri
- BankDeposit/BankWithdrawal persistence workflow;
- FraudEvents/manual review;
- Outbox/Inbox;
- ReconciliationRuns/ReconciliationIssues;
- AuditEvents/operational logging store yaklaşımı;
- integration/concurrency test fixtures.

---

## English

### Source of truth
MSSQL is the durable source of truth for customer, authentication/session and financial state. Redis is not a replacement for MSSQL and cannot independently guarantee money correctness.

Persistence uses explicit parameterized `Microsoft.Data.SqlClient`. SQL transaction boundaries remain visible, especially in financial locking code.

### Authentication schema
`database/001_authentication_schema.sql`:
- Customers;
- CustomerCredentials;
- CustomerSessions;
- RefreshTokens.

Raw passwords and raw refresh tokens are never stored.

### Financial accounts schema
`database/002_financial_accounts_schema.sql`:
- Wallets;
- BankAccounts.

Wallet invariants:
- unique `(CustomerId, Currency)`;
- non-negative available/blocked balances;
- customer ownership FK;
- durable lifecycle;
- `DECIMAL(19,4)` balance.

BankAccount invariants:
- one BankAccount per Wallet;
- internal and provider AccountId remain separate;
- composite wallet/customer/currency relationship;
- provider account ID and IBAN-like value remain consistent;
- lifecycle updates use expected status + `UpdatedAt` compare-and-set.

### Ledger/transaction schema
`database/003_ledger_transaction_schema.sql` defines:
- LedgerAccounts;
- FinancialTransactions;
- IdempotencyRecords;
- LedgerJournals;
- LedgerEntries.

A **FinancialTransaction** is the durable business-operation record; it is not the same concept as a Wallet row or LedgerJournal.

Stable transaction types:
1. WalletTransfer
2. BankDeposit
3. BankWithdrawal
4. Refund
5. Reversal

Lifecycle:
1. Processing
2. Completed
3. Failed
4. Reversed

### Financial decimal rule
Financial amounts must fit `DECIMAL(19,4)`:
- maximum four decimal places;
- maximum absolute amount `999999999999999.9999`.

Application boundaries validate amount/balance capacity before SQL rather than relying on database overflow errors.

### Durable idempotency
Identity:
```text
Scope + CustomerId + IdempotencyKey
```
Wallet-transfer scope: `WALLET_TRANSFER`.

The request fingerprint is SHA-256 over canonical source/destination/amount values.

Behavior:
- same key + same payload -> wait/replay the same transaction;
- same key + different payload -> conflict;
- MSSQL is final authority;
- replay returns immutable transaction fields, not current wallet balances.

The posting store uses Serializable isolation with `UPDLOCK, HOLDLOCK` for the idempotency key range.

### Wallet-transfer accounting
Wallet Liability ledger account code:
```text
WALLET-LIABILITY:{walletId:N}
```

Accounting:
```text
Debit   source wallet liability       amount
Credit  destination wallet liability  amount
```
This is a pure internal liability reclassification.

### Atomic posting — implemented
`SqlWalletTransferPostingStore` performs in one MSSQL transaction:
```text
BEGIN SERIALIZABLE
-> claim/replay idempotency
-> wallet row locks (deterministic GUID order)
-> validate ownership/status/currency/balance
-> calculate domain debit/credit
-> find/create ledger accounts
-> insert Processing FinancialTransaction
-> create/post LedgerJournal + entries
-> SQL aggregate Debit/Credit validation
-> update wallet balances
-> finalize transaction
-> finalize idempotency
-> COMMIT
```
Any exception before commit rolls back the complete financial state.

### Debit = Credit defense in depth
Domain `LedgerJournal.Post(...)` validates balance. Persistence also aggregates inserted entries in SQL. Commit is forbidden unless there are at least two entries, positive totals and exact Debit/Credit equality.

### Domain rehydration
Persisted entity state is materialized through controlled `Restore` factories rather than reflection-based mutation of private setters.

### Redis state
Redis stores transient challenge state such as registration OTP. Wallet/Ledger/FinancialTransaction truth never moves to Redis.

### Current status
Public `POST /api/v1/transfers` is **implemented** and uses the posting store through Application fraud/session orchestration. The previous documentation statement that no public transfer endpoint existed is obsolete and has been removed.

### Remaining database work
- BankDeposit/BankWithdrawal persistence workflow;
- FraudEvents/manual review;
- Outbox/Inbox;
- ReconciliationRuns/ReconciliationIssues;
- AuditEvents/operational logging storage approach;
- integration/concurrency test fixtures.
