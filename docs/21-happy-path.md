# FinWallet Başarılı Akış - Register'dan Bankadan Wallet'a Para Yüklemeye / FinWallet Happy Path - Registration to Bank-to-Wallet Funding

## Türkçe

Bu doküman FinWallet v1 içinde yeni bir müşterinin kayıt olmasından dış banka hesabındaki paranın dijital wallet'a başarıyla aktarılmasına kadar olan gerçek happy-path akışını gösterir. Normal müşteri çağrılarının tamamı YARP Gateway üzerinden yapılır.

### 0. Base URL ve örnek değişkenler

```text
{{gateway}} = http://localhost:8080
{{token}} = login response accessToken
{{customerId}} = register response customerId
{{walletId}} = wallet create response walletId
{{bankAccountId}} = FinWallet internal BankAccount id
{{externalAccountId}} = FakeBank provider account id
{{internalKey}} = local development Gateway InternalServiceKey
```

Normal client doğrudan FinWallet.Api veya Fake provider portlarını çağırmaz. Client için tek giriş noktası `{{gateway}}` adresidir.

### 1. CLIENT - Register

```http
POST {{gateway}}/api/v1/auth/register
Content-Type: application/json
X-Correlation-Id: hp-register-001
```

```json
{
  "countryCode": "TR",
  "phoneNumber": "+905321111111",
  "email": "happy.path@example.test",
  "password": "Example-Password-123!"
}
```

Başarılı response:

```json
{
  "isSuccess": true,
  "code": "REGISTRATION_ACCEPTED",
  "message": "Registration accepted and verification is pending.",
  "data": {
    "customerId": "11111111-1111-1111-1111-111111111111",
    "otpExpiresAt": "2026-08-10T20:03:00+00:00"
  },
  "errors": []
}
```

FinWallet bu noktada Customer ve Credential state'ini MSSQL'e durable olarak yazar. Müşteri henüz Active değildir.

### 2. INTERNAL - OTP SMS gönderimi

Register handler DB transaction tamamlandıktan sonra FakeCommunication'a Gateway üzerinden gider.

```http
POST {{gateway}}/providers/communication/api/v1/communication/sms
X-Internal-Service-Key: {{internalKey}}
Content-Type: application/json
X-Correlation-Id: hp-register-001
```

Örnek internal request:

```json
{
  "recipient": "+905321111111",
  "messageType": "RegistrationOtp",
  "body": "FinWallet verification code: 123456",
  "correlationId": "hp-register-001"
}
```

FakeCommunication başarılı response:

```json
{
  "isSuccess": true,
  "code": "MESSAGE_ACCEPTED",
  "message": "Message accepted by fake provider.",
  "data": {
    "messageId": "22222222-2222-2222-2222-222222222222",
    "status": "Accepted",
    "acceptedAt": "2026-08-10T20:00:01+00:00"
  },
  "errors": []
}
```

Bu çağrı normal kullanıcının yaptığı bir API çağrısı değildir. OTP public register response içinde dönmez. Local testte örnek OTP simulator mesajından okunur.

### 3. CLIENT - OTP doğrulama

```http
POST {{gateway}}/api/v1/auth/registration/verify
Content-Type: application/json
X-Correlation-Id: hp-verify-001
```

```json
{
  "customerId": "11111111-1111-1111-1111-111111111111",
  "code": "123456"
}
```

Başarılı response:

```json
{
  "isSuccess": true,
  "code": "REGISTRATION_VERIFIED",
  "message": "Registration verification completed.",
  "data": null,
  "errors": []
}
```

Customer artık Active hale gelir.

### 4. CLIENT - Login

```http
POST {{gateway}}/api/v1/auth/login
Content-Type: application/json
X-Correlation-Id: hp-login-001
```

```json
{
  "phoneNumber": "+905321111111",
  "password": "Example-Password-123!",
  "deviceId": "happy-path-device-01"
}
```

Başarılı response:

```json
{
  "isSuccess": true,
  "code": "AUTHENTICATED",
  "message": "Authentication completed successfully.",
  "data": {
    "customerId": "11111111-1111-1111-1111-111111111111",
    "sessionId": "33333333-3333-3333-3333-333333333333",
    "accessToken": "<JWT>",
    "accessTokenExpiresAt": "2026-08-10T20:10:00+00:00",
    "refreshToken": "<OPAQUE_REFRESH_TOKEN>",
    "refreshTokenExpiresAt": "2026-08-24T20:00:00+00:00"
  },
  "errors": []
}
```

