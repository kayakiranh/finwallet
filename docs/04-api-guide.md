# FinWallet API Rehberi / API Guide

## Türkçe

### Public giriş noktası
Normal client yalnız Gateway'i çağırır:
```text
http://localhost:8080
```
Ana prefix: `/api/v1`.

Anonymous rotalar:
- `POST /api/v1/auth/register`
- `POST /api/v1/auth/registration/verify`
- `POST /api/v1/auth/login`
- `POST /api/v1/auth/refresh`

Diğer public `/api/*` rotaları Gateway'de JWT ister. FinWallet.Api JWT/session/ownership kontrolünü ayrıca tekrarlar.

### Ortak contract
Tüm API response'ları `ServiceResult<T>` kullanır. Client branching sırası:
1. HTTP status;
2. stabil `code`.

`message` parse edilerek business logic yazılmamalıdır.

Para değiştiren command'larda `Idempotency-Key` zorunludur. Aynı key+aynı canonical payload replay; aynı key+farklı payload 409 conflict üretir.

Client `X-Correlation-Id` gönderebilir; correlation ID transaction/idempotency kimliği değildir ve PII içermemelidir.

### Auth
**POST `/api/v1/auth/register`** — anonymous, HTTP 202.  
**POST `/api/v1/auth/registration/verify`** — anonymous.  
**POST `/api/v1/auth/login`** — anonymous; durable session + access/refresh token.  
**POST `/api/v1/auth/refresh`** — anonymous; single-use refresh rotation/reuse detection.  
**POST `/api/v1/auth/logout`** — JWT; current `sid` durable olarak revoke edilir.

### Wallet
**POST `/api/v1/wallets`** — JWT.
```json
{ "currency": "TRY" }
```
Currency: `TRY`, `USD`, `EUR`.

**GET `/api/v1/wallets`** — JWT; yalnız authenticated customer wallet'ları.

### Bank account
**POST `/api/v1/bank-accounts`** — JWT.
```json
{ "walletId": "<wallet-guid>" }
```
External provider HTTP açık SQL transaction içinde çalışmaz.

### Bank -> Wallet deposit
**POST `/api/v1/bank-movements/deposits`** — JWT + `Idempotency-Key`.
```json
{
  "bankAccountId": "<bank-account-guid>",
  "amount": 1000.00
}
```
FinWallet `BankDeposit`; FakeBank tarafında external account debit/withdrawal. Provider pending ise HTTP 202 olabilir; completed ise 200.

### Wallet -> Bank withdrawal
**POST `/api/v1/bank-movements/withdrawals`** — JWT + `Idempotency-Key`.
```json
{
  "bankAccountId": "<bank-account-guid>",
  "amount": 100.00
}
```
Akış:
```text
server-side BankAccount/customer context
-> FakeCutoff
-> durable idempotency
-> available -> blocked reservation
-> SQL commit
-> provider HTTP
-> Completed: blocked settle + ledger + outbox
-> terminal failure: blocked release + Failed
```
Cutoff sonrası HTTP 202 `Scheduled` dönebilir. Background processor due movement'ları ilerletir.

### Wallet transfer
**POST `/api/v1/transfers`** — JWT + active durable session + `Idempotency-Key`.
```json
{
  "sourceWalletId": "aaaaaaaa-1111-4111-8111-111111111111",
  "destinationWalletId": "bbbbbbbb-2222-4222-8222-222222222222",
  "amount": 125.50
}
```

Akış:
```text
completed replay
-> server-side risk signals
-> internal fraud
-> FakeFraud
-> durable FraudEvent
-> Allow/Approved
-> atomic balances + transaction + ledger + idempotency + outbox
```

`Review` -> HTTP 202 ve para hareketi yok. Internal approval sonrası aynı request/key ile devam edilir.

### Merchant purchase
**POST `/api/v1/purchases`** — JWT + active `sid` + `Idempotency-Key`.
```json
{
  "walletId": "<wallet-guid>",
  "merchantId": "<merchant-id>",
  "amount": 100.00
}
```
Purchase fraud -> FakeCampaign -> atomic purchase posting.

Platform-sponsored örnek:
```text
Original 100
Discount 10
Customer pays 90
Merchant receives 100
Platform Campaign Expense 10
```
Ledger balanced kalır.

### Refund
**POST `/api/v1/transactions/{transactionId}/refund`** — JWT + `Idempotency-Key`.
Yalnız completed Purchase tam refund edilir. Original history overwrite edilmez; yeni Refund transaction ve opposite journal oluşur.

