# İlk Çalıştırma Happy Path: Kayıttan Wallet Transfer'a / First Run Happy Path: Registration to Wallet Transfer

## Türkçe

Bu doküman projeyi ilk kez gören biri içindir. Normal client çağrılarının tamamı YARP Gateway'e gider.

### Base URL
```text
{{gateway}} = http://localhost:8080
```
FinWallet.Api `:8081` normal client flow için doğrudan çağrılmamalıdır.

Örnek değişkenler:
```text
{{tokenA}}  Customer A JWT
{{tokenB}}  Customer B JWT
{{walletA}} Customer A TRY wallet ID
{{walletB}} Customer B TRY wallet ID
```

### 1. Customer A register
```http
POST {{gateway}}/api/v1/auth/register
Content-Type: application/json
X-Correlation-Id: demo-register-a
```
```json
{
  "countryCode": "TR",
  "phoneNumber": "+905321111111",
  "email": "customer.a@example.test",
  "password": "Example-Password-A-123!"
}
```
Beklenen: HTTP 202 ve registration pending response. `customerId` saklanır.

**OTP notu:** FinWallet registration response içinde OTP döndürmez. FakeCommunication raw OTP'yi simulated SMS body olarak alır. Local test/debug instrumentation ile okunmalıdır; public API'ye OTP leak endpoint eklenmemelidir.

### 2. Customer A OTP verify
```http
POST {{gateway}}/api/v1/auth/registration/verify
Content-Type: application/json
```
```json
{
  "customerId": "<customer-a-id>",
  "code": "123456"
}
```
Beklenen: HTTP 200 ve registration verified.

### 3. Customer A login
```http
POST {{gateway}}/api/v1/auth/login
Content-Type: application/json
```
```json
{
  "phoneNumber": "+905321111111",
  "password": "Example-Password-A-123!",
  "deviceId": "demo-device-a"
}
```
Response içindeki `accessToken` -> `tokenA` olarak saklanır. Token loglanmaz.

### 4. Customer A TRY wallet create
```http
POST {{gateway}}/api/v1/wallets
Authorization: Bearer {{tokenA}}
Content-Type: application/json
```
```json
{ "currency": "TRY" }
```
İlk çağrı HTTP 201 `WALLET_CREATED`; aynı customer/currency tekrarında HTTP 200 `WALLET_EXISTS`. `walletId` -> `walletA`. Başlangıç available/blocked balance = 0.

### 5. Customer A external bank account
Wallet-to-wallet transfer için zorunlu değildir ama intended onboarding story'nin parçasıdır.
```http
POST {{gateway}}/api/v1/bank-accounts
Authorization: Bearer {{tokenA}}
Content-Type: application/json
```
```json
{ "walletId": "{{walletA}}" }
```
Provider pending ise HTTP 202 dönebilir. Retry aynı durable BankAccount ID'den deterministic provider request key ürettiği için duplicate account açmamalıdır.

### 6. Customer B register + verify + login
Aynı 1-3 adımlarını farklı phone/email/device ile tekrarla. Örnek phone: `+905322222222`. Access token -> `tokenB`.

### 7. Customer B TRY wallet
```http
POST {{gateway}}/api/v1/wallets
Authorization: Bearer {{tokenB}}
Content-Type: application/json
```
```json
{ "currency": "TRY" }
```
Wallet ID -> `walletB`.

### 8. Wallet list kontrolü
```http
GET {{gateway}}/api/v1/wallets
Authorization: Bearer {{tokenA}}
```
JWT yok/geçersiz ise Gateway request'i backend'e göndermeden reddeder.

### 9. Funding prerequisite — mevcut gap
Yeni wallet 0 bakiye ile açılır. Public BankDeposit/funding endpoint'i **henüz yoktur**. Dolayısıyla yalnız public API ile `register -> fund -> successful transfer` tamamlanamaz.

Şunu yapma:
```sql
UPDATE Wallets SET AvailableBalance = ...
```
Bu Ledger'ı atlayarak para yaratır ve reconciliation'ı bozar.

Transfer demo/test için controlled integration fixture aynı atomik transaction içinde balance + FinancialTransaction + balanced LedgerJournal/Entries oluşturmalıdır. Sıradaki gerçek feature BankDeposit olmalıdır.

### 10. Wallet transfer
`walletA` geçerli şekilde fonlandıktan sonra:
```http
POST {{gateway}}/api/v1/transfers
Authorization: Bearer {{tokenA}}
Idempotency-Key: demo-transfer-a-to-b-0001
Content-Type: application/json
X-Correlation-Id: demo-transfer-a-to-b
```
```json
{
  "sourceWalletId": "{{walletA}}",
  "destinationWalletId": "{{walletB}}",
  "amount": 125.50
}
```
Beklenen success: HTTP 200 `WALLET_TRANSFER_COMPLETED`, immutable `transactionId`, amount/currency/completedAt ve `wasReplay=false`.

### 11. Safe replay
Aynı request + aynı `Idempotency-Key` tekrar gönderilir.
Beklenen:
- ikinci money movement yok;
- ikinci ledger posting yok;
- completed replay için ikinci fraud evaluation yok;
- same transaction ID;
- `WALLET_TRANSFER_REPLAYED`;
- `wasReplay=true`.

Aynı key farklı amount/destination ile gönderilirse conflict olur.

### Gateway beklentileri
| Request | Beklenen |
|---|---|
| Register/verify/login/refresh JWT olmadan | Gateway geçirir |
| Wallet/bank-account/transfer JWT olmadan | Gateway reddeder |
| `/providers/*` internal key olmadan | Gateway reddeder |
| Backend business endpoint downstream key olmadan | Destination reddeder |
| Rate/body/header limit aşımı | Business processing öncesi reddedilir |