Bundan sonraki public finansal endpoint'lerde `Authorization: Bearer {{token}}` kullanılır.

### 5. CLIENT - TRY Wallet oluşturma

```http
POST {{gateway}}/api/v1/wallets
Authorization: Bearer {{token}}
Content-Type: application/json
X-Correlation-Id: hp-wallet-001
```

```json
{
  "currency": "TRY"
}
```

İlk çağrı başarılı response:

```json
{
  "isSuccess": true,
  "code": "WALLET_CREATED",
  "message": "Wallet created successfully.",
  "data": {
    "walletId": "44444444-4444-4444-4444-444444444444",
    "currency": "TRY",
    "availableBalance": 0.0,
    "blockedBalance": 0.0,
    "status": "Active",
    "createdAt": "2026-08-10T20:01:00+00:00"
  },
  "errors": []
}
```

Yeni wallet para yaratmaz; başlangıç bakiyesi sıfırdır.

### 6. CLIENT - FinWallet BankAccount açma

```http
POST {{gateway}}/api/v1/bank-accounts
Authorization: Bearer {{token}}
Content-Type: application/json
X-Correlation-Id: hp-bank-account-001
```

```json
{
  "walletId": "44444444-4444-4444-4444-444444444444"
}
```

FinWallet önce MSSQL'de durable `Opening` BankAccount oluşturur. SQL transaction kapandıktan sonra FakeBank çağrılır.

### 7. INTERNAL - FakeBank external account açma

FinWallet Infrastructure adapter'ı aşağıdaki provider çağrısını Gateway üzerinden yapar:

```http
POST {{gateway}}/providers/bank/api/v1/bank/accounts
X-Internal-Service-Key: {{internalKey}}
Content-Type: application/json
X-Correlation-Id: hp-bank-account-001
```

Örnek provider request:

```json
{
  "externalCustomerReference": "11111111-1111-1111-1111-111111111111",
  "currency": "TRY",
  "requestKey": "bank-account-open:55555555555555555555555555555555"
}
```

Provider response:

```json
{
  "isSuccess": true,
  "code": "BANK_ACCOUNT_ACCEPTED",
  "message": "External bank account request accepted.",
  "data": {
    "accountId": "66666666-6666-6666-6666-666666666666",
    "iban": "FWTRY66666666666666666666666",
    "currency": "TRY",
    "status": 2
  },
  "errors": []
}
```

`status=2` FakeBank `Active` durumudur.

FinWallet public response:

```json
{
  "isSuccess": true,
  "code": "BANK_ACCOUNT_READY",
  "message": "Bank account state is available.",
  "data": {
    "bankAccountId": "55555555-5555-5555-5555-555555555555",
    "walletId": "44444444-4444-4444-4444-444444444444",
    "currency": "TRY",
    "externalAccountId": "66666666-6666-6666-6666-666666666666",
    "externalIban": "FWTRY66666666666666666666666",
    "status": "Active"
  },
  "errors": []
}
```

### 8. TEST ONLY - FakeBank hesabına başlangıç parası yükleme

FakeBank yeni hesabı `0 TRY` ile açar. Gerçek bankada bu bakiye zaten müşterinin mevcut hesabından gelir; simulator'da happy path test edebilmek için provider hesabı internal test endpoint'i ile fonlanır.

Bu çağrı normal müşteri API'si değildir.

```http
POST {{gateway}}/providers/bank/api/v1/bank/transactions
X-Internal-Service-Key: {{internalKey}}
Content-Type: application/json
X-Correlation-Id: hp-provider-seed-001
```

```json
{
  "accountId": "66666666-6666-6666-6666-666666666666",
  "amount": 5000.00,
  "currency": "TRY",
  "transactionType": 1,
  "requestKey": "test-seed-bank-account-001"
}
```

`transactionType=1` provider hesabına `Deposit`, yani credit uygular.

Başarılı provider response:

```json
{
  "isSuccess": true,
  "code": "BANK_TRANSACTION_ACCEPTED",
  "message": "External bank transaction request accepted.",
  "data": {
    "transactionId": "77777777-7777-7777-7777-777777777777",
    "status": 2,
    "accountBalance": 5000.00
  },
  "errors": []
}
```

