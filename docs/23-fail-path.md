# FinWallet Fail Path - Hata ve Recovery Senaryoları / FinWallet Fail Path - Failure and Recovery Scenarios

## Türkçe

Bu doküman FinWallet v1'de önemli hata senaryolarında sistemin nasıl davrandığını gösterir. Amaç yalnız HTTP status göstermek değil; müşteri bakiyesi, ledger, idempotency ve retry state'inin ne olduğunu da netleştirmektir.

### 0. Temel güvenlik kuralı

Normal müşteri çağrıları YARP Gateway üzerinden yapılır.

```text
{{gateway}} = http://localhost:8080
```

Finansal işlem için genel kural:

```text
External HTTP çağrısı açık MSSQL financial transaction içinde yapılmaz.
Completed para hareketi communication hatası yüzünden geri alınmaz.
Fraud belirsizse transfer/purchase fail-closed davranır.
MSSQL finansal source of truth'tur.
```

### 1. FAIL - JWT yok veya geçersiz

```http
GET {{gateway}}/api/v1/wallets
```

Gateway backend'e göndermeden reddeder.

```json
{
  "isSuccess": false,
  "code": "GATEWAY_UNAUTHORIZED",
  "message": "A valid access token is required by the gateway.",
  "data": null,
  "errors": []
}
```

HTTP: `401 Unauthorized`.

```text
MSSQL değişikliği: yok
Wallet/Ledger değişikliği: yok
Retry: geçerli JWT alındıktan sonra
```

### 2. FAIL - Registration country/phone uygun değil

Örnek olarak TR seçilip uyumsuz telefon prefix'i gönderilirse:

```http
POST {{gateway}}/api/v1/auth/register
Content-Type: application/json
```

```json
{
  "countryCode": "TR",
  "phoneNumber": "+994501234567",
  "email": "invalid@example.test",
  "password": "Example-Password-123!"
}
```

Response:

```json
{
  "isSuccess": false,
  "code": "REGISTRATION_NOT_ALLOWED",
  "message": "The registration country or phone number is not eligible.",
  "data": null,
  "errors": []
}
```

HTTP: `400 Bad Request`.

### 3. FAIL - OTP yanlış, expired veya consume edilmiş

```http
POST {{gateway}}/api/v1/auth/registration/verify
Content-Type: application/json
```

```json
{
  "customerId": "11111111-1111-1111-1111-111111111111",
  "code": "999999"
}
```

Response:

```json
{
  "isSuccess": false,
  "code": "INVALID_REGISTRATION_OTP",
  "message": "The verification code is invalid, expired or already consumed.",
  "data": null,
  "errors": []
}
```

HTTP: `400 Bad Request`.

Resend çok hızlı yapılırsa:

```text
429 OTP_RESEND_RATE_LIMIT
```

### 4. FAIL - Login yanlış parola veya lockout

Yanlış credential:

```json
{
  "isSuccess": false,
  "code": "INVALID_CREDENTIALS",
  "message": "The supplied credentials are invalid.",
  "data": null,
  "errors": []
}
```

HTTP: `401 Unauthorized`.

Failed-login limiti sonrası:

```json
{
  "isSuccess": false,
  "code": "AUTH_TEMPORARILY_LOCKED",
  "message": "Authentication is temporarily unavailable for this credential.",
  "data": null,
  "errors": []
}
```

HTTP: `429 Too Many Requests`.

### 5. FAIL - BankAccount opening sırasında FakeBank unavailable

Client request:

```http
POST {{gateway}}/api/v1/bank-accounts
Authorization: Bearer {{token}}
Content-Type: application/json
```

```json
{
  "walletId": "44444444-4444-4444-4444-444444444444"
}
```

FinWallet internal BankAccount `Opening` kaydını önce durable yazar ve SQL transaction'ı kapatır. Sonra FakeBank çağrılır.

FakeBank unavailable/timeout durumunda provider error örnekleri:

```text
FAKE_BANK_UNAVAILABLE
BANK_PROVIDER_TIMEOUT
BANK_PROVIDER_NETWORK_ERROR
```