### Reversal
**POST `/api/v1/transactions/{transactionId}/reversal`** — JWT + `Idempotency-Key`.
Yalnız completed internal WalletTransfer için güvenli reversal uygulanır. External-bank movements provider compensation gerektirir.

### Transaction history
**GET `/api/v1/transactions?take=50&beforeTransactionId=<guid>`** — JWT.
Newest-first keyset pagination. Raw ledger entry veya PII dönmez.

### Internal bank callback
**POST `/api/v1/internal/bank/callbacks`** — public JWT endpoint'i değildir; Gateway `InternalService` policy gerekir.
```json
{
  "messageId": "provider-message-001",
  "externalTransactionId": "<guid>",
  "status": "Completed"
}
```
Inbox `Source + MessageId + payload hash` ile dedupe eder.

### Internal fraud review
**GET `/api/v1/internal/fraud-reviews?take=50`** — internal service.  
**POST `/api/v1/internal/fraud-reviews/{fraudEventId}/decision`** — internal service + `X-Reviewer-Id`.
```json
{ "approve": true }
```
Pending event yalnız bir kez Approved/Denied olur.

### Internal reconciliation
**POST `/api/v1/internal/reconciliation/runs/{scope}`** — internal service.
Scope:
- `WalletLedger`
- `BankSettlementLedger`
- `ExternalBankStatement`

**GET `/api/v1/internal/reconciliation/runs/{runId}`**  
**GET `/api/v1/internal/reconciliation/runs/{runId}/issues?take=200`**

Reconciliation mismatch raporlar; wallet/ledger/bank state'ini otomatik overwrite etmez.

### Provider rotaları
```text
/providers/bank/*
/providers/fraud/*
/providers/cutoff/*
/providers/campaign/*
/providers/communication/*
```
Public client API değildir. Gateway internal-service authorization, provider destination ise downstream key doğrular.

### Başlıca hata contract'ları
- 401: invalid/missing auth/session.
- 403: fraud denied / forbidden.
- 404: owned resource/callback target bulunamadı.
- 409: idempotency conflict, insufficient balance, invalid correction/review state.
- 202: pending/scheduled/manual review.
- 429: rate limit / temporary auth lock.
- 503: required dependency temporarily unavailable.

### Swagger
Local/development'ta tüm Web API projelerinde Swagger vardır. Production Compose overlay'de kapatılır. Normal business çağrısı yine Gateway üzerinden yapılır.

---

## English

### Public entry point
Normal clients call only the Gateway:
```text
http://localhost:8080
```
Main prefix: `/api/v1`.

Anonymous routes:
- `POST /api/v1/auth/register`
- `POST /api/v1/auth/registration/verify`
- `POST /api/v1/auth/login`
- `POST /api/v1/auth/refresh`

Other public `/api/*` routes require JWT at the Gateway. FinWallet.Api independently repeats JWT/session/ownership checks.

### Common contract
All responses use `ServiceResult<T>`. Client branching order:
1. HTTP status;
2. stable `code`.

Do not parse human-readable `message` text for business logic.

Money-changing commands require `Idempotency-Key`. Same key + same canonical payload replays; same key + different payload returns 409 conflict.

Clients may send `X-Correlation-Id`; correlation is not a transaction/idempotency identifier and must contain no PII.

### Authentication
**POST `/api/v1/auth/register`** — anonymous, HTTP 202.  
**POST `/api/v1/auth/registration/verify`** — anonymous.  
**POST `/api/v1/auth/login`** — anonymous; creates durable session + access/refresh tokens.  
**POST `/api/v1/auth/refresh`** — anonymous; single-use refresh rotation/reuse detection.  
**POST `/api/v1/auth/logout`** — JWT; durably revokes current `sid`.

### Wallet
**POST `/api/v1/wallets`** — JWT.
```json
{ "currency": "TRY" }
```
Currencies: `TRY`, `USD`, `EUR`.

**GET `/api/v1/wallets`** — JWT; authenticated customer's wallets only.

### Bank account
**POST `/api/v1/bank-accounts`** — JWT.
```json
{ "walletId": "<wallet-guid>" }
```
External-provider HTTP never runs inside an open SQL transaction.