### 9. CLIENT - Banka hesabından Digital Wallet'a 1.000 TRY aktar

```http
POST {{gateway}}/api/v1/bank-movements/deposits
Authorization: Bearer {{token}}
Idempotency-Key: hp-bank-to-wallet-0001
Content-Type: application/json
X-Correlation-Id: hp-bank-to-wallet-001
```

```json
{
  "bankAccountId": "55555555-5555-5555-5555-555555555555",
  "amount": 1000.00
}
```

Bu endpoint adı FinWallet açısından `BankDeposit`'tir: dış bankadan wallet'a para girişi yapılır.

### 10. INTERNAL - FakeBank hesabından 1.000 TRY debit

FinWallet, dış provider'a `Withdrawal` yönünde gider; çünkü para FakeBank hesabından çıkar.

```http
POST {{gateway}}/providers/bank/api/v1/bank/transactions
X-Internal-Service-Key: {{internalKey}}
Content-Type: application/json
X-Correlation-Id: hp-bank-to-wallet-001
```

Örnek internal request:

```json
{
  "accountId": "66666666-6666-6666-6666-666666666666",
  "amount": 1000.00,
  "currency": "TRY",
  "transactionType": 2,
  "requestKey": "88888888888888888888888888888888"
}
```

`transactionType=2` FakeBank hesabından `Withdrawal`, yani debit uygular.

Provider response:

```json
{
  "isSuccess": true,
  "code": "BANK_TRANSACTION_ACCEPTED",
  "message": "External bank transaction request accepted.",
  "data": {
    "transactionId": "99999999-9999-9999-9999-999999999999",
    "status": 2,
    "accountBalance": 4000.00
  },
  "errors": []
}
```

### 11. FinWallet atomic financial commit

Provider `Completed` döndükten sonra FinWallet tek MSSQL transaction içinde şu state'i commit eder:

```text
Wallet.AvailableBalance: 0 -> 1000 TRY
FinancialTransaction: BankDeposit / Completed
IdempotencyRecord: Completed
Outbox: BANK_MOVEMENT_COMPLETED
```

Double-entry ledger posting:

```text
Debit   BANK-SETTLEMENT:TRY             1000 TRY
Credit  WALLET-LIABILITY:<walletId>     1000 TRY
```

Debit ve Credit eşit değilse transaction commit edilmez.

CLIENT response:

```json
{
  "isSuccess": true,
  "code": "BANKDEPOSIT_COMPLETED",
  "message": "BankDeposit state is Completed.",
  "data": {
    "transactionId": "88888888-8888-8888-8888-888888888888",
    "bankAccountId": "55555555-5555-5555-5555-555555555555",
    "externalTransactionId": "99999999-9999-9999-9999-999999999999",
    "operation": "BankDeposit",
    "amount": 1000.00,
    "currency": "TRY",
    "state": "Completed",
    "processingDate": "2026-08-10",
    "settlementDate": "2026-08-10",
    "wasReplay": false
  },
  "errors": []
}
```

### 12. INTERNAL - Notification Outbox worker

Para commit edildikten sonra Outbox worker SMS gönderir. Communication hatası finansal işlemi geri almaz.

```http
POST {{gateway}}/providers/communication/api/v1/communication/sms
X-Internal-Service-Key: {{internalKey}}
Content-Type: application/json
```

Örnek mesaj:

```json
{
  "recipient": "+905321111111",
  "messageType": "BANK_MOVEMENT_COMPLETED",
  "body": "FinWallet notification: BANK_MOVEMENT_COMPLETED. Reference: 88888888888888888888888888888888.",
  "correlationId": "hp-bank-to-wallet-001"
}
```

Provider başarılı olduğunda Outbox kaydı Processed olur.

### 13. CLIENT - Wallet bakiyesini doğrula

```http
GET {{gateway}}/api/v1/wallets
Authorization: Bearer {{token}}
X-Correlation-Id: hp-wallet-check-001
```

Beklenen ilgili wallet:

```json
{
  "walletId": "44444444-4444-4444-4444-444444444444",
  "currency": "TRY",
  "availableBalance": 1000.00,
  "blockedBalance": 0.00,
  "status": "Active"
}
```

### 14. CLIENT - Transaction history ile doğrula