Retryable provider exception public API'de `503 Service Unavailable` olarak dönebilir. Durable internal BankAccount kaydı kaybolmaz; aynı internal id'den aynı deterministic provider request key üretilir. Retry duplicate dış hesap açmamalıdır.

```text
Wallet balance değişikliği: yok
Ledger değişikliği: yok
BankAccount durable state: Opening kalabilir
Retry: güvenli, aynı provider request key
```

### 6. RESILIENCE - BankDeposit provider timeout/network

Client:

```http
POST {{gateway}}/api/v1/bank-movements/deposits
Authorization: Bearer {{token}}
Idempotency-Key: fail-bank-deposit-001
Content-Type: application/json
```

```json
{
  "bankAccountId": "55555555-5555-5555-5555-555555555555",
  "amount": 1000.00
}
```

FinWallet önce durable bank movement + idempotency state oluşturur. FakeBank çağrısı timeout/network ile başarısız olursa handler retryable provider hatasını finansal failure olarak kesinleştirmez.

Beklenen public lifecycle response:

```json
{
  "isSuccess": true,
  "code": "BANKDEPOSIT_PENDING",
  "message": "BankDeposit state is Pending.",
  "data": {
    "operation": "BankDeposit",
    "amount": 1000.00,
    "currency": "TRY",
    "state": "Pending",
    "wasReplay": false
  },
  "errors": []
}
```

HTTP: `202 Accepted`.

Background `BankMoneyMovementBackgroundService` due operation'ı tekrar poll/start eder.

```text
Wallet balance değişikliği: henüz yok
Ledger posting: henüz yok
Durable transaction: Processing/Pending
Retry: background worker
```

### 7. FAIL - FakeBank hesabında yeterli para yok

Bankadan wallet'a 1.000 TRY çekilmek istenir fakat FakeBank external account yalnız 100 TRY içerir.

FakeBank provider hesabı negatife düşürmez. Provider transaction conflict/terminal error üretir.

Provider tarafında tipik code:

```text
BANK_TRANSACTION_CONFLICT
```

FinWallet `BankMoneyMovementProcessor` non-retryable provider hatasını terminal `Failed` state'e çevirir.

Public bank-movement sonucu business lifecycle olarak Failed olabilir:

```json
{
  "isSuccess": true,
  "code": "BANKDEPOSIT_FAILED",
  "message": "BankDeposit state is Failed.",
  "data": {
    "operation": "BankDeposit",
    "amount": 1000.00,
    "currency": "TRY",
    "state": "Failed",
    "wasReplay": false
  },
  "errors": []
}
```

```text
External bank balance: değişmez
Wallet available balance: değişmez
Ledger posting: oluşmaz
Idempotency: terminal failure state
```

### 8. FAIL - Wallet transfer için bakiye yetersiz

```http
POST {{gateway}}/api/v1/transfers
Authorization: Bearer {{token}}
Idempotency-Key: fail-transfer-balance-001
Content-Type: application/json
```

```json
{
  "sourceWalletId": "11111111-aaaa-aaaa-aaaa-111111111111",
  "destinationWalletId": "22222222-bbbb-bbbb-bbbb-222222222222",
  "amount": 5000.00
}
```

Kaynak wallet available balance 500 TRY ise:

```json
{
  "isSuccess": false,
  "code": "INSUFFICIENT_BALANCE",
  "message": "The source wallet has insufficient available balance.",
  "data": null,
  "errors": []
}
```

HTTP: `409 Conflict`.

Atomic posting transaction rollback olur; kısmi ledger/balance değişikliği kalmaz.

### 9. FAIL - Fraud provider unavailable

Transfer/Purchase internal fraud Deny değilse dış FakeFraud zorunludur. Network/timeout/invalid response durumunda fail-closed:

```json
{
  "isSuccess": false,
  "code": "FRAUD_DEPENDENCY_UNAVAILABLE",
  "message": "The required fraud service is temporarily unavailable.",
  "data": null,
  "errors": []
}
```

HTTP: `503 Service Unavailable`.

```text
Wallet balance: değişmez
Ledger: değişmez
Financial posting: başlamaz
```

### 10. FAIL - Aynı Idempotency-Key farklı request ile kullanılır