### Bank -> Wallet deposit
**POST `/api/v1/bank-movements/deposits`** — JWT + `Idempotency-Key`.
```json
{
  "bankAccountId": "<bank-account-guid>",
  "amount": 1000.00
}
```
FinWallet treats it as `BankDeposit`; FakeBank treats it as external-account debit/withdrawal. Provider pending may return HTTP 202; completed returns 200.

### Wallet -> Bank withdrawal
**POST `/api/v1/bank-movements/withdrawals`** — JWT + `Idempotency-Key`.
```json
{
  "bankAccountId": "<bank-account-guid>",
  "amount": 100.00
}
```
Flow:
```text
server-side BankAccount/customer context
-> FakeCutoff
-> durable idempotency
-> available -> blocked reservation
-> SQL commit
-> provider HTTP
-> Completed: blocked settle + ledger + outbox
-> terminal failure: blocked release + Failed
```
After cutoff the API may return HTTP 202 `Scheduled`. A background processor advances due movements.

### Wallet transfer
**POST `/api/v1/transfers`** — JWT + active durable session + `Idempotency-Key`.
```json
{
  "sourceWalletId": "aaaaaaaa-1111-4111-8111-111111111111",
  "destinationWalletId": "bbbbbbbb-2222-4222-8222-222222222222",
  "amount": 125.50
}
```

Flow:
```text
completed replay
-> server-side risk signals
-> internal fraud
-> FakeFraud
-> durable FraudEvent
-> Allow/Approved
-> atomic balances + transaction + ledger + idempotency + outbox
```

`Review` -> HTTP 202 and no money moves. After internal approval, resend the same request/key to continue.

### Merchant purchase
**POST `/api/v1/purchases`** — JWT + active `sid` + `Idempotency-Key`.
```json
{
  "walletId": "<wallet-guid>",
  "merchantId": "<merchant-id>",
  "amount": 100.00
}
```
Purchase fraud -> FakeCampaign -> atomic purchase posting.

Platform-sponsored example:
```text
Original 100
Discount 10
Customer pays 90
Merchant receives 100
Platform Campaign Expense 10
```
The ledger remains balanced.

### Refund
**POST `/api/v1/transactions/{transactionId}/refund`** — JWT + `Idempotency-Key`.
Only completed Purchase transactions are fully refunded. Original history is never overwritten; a new Refund transaction and opposite journal are created.

### Reversal
**POST `/api/v1/transactions/{transactionId}/reversal`** — JWT + `Idempotency-Key`.
Safe only for completed internal WalletTransfer. External-bank movements require provider compensation.

### Transaction history
**GET `/api/v1/transactions?take=50&beforeTransactionId=<guid>`** — JWT.
Newest-first keyset pagination. Raw ledger entries and PII are not returned.

### Internal bank callback
**POST `/api/v1/internal/bank/callbacks`** — not a public JWT endpoint; requires Gateway `InternalService` policy.
```json
{
  "messageId": "provider-message-001",
  "externalTransactionId": "<guid>",
  "status": "Completed"
}
```
Inbox deduplicates by `Source + MessageId + payload hash`.

### Internal fraud review
**GET `/api/v1/internal/fraud-reviews?take=50`** — internal service.  
**POST `/api/v1/internal/fraud-reviews/{fraudEventId}/decision`** — internal service + `X-Reviewer-Id`.
```json
{ "approve": true }
```
A Pending event can transition only once to Approved/Denied.

### Internal reconciliation
**POST `/api/v1/internal/reconciliation/runs/{scope}`** — internal service.
Scopes:
- `WalletLedger`
- `BankSettlementLedger`
- `ExternalBankStatement`

**GET `/api/v1/internal/reconciliation/runs/{runId}`**  
**GET `/api/v1/internal/reconciliation/runs/{runId}/issues?take=200`**

Reconciliation reports mismatches; it never automatically overwrites wallet/ledger/bank state.

### Provider routes
```text
/providers/bank/*
/providers/fraud/*
/providers/cutoff/*
/providers/campaign/*
/providers/communication/*
```
These are not public client APIs. Gateway validates internal-service authorization and provider destinations validate the downstream key.

### Main error contracts
- 401: invalid/missing authentication/session.
- 403: fraud denied / forbidden.
- 404: owned resource/callback target not found.
- 409: idempotency conflict, insufficient balance, invalid correction/review state.
- 202: pending/scheduled/manual review.
- 429: rate limit / temporary authentication lock.
- 503: required dependency temporarily unavailable.

### Swagger
All Web API projects expose Swagger in local/development. The production Compose overlay disables it. Normal business traffic still goes through the Gateway.