```http
GET {{gateway}}/api/v1/transactions?take=20
Authorization: Bearer {{token}}
```

History içinde `BankDeposit / Completed / 1000 TRY`, internal BankAccountId ve external transaction reference görülür. Raw ledger entry, password, token veya hassas provider payload'u public history'de dönmez.

### Fraud notu

Bu BankDeposit happy path'inde fraud provider çağrısı yoktur. Mevcut FinWallet v1 implementasyonunda internal + external fraud akışı WalletTransfer ve Purchase öncesinde çalışır. Fraud süreci `22-fraud-path` dokümanında gösterilmiştir.

---

## English

This document shows the actual FinWallet v1 happy path from new-customer registration through moving money from the customer's external bank account into the digital wallet. All normal customer calls go through YARP Gateway.

### 0. Base URL and sample variables

```text
{{gateway}} = http://localhost:8080
{{token}} = login response accessToken
{{customerId}} = register response customerId
{{walletId}} = wallet create response walletId
{{bankAccountId}} = FinWallet internal BankAccount id
{{externalAccountId}} = FakeBank provider account id
{{internalKey}} = local development Gateway InternalServiceKey
```

A normal client does not call FinWallet.Api or fake-provider ports directly. The single client entry point is `{{gateway}}`.

### 1. CLIENT - Register

```http
POST {{gateway}}/api/v1/auth/register
Content-Type: application/json
X-Correlation-Id: hp-register-001
```

```json
{
  "countryCode": "TR",
  "phoneNumber": "+905321111111",
  "email": "happy.path@example.test",
  "password": "Example-Password-123!"
}
```

Successful response:

```json
{
  "isSuccess": true,
  "code": "REGISTRATION_ACCEPTED",
  "message": "Registration accepted and verification is pending.",
  "data": {
    "customerId": "11111111-1111-1111-1111-111111111111",
    "otpExpiresAt": "2026-08-10T20:03:00+00:00"
  },
  "errors": []
}
```

FinWallet durably writes Customer and Credential state to MSSQL. The customer is not Active yet.

### 2. INTERNAL - OTP SMS delivery

After the registration DB transaction is complete, the handler calls FakeCommunication through Gateway.

```http
POST {{gateway}}/providers/communication/api/v1/communication/sms
X-Internal-Service-Key: {{internalKey}}
Content-Type: application/json
X-Correlation-Id: hp-register-001
```

Example internal request:

```json
{
  "recipient": "+905321111111",
  "messageType": "RegistrationOtp",
  "body": "FinWallet verification code: 123456",
  "correlationId": "hp-register-001"
}
```

FakeCommunication success response:

```json
{
  "isSuccess": true,
  "code": "MESSAGE_ACCEPTED",
  "message": "Message accepted by fake provider.",
  "data": {
    "messageId": "22222222-2222-2222-2222-222222222222",
    "status": "Accepted",
    "acceptedAt": "2026-08-10T20:00:01+00:00"
  },
  "errors": []
}
```

This is not a normal user-facing call. The OTP is never returned in the public register response. In local tests, the sample OTP is read from simulator instrumentation.

### 3. CLIENT - Verify OTP

```http
POST {{gateway}}/api/v1/auth/registration/verify
Content-Type: application/json
X-Correlation-Id: hp-verify-001
```

```json
{
  "customerId": "11111111-1111-1111-1111-111111111111",
  "code": "123456"
}
```

Successful response:

```json
{
  "isSuccess": true,
  "code": "REGISTRATION_VERIFIED",
  "message": "Registration verification completed.",
  "data": null,
  "errors": []
}
```

The Customer is now Active.

### 4. CLIENT - Login

```http
POST {{gateway}}/api/v1/auth/login
Content-Type: application/json
X-Correlation-Id: hp-login-001
```

```json
{
  "phoneNumber": "+905321111111",
  "password": "Example-Password-123!",
  "deviceId": "happy-path-device-01"
}
```

Successful response:

```json
{
  "isSuccess": true,
  "code": "AUTHENTICATED",
  "message": "Authentication completed successfully.",
  "data": {
    "customerId": "11111111-1111-1111-1111-111111111111",
    "sessionId": "33333333-3333-3333-3333-333333333333",
    "accessToken": "<JWT>",
    "accessTokenExpiresAt": "2026-08-10T20:10:00+00:00",
    "refreshToken": "<OPAQUE_REFRESH_TOKEN>",
    "refreshTokenExpiresAt": "2026-08-24T20:00:00+00:00"
  },
  "errors": []
}
```