İlk request:

```json
{
  "destinationWalletId": "22222222-bbbb-bbbb-bbbb-222222222222",
  "amount": 100.00
}
```

Aynı key ile ikinci request:

```json
{
  "destinationWalletId": "33333333-cccc-cccc-cccc-333333333333",
  "amount": 200.00
}
```

Response:

```json
{
  "isSuccess": false,
  "code": "IDEMPOTENCY_CONFLICT",
  "message": "The Idempotency-Key was already used with a different transfer request.",
  "data": null,
  "errors": []
}
```

HTTP: `409 Conflict`.

Bu davranış duplicate protection'ın yanlış request'i replay etmesini engeller.

### 11. FAIL - Notification provider down ama para tamamlandı

BankDeposit veya Purchase atomic commit içinde Outbox yazılmış olsun. Financial transaction artık `Completed`.

Outbox worker FakeCommunication'a giderken provider 503/timeout üretirse:

```text
FinancialTransaction: Completed kalır
Wallet/Ledger: geri alınmaz
Outbox: Processed olmaz
Outbox: backoff ile yeniden Available olur
```

Worker failure code örnekleri:

```text
COMMUNICATION_UNAVAILABLE
OUTBOX_DISPATCH_ERROR
OUTBOX_RECIPIENT_UNAVAILABLE
OUTBOX_INVALID_PAYLOAD
```

Bu senaryoda istemciye daha önce verilen finansal success geri çevrilmez.

### 12. FAIL - Rate limit / method / content type

Rate limit:

```json
{
  "isSuccess": false,
  "code": "RATE_LIMITED",
  "message": "Too many requests.",
  "data": null,
  "errors": []
}
```

TRACE veya CONNECT:

```text
405 METHOD_NOT_ALLOWED
```

POST/PUT/PATCH body JSON değilse:

```text
415 UNSUPPORTED_MEDIA_TYPE
```

### 13. Failure davranış özeti

| Failure | Para değişir mi? | Ledger yazılır mı? | Retry |
|---|---:|---:|---|
| Gateway unauthorized | Hayır | Hayır | Yeni JWT ile |
| Invalid OTP/login | Hayır | Hayır | Kuralına göre |
| Bank account provider timeout | Hayır | Hayır | Aynı request key |
| BankDeposit provider transient error | Henüz hayır | Henüz hayır | Background |
| External bank insufficient balance | Hayır | Hayır | Terminal failure |
| Transfer insufficient balance | Hayır | Hayır | Bakiye sonrası yeni request |
| Fraud unavailable | Hayır | Hayır | Dependency düzelince |
| Idempotency conflict | Hayır | Hayır | Yeni doğru key/request |
| Notification unavailable after commit | Evet, daha önce tamamlandı | Evet | Outbox retry |

---

## English

This document describes the important FinWallet v1 failure behaviors. It does not stop at HTTP status codes; it also explains what happens to customer balances, Ledger, idempotency and retry state.

### 0. Core safety rule

Normal customer traffic goes through YARP Gateway.

```text
{{gateway}} = http://localhost:8080
```

General financial rules:

```text
No external HTTP call runs inside an open MSSQL financial transaction.
A Completed money movement is never rolled back because communication failed.
Transfer/Purchase fail closed when fraud is uncertain.
MSSQL is the financial source of truth.
```

### 1. FAIL - Missing or invalid JWT

```http
GET {{gateway}}/api/v1/wallets
```

Gateway rejects the request before forwarding it.

```json
{
  "isSuccess": false,
  "code": "GATEWAY_UNAUTHORIZED",
  "message": "A valid access token is required by the gateway.",
  "data": null,
  "errors": []
}
```

HTTP: `401 Unauthorized`.

```text
MSSQL change: none
Wallet/Ledger change: none
Retry: after obtaining a valid JWT
```

### 2. FAIL - Registration country/phone mismatch

Example: TR is selected but an incompatible phone prefix is supplied.

```http
POST {{gateway}}/api/v1/auth/register
Content-Type: application/json
```

```json
{
  "countryCode": "TR",
  "phoneNumber": "+994501234567",
  "email": "invalid@example.test",
  "password": "Example-Password-123!"
}
```