### Swagger
```text
http://localhost:8080/swagger  Gateway
http://localhost:8081/swagger  FinWallet.Api
http://localhost:8082/swagger  FakeBank
http://localhost:8083/swagger  FakeFraud
```
Normal business çağrısı yine Gateway `:8080` üzerinden yapılır.

---

## English

This document is for someone seeing the project for the first time. All normal client calls go through YARP Gateway.

### Base URL
```text
{{gateway}} = http://localhost:8080
```
FinWallet.Api on `:8081` should not be called directly for normal client flows.

Example variables:
```text
{{tokenA}}  Customer A JWT
{{tokenB}}  Customer B JWT
{{walletA}} Customer A TRY wallet ID
{{walletB}} Customer B TRY wallet ID
```

### 1. Register Customer A
```http
POST {{gateway}}/api/v1/auth/register
Content-Type: application/json
X-Correlation-Id: demo-register-a
```
```json
{
  "countryCode": "TR",
  "phoneNumber": "+905321111111",
  "email": "customer.a@example.test",
  "password": "Example-Password-A-123!"
}
```
Expected: HTTP 202 with registration pending response. Save `customerId`.

**OTP note:** FinWallet never returns OTP in the registration response. FakeCommunication receives the raw OTP as the simulated SMS body. Read it only through local test/debug instrumentation; do not add a public OTP-leak endpoint.

### 2. Verify Customer A OTP
```http
POST {{gateway}}/api/v1/auth/registration/verify
Content-Type: application/json
```
```json
{
  "customerId": "<customer-a-id>",
  "code": "123456"
}
```
Expected: HTTP 200 and registration verified.

### 3. Login Customer A
```http
POST {{gateway}}/api/v1/auth/login
Content-Type: application/json
```
```json
{
  "phoneNumber": "+905321111111",
  "password": "Example-Password-A-123!",
  "deviceId": "demo-device-a"
}
```
Store response `accessToken` as `tokenA`. Never log the token.

### 4. Create Customer A TRY wallet
```http
POST {{gateway}}/api/v1/wallets
Authorization: Bearer {{tokenA}}
Content-Type: application/json
```
```json
{ "currency": "TRY" }
```
First call: HTTP 201 `WALLET_CREATED`; repeat for the same customer/currency: HTTP 200 `WALLET_EXISTS`. Store `walletId` as `walletA`. Initial available/blocked balances are zero.

### 5. Open Customer A external bank account
Not required for an internal wallet transfer but part of the intended onboarding story.
```http
POST {{gateway}}/api/v1/bank-accounts
Authorization: Bearer {{tokenA}}
Content-Type: application/json
```
```json
{ "walletId": "{{walletA}}" }
```
Provider pending may return HTTP 202. Retrying must not create a duplicate provider account because the durable BankAccount ID produces a deterministic provider request key.

### 6. Register + verify + login Customer B
Repeat steps 1-3 with different phone/email/device. Example phone: `+905322222222`. Store access token as `tokenB`.

### 7. Create Customer B TRY wallet
```http
POST {{gateway}}/api/v1/wallets
Authorization: Bearer {{tokenB}}
Content-Type: application/json
```
```json
{ "currency": "TRY" }
```
Store wallet ID as `walletB`.

### 8. Verify wallet list
```http
GET {{gateway}}/api/v1/wallets
Authorization: Bearer {{tokenA}}
```
If JWT is missing/invalid, Gateway rejects the request before forwarding it to the backend.

### 9. Funding prerequisite — current gap
A new wallet starts at zero balance. A public BankDeposit/funding endpoint **does not exist yet**, so public APIs alone cannot complete `register -> fund -> successful transfer`.

Do not do this:
```sql
UPDATE Wallets SET AvailableBalance = ...
```
It creates money outside the Ledger and breaks reconciliation.

For transfer demos/tests, a controlled integration fixture must atomically create balance + FinancialTransaction + balanced LedgerJournal/Entries. BankDeposit should be the next real feature.

### 10. Wallet transfer
After `walletA` has been validly funded:
```http
POST {{gateway}}/api/v1/transfers
Authorization: Bearer {{tokenA}}
Idempotency-Key: demo-transfer-a-to-b-0001
Content-Type: application/json
X-Correlation-Id: demo-transfer-a-to-b
```
```json
{
  "sourceWalletId": "{{walletA}}",
  "destinationWalletId": "{{walletB}}",
  "amount": 125.50
}
```
Expected success: HTTP 200 `WALLET_TRANSFER_COMPLETED`, immutable `transactionId`, amount/currency/completedAt and `wasReplay=false`.

### 11. Safe replay
Send the same request with the same `Idempotency-Key` again.
Expected:
- no second money movement;
- no second ledger posting;
- no second fraud evaluation for a completed replay;
- same transaction ID;
- `WALLET_TRANSFER_REPLAYED`;
- `wasReplay=true`.

Using the same key with a different amount/destination returns a conflict.

### Gateway expectations
| Request | Expected behavior |
|---|---|
| Register/verify/login/refresh without JWT | Gateway forwards |
| Wallet/bank-account/transfer without JWT | Gateway rejects |
| `/providers/*` without internal key | Gateway rejects |
| Backend business endpoint without downstream key | Destination rejects |
| Rate/body/header limits exceeded | Rejected before business processing |

### Swagger
```text
http://localhost:8080/swagger  Gateway
http://localhost:8081/swagger  FinWallet.Api
http://localhost:8082/swagger  FakeBank
http://localhost:8083/swagger  FakeFraud
```
Normal business calls still go through Gateway `:8080`.