Use `Authorization: Bearer {{token}}` for subsequent public financial endpoints.

### 5. CLIENT - Create TRY Wallet

```http
POST {{gateway}}/api/v1/wallets
Authorization: Bearer {{token}}
Content-Type: application/json
X-Correlation-Id: hp-wallet-001
```

```json
{
  "currency": "TRY"
}
```

First successful response:

```json
{
  "isSuccess": true,
  "code": "WALLET_CREATED",
  "message": "Wallet created successfully.",
  "data": {
    "walletId": "44444444-4444-4444-4444-444444444444",
    "currency": "TRY",
    "availableBalance": 0.0,
    "blockedBalance": 0.0,
    "status": "Active",
    "createdAt": "2026-08-10T20:01:00+00:00"
  },
  "errors": []
}
```

A new wallet never mints money; it starts at zero.

### 6. CLIENT - Open FinWallet BankAccount

```http
POST {{gateway}}/api/v1/bank-accounts
Authorization: Bearer {{token}}
Content-Type: application/json
X-Correlation-Id: hp-bank-account-001
```

```json
{
  "walletId": "44444444-4444-4444-4444-444444444444"
}
```

FinWallet first persists the durable internal `Opening` BankAccount in MSSQL. Only after the SQL transaction ends does it call FakeBank.

### 7. INTERNAL - Open FakeBank external account

```http
POST {{gateway}}/providers/bank/api/v1/bank/accounts
X-Internal-Service-Key: {{internalKey}}
Content-Type: application/json
X-Correlation-Id: hp-bank-account-001
```

Example provider request:

```json
{
  "externalCustomerReference": "11111111-1111-1111-1111-111111111111",
  "currency": "TRY",
  "requestKey": "bank-account-open:55555555555555555555555555555555"
}
```

Provider response:

```json
{
  "isSuccess": true,
  "code": "BANK_ACCOUNT_ACCEPTED",
  "message": "External bank account request accepted.",
  "data": {
    "accountId": "66666666-6666-6666-6666-666666666666",
    "iban": "FWTRY66666666666666666666666",
    "currency": "TRY",
    "status": 2
  },
  "errors": []
}
```

`status=2` means FakeBank `Active`.

FinWallet public response:

```json
{
  "isSuccess": true,
  "code": "BANK_ACCOUNT_READY",
  "message": "Bank account state is available.",
  "data": {
    "bankAccountId": "55555555-5555-5555-5555-555555555555",
    "walletId": "44444444-4444-4444-4444-444444444444",
    "currency": "TRY",
    "externalAccountId": "66666666-6666-6666-6666-666666666666",
    "externalIban": "FWTRY66666666666666666666666",
    "status": "Active"
  },
  "errors": []
}
```

### 8. TEST ONLY - Seed initial FakeBank money

A newly opened FakeBank account starts with `0 TRY`. In a real bank the customer account would already hold funds. For the simulator happy path, seed the provider account through the internal test route.

This is not a normal customer API.

```http
POST {{gateway}}/providers/bank/api/v1/bank/transactions
X-Internal-Service-Key: {{internalKey}}
Content-Type: application/json
X-Correlation-Id: hp-provider-seed-001
```

```json
{
  "accountId": "66666666-6666-6666-6666-666666666666",
  "amount": 5000.00,
  "currency": "TRY",
  "transactionType": 1,
  "requestKey": "test-seed-bank-account-001"
}
```

`transactionType=1` means provider-account `Deposit`, therefore credit.

Successful provider response:

```json
{
  "isSuccess": true,
  "code": "BANK_TRANSACTION_ACCEPTED",
  "message": "External bank transaction request accepted.",
  "data": {
    "transactionId": "77777777-7777-7777-7777-777777777777",
    "status": 2,
    "accountBalance": 5000.00
  },
  "errors": []
}
```

### 9. CLIENT - Move 1,000 TRY from bank account to Digital Wallet

```http
POST {{gateway}}/api/v1/bank-movements/deposits
Authorization: Bearer {{token}}
Idempotency-Key: hp-bank-to-wallet-0001
Content-Type: application/json
X-Correlation-Id: hp-bank-to-wallet-001
```