Response:

```json
{
  "isSuccess": false,
  "code": "REGISTRATION_NOT_ALLOWED",
  "message": "The registration country or phone number is not eligible.",
  "data": null,
  "errors": []
}
```

HTTP: `400 Bad Request`.

### 3. FAIL - OTP invalid, expired or consumed

```http
POST {{gateway}}/api/v1/auth/registration/verify
Content-Type: application/json
```

```json
{
  "customerId": "11111111-1111-1111-1111-111111111111",
  "code": "999999"
}
```

Response:

```json
{
  "isSuccess": false,
  "code": "INVALID_REGISTRATION_OTP",
  "message": "The verification code is invalid, expired or already consumed.",
  "data": null,
  "errors": []
}
```

HTTP: `400 Bad Request`.

Resending too quickly returns:

```text
429 OTP_RESEND_RATE_LIMIT
```

### 4. FAIL - Invalid login or lockout

Invalid credentials:

```json
{
  "isSuccess": false,
  "code": "INVALID_CREDENTIALS",
  "message": "The supplied credentials are invalid.",
  "data": null,
  "errors": []
}
```

HTTP: `401 Unauthorized`.

After the failed-login limit:

```json
{
  "isSuccess": false,
  "code": "AUTH_TEMPORARILY_LOCKED",
  "message": "Authentication is temporarily unavailable for this credential.",
  "data": null,
  "errors": []
}
```

HTTP: `429 Too Many Requests`.

### 5. FAIL - FakeBank unavailable during BankAccount opening

Client request:

```http
POST {{gateway}}/api/v1/bank-accounts
Authorization: Bearer {{token}}
Content-Type: application/json
```

```json
{
  "walletId": "44444444-4444-4444-4444-444444444444"
}
```

FinWallet first durably writes the internal BankAccount in `Opening` state, closes the SQL transaction, then calls FakeBank.

Example provider errors:

```text
FAKE_BANK_UNAVAILABLE
BANK_PROVIDER_TIMEOUT
BANK_PROVIDER_NETWORK_ERROR
```

A retryable provider exception can be exposed as `503 Service Unavailable`. The durable internal BankAccount is not lost. A deterministic provider request key is derived from the same internal ID, so retries should not open duplicate external accounts.

```text
Wallet balance change: none
Ledger change: none
BankAccount durable state: may remain Opening
Retry: safe with same provider request key
```

### 6. RESILIENCE - BankDeposit provider timeout/network

Client:

```http
POST {{gateway}}/api/v1/bank-movements/deposits
Authorization: Bearer {{token}}
Idempotency-Key: fail-bank-deposit-001
Content-Type: application/json
```

```json
{
  "bankAccountId": "55555555-5555-5555-5555-555555555555",
  "amount": 1000.00
}
```

FinWallet first creates durable bank-movement and idempotency state. If the FakeBank call times out or has a network failure, the handler does not finalize the operation as a financial failure.

Expected lifecycle response:

```json
{
  "isSuccess": true,
  "code": "BANKDEPOSIT_PENDING",
  "message": "BankDeposit state is Pending.",
  "data": {
    "operation": "BankDeposit",
    "amount": 1000.00,
    "currency": "TRY",
    "state": "Pending",
    "wasReplay": false
  },
  "errors": []
}
```

HTTP: `202 Accepted`.

`BankMoneyMovementBackgroundService` later retries/polls the due operation.

```text
Wallet balance change: not yet
Ledger posting: not yet
Durable transaction: Processing/Pending
Retry: background worker
```

### 7. FAIL - External FakeBank account has insufficient money

The client requests 1,000 TRY BankDeposit while the external account contains only 100 TRY.

FakeBank prevents negative provider balance and produces a transaction conflict/terminal error.

Typical provider code:

```text
BANK_TRANSACTION_CONFLICT
```

FinWallet converts the non-retryable provider error into terminal `Failed` state.

The public bank-movement result may represent this terminal business state as:

```json
{
  "isSuccess": true,
  "code": "BANKDEPOSIT_FAILED",
  "message": "BankDeposit state is Failed.",
  "data": {
    "operation": "BankDeposit",
    "amount": 1000.00,
    "currency": "TRY",
    "state": "Failed",
    "wasReplay": false
  },
  "errors": []
}
```

```text
External bank balance: unchanged
Wallet available balance: unchanged
Ledger posting: none
Idempotency: terminal failure state
```

### 8. FAIL - Insufficient Wallet balance for transfer

```http
POST {{gateway}}/api/v1/transfers
Authorization: Bearer {{token}}
Idempotency-Key: fail-transfer-balance-001
Content-Type: application/json
```

```json
{
  "sourceWalletId": "11111111-aaaa-aaaa-aaaa-111111111111",
  "destinationWalletId": "22222222-bbbb-bbbb-bbbb-222222222222",
  "amount": 5000.00
}
```

If source available balance is 500 TRY:

```json
{
  "isSuccess": false,
  "code": "INSUFFICIENT_BALANCE",
  "message": "The source wallet has insufficient available balance.",
  "data": null,
  "errors": []
}
```

HTTP: `409 Conflict`.

The atomic posting transaction rolls back; no partial balance or Ledger change remains.

### 9. FAIL - Fraud provider unavailable

For Transfer/Purchase, external FakeFraud is mandatory unless internal fraud already denied the operation. Network/timeout/invalid response causes fail-closed behavior:

```json
{
  "isSuccess": false,
  "code": "FRAUD_DEPENDENCY_UNAVAILABLE",
  "message": "The required fraud service is temporarily unavailable.",
  "data": null,
  "errors": []
}
```

HTTP: `503 Service Unavailable`.

```text
Wallet balance: unchanged
Ledger: unchanged
Financial posting: never starts
```

### 10. FAIL - Same Idempotency-Key reused with different request

First request:

```json
{
  "destinationWalletId": "22222222-bbbb-bbbb-bbbb-222222222222",
  "amount": 100.00
}
```

Second request with the same key:

```json
{
  "destinationWalletId": "33333333-cccc-cccc-cccc-333333333333",
  "amount": 200.00
}
```

Response:

```json
{
  "isSuccess": false,
  "code": "IDEMPOTENCY_CONFLICT",
  "message": "The Idempotency-Key was already used with a different transfer request.",
  "data": null,
  "errors": []
}
```

HTTP: `409 Conflict`.

This prevents duplicate protection from replaying the wrong request.

### 11. FAIL - Notification provider down after money completed

Assume BankDeposit or Purchase already committed money and Outbox in the same transaction. The FinancialTransaction is now `Completed`.

If FakeCommunication returns 503/timeout:

```text
FinancialTransaction: remains Completed
Wallet/Ledger: not rolled back
Outbox: not Processed
Outbox: rescheduled with bounded backoff
```

Worker failure codes include:

```text
COMMUNICATION_UNAVAILABLE
OUTBOX_DISPATCH_ERROR
OUTBOX_RECIPIENT_UNAVAILABLE
OUTBOX_INVALID_PAYLOAD
```

The previously returned financial success is never reversed because communication failed.

### 12. FAIL - Rate limit / method / content type

Rate limit:

```json
{
  "isSuccess": false,
  "code": "RATE_LIMITED",
  "message": "Too many requests.",
  "data": null,
  "errors": []
}
```

TRACE or CONNECT:

```text
405 METHOD_NOT_ALLOWED
```

POST/PUT/PATCH with a non-JSON body:

```text
415 UNSUPPORTED_MEDIA_TYPE
```

### 13. Failure behavior summary

| Failure | Money changes? | Ledger written? | Retry |
|---|---:|---:|---|
| Gateway unauthorized | No | No | With new JWT |
| Invalid OTP/login | No | No | Per security policy |
| Bank-account provider timeout | No | No | Same request key |
| BankDeposit transient provider error | Not yet | Not yet | Background |
| External-bank insufficient balance | No | No | Terminal failure |
| Transfer insufficient balance | No | No | New request after funding |
| Fraud unavailable | No | No | After dependency recovery |
| Idempotency conflict | No | No | Correct new key/request |
| Notification unavailable after commit | Already completed | Yes | Outbox retry |