```json
{
  "bankAccountId": "55555555-5555-5555-5555-555555555555",
  "amount": 1000.00
}
```

The public operation is called `BankDeposit` from the FinWallet point of view: money enters the wallet from the external bank.

### 10. INTERNAL - Debit 1,000 TRY from FakeBank account

FinWallet calls the external provider using the `Withdrawal` direction because money leaves the FakeBank account.

```http
POST {{gateway}}/providers/bank/api/v1/bank/transactions
X-Internal-Service-Key: {{internalKey}}
Content-Type: application/json
X-Correlation-Id: hp-bank-to-wallet-001
```

Example internal request:

```json
{
  "accountId": "66666666-6666-6666-6666-666666666666",
  "amount": 1000.00,
  "currency": "TRY",
  "transactionType": 2,
  "requestKey": "88888888888888888888888888888888"
}
```

`transactionType=2` means FakeBank-account `Withdrawal`, therefore debit.

Provider response:

```json
{
  "isSuccess": true,
  "code": "BANK_TRANSACTION_ACCEPTED",
  "message": "External bank transaction request accepted.",
  "data": {
    "transactionId": "99999999-9999-9999-9999-999999999999",
    "status": 2,
    "accountBalance": 4000.00
  },
  "errors": []
}
```

### 11. FinWallet atomic financial commit

After the provider reports `Completed`, FinWallet commits the following state in one MSSQL transaction:

```text
Wallet.AvailableBalance: 0 -> 1000 TRY
FinancialTransaction: BankDeposit / Completed
IdempotencyRecord: Completed
Outbox: BANK_MOVEMENT_COMPLETED
```

Double-entry ledger posting:

```text
Debit   BANK-SETTLEMENT:TRY             1000 TRY
Credit  WALLET-LIABILITY:<walletId>     1000 TRY
```

If Debit and Credit are not equal, the transaction is not committed.

CLIENT response:

```json
{
  "isSuccess": true,
  "code": "BANKDEPOSIT_COMPLETED",
  "message": "BankDeposit state is Completed.",
  "data": {
    "transactionId": "88888888-8888-8888-8888-888888888888",
    "bankAccountId": "55555555-5555-5555-5555-555555555555",
    "externalTransactionId": "99999999-9999-9999-9999-999999999999",
    "operation": "BankDeposit",
    "amount": 1000.00,
    "currency": "TRY",
    "state": "Completed",
    "processingDate": "2026-08-10",
    "settlementDate": "2026-08-10",
    "wasReplay": false
  },
  "errors": []
}
```

### 12. INTERNAL - Notification Outbox worker

After the money commit, the Outbox worker sends the SMS. Communication failure never rolls back the financial transaction.

```http
POST {{gateway}}/providers/communication/api/v1/communication/sms
X-Internal-Service-Key: {{internalKey}}
Content-Type: application/json
```

Example message:

```json
{
  "recipient": "+905321111111",
  "messageType": "BANK_MOVEMENT_COMPLETED",
  "body": "FinWallet notification: BANK_MOVEMENT_COMPLETED. Reference: 88888888888888888888888888888888.",
  "correlationId": "hp-bank-to-wallet-001"
}
```

When the provider accepts it, the Outbox row becomes Processed.

### 13. CLIENT - Verify Wallet balance

```http
GET {{gateway}}/api/v1/wallets
Authorization: Bearer {{token}}
X-Correlation-Id: hp-wallet-check-001
```

Expected Wallet data:

```json
{
  "walletId": "44444444-4444-4444-4444-444444444444",
  "currency": "TRY",
  "availableBalance": 1000.00,
  "blockedBalance": 0.00,
  "status": "Active"
}
```

### 14. CLIENT - Verify Transaction history

```http
GET {{gateway}}/api/v1/transactions?take=20
Authorization: Bearer {{token}}
```

History contains the `BankDeposit / Completed / 1000 TRY` row together with the internal BankAccountId and external transaction reference. Raw ledger entries, passwords, tokens and sensitive provider payloads are not returned by the public history API.

### Fraud note

There is no fraud-provider call in this BankDeposit happy path. In the current FinWallet v1 implementation, internal + external fraud runs before WalletTransfer and Purchase. The fraud flow is documented in `22-fraud-path`.
