# İlk Çalıştırma Happy Path: Kayıttan Para Aktarımına / First Run Happy Path: Registration to Money Transfer

## Türkçe

Bu doküman projeyi ilk kez gören biri için **tam public happy path** akışıdır. Normal client yalnız Gateway'i çağırır.

```text
{{gateway}} = http://localhost:8080
```

FinWallet.Api ve fake provider servisleri normal client tarafından doğrudan çağrılmaz.

### Değişkenler
```text
{{customerA}} Customer A ID
{{customerB}} Customer B ID
{{tokenA}}    Customer A JWT
{{tokenB}}    Customer B JWT
{{walletA}}   Customer A TRY Wallet ID
{{walletB}}   Customer B TRY Wallet ID
{{bankA}}     Customer A BankAccount ID
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
Beklenen: HTTP 202. `customerId` -> `customerA`.

OTP public response'a dönmez. FakeCommunication simulated SMS olarak alır; local test/debug dışında OTP leak endpoint oluşturulmaz.

### 2. OTP verify
```http
POST {{gateway}}/api/v1/auth/registration/verify
Content-Type: application/json
```
```json
{
  "customerId": "{{customerA}}",
  "code": "<local-simulated-otp>"
}
```
Beklenen: HTTP 200.

### 3. Login
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
`accessToken` -> `tokenA`.

### 4. TRY wallet oluştur
```http
POST {{gateway}}/api/v1/wallets
Authorization: Bearer {{tokenA}}
Content-Type: application/json
```
```json
{ "currency": "TRY" }
```
`walletId` -> `walletA`. Yeni wallet 0 available / 0 blocked balance ile başlar.

### 5. External bank account aç
```http
POST {{gateway}}/api/v1/bank-accounts
Authorization: Bearer {{tokenA}}
Content-Type: application/json
```
```json
{ "walletId": "{{walletA}}" }
```
Provider pending ise HTTP 202 olabilir. Tamamlandığında response içindeki internal `bankAccountId` -> `bankA`.

### 6. Bank -> Wallet deposit ile wallet fonla
```http
POST {{gateway}}/api/v1/bank-movements/deposits
Authorization: Bearer {{tokenA}}
Idempotency-Key: demo-deposit-a-0001
Content-Type: application/json
X-Correlation-Id: demo-deposit-a
```
```json
{
  "bankAccountId": "{{bankA}}",
  "amount": 1000.00
}
```
Beklenen:
- provider tamamladıysa HTTP 200 `Completed`;
- provider pending ise HTTP 202 `Pending`;
- aynı key+payload replay ikinci para hareketi üretmez.

Bu akış FakeBank açısından external account debit/withdrawal; FinWallet açısından `BankDeposit` muhasebesidir.

### 7. Customer B register + verify + login + TRY wallet
Adım 1-4'ü farklı phone/email/device ile tekrarla. Örnek phone: `+905322222222`.

Sonuç:
```text
{{tokenB}}
{{walletB}}
```

### 8. Wallet transfer
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

Akış:
```text
completed replay check
-> durable session + server-side risk signals
-> internal fraud
-> FakeFraud via Gateway
-> durable FraudEvent
-> Allow/Approved
-> single MSSQL financial transaction
   -> balances
   -> FinancialTransaction
   -> balanced LedgerJournal/Entries
   -> Idempotency
   -> Outbox
```

Beklenen success: HTTP 200 ve `WALLET_TRANSFER_COMPLETED`.

Fraud `Review` dönerse HTTP 202; para hareket etmez. Internal fraud-review endpoint'i approve ettikten sonra **aynı request + aynı Idempotency-Key** tekrar gönderilerek işlem devam ettirilir.

### 9. Safe replay
Adım 8'deki request'i aynı key ile tekrar gönder.

Beklenen:
- aynı transaction ID;
- ikinci debit/credit yok;
- ikinci ledger journal yok;
- completed replay için ikinci fraud evaluation yok;
- `wasReplay=true`.

Aynı key farklı amount/destination ile gönderilirse HTTP 409 conflict.

### 10. Transaction history
```http
GET {{gateway}}/api/v1/transactions?take=50
Authorization: Bearer {{tokenA}}
```
Newest-first keyset pagination kullanır. Sonraki sayfa için önceki sayfanın son `transactionId` değeri `beforeTransactionId` olarak gönderilebilir.

### 11. Wallet -> Bank withdrawal
```http
POST {{gateway}}/api/v1/bank-movements/withdrawals
Authorization: Bearer {{tokenA}}
Idempotency-Key: demo-withdraw-a-0001
Content-Type: application/json
```
```json
{
  "bankAccountId": "{{bankA}}",
  "amount": 100.00
}
```
FakeCutoff sonucu:
- hemen işlenebilir -> Pending/Completed;
- cutoff sonrası -> HTTP 202 `Scheduled` ve processing/settlement date.

Withdrawal hazırlanırken fon available -> blocked taşınır. Provider terminal failure verirse blok geri açılır. Provider success sonrası blocked funds settle edilir ve ledger finalize olur.

### 12. Purchase + campaign
```http
POST {{gateway}}/api/v1/purchases
Authorization: Bearer {{tokenA}}
Idempotency-Key: demo-purchase-a-0001
Content-Type: application/json
```
```json
{
  "walletId": "{{walletA}}",
  "merchantId": "<active-merchant-id>",
  "amount": 100.00
}
```
Purchase fraud kontrolünden geçer, FakeCampaign eligibility/discount/sponsor hesaplar, FinWallet customer/merchant/platform ekonomik etkilerini balanced ledger'a yazar.

### 13. Refund
```http
POST {{gateway}}/api/v1/transactions/<purchase-transaction-id>/refund
Authorization: Bearer {{tokenA}}
Idempotency-Key: demo-refund-0001
```
Original purchase geçmişi overwrite edilmez; yeni Refund transaction + opposite ledger journal oluşturulur.

### 14. Internal wallet-transfer reversal
```http
POST {{gateway}}/api/v1/transactions/<wallet-transfer-id>/reversal
Authorization: Bearer {{tokenA}}
Idempotency-Key: demo-reversal-0001
```
Yalnız completed internal WalletTransfer için güvenlidir. External-bank movement bu endpoint ile doğrudan reverse edilmez; provider compensation gerekir.

### Gateway güvenlik beklentileri
| Request | Beklenen |
|---|---|
| Register/verify/login/refresh JWT olmadan | Geçer |
| Protected public API JWT olmadan | Gateway 401 |
| `/providers/*` internal key olmadan | Gateway reddeder |
| `/api/v1/internal/*` internal service key olmadan | Gateway reddeder |
| Backend/provider direct call downstream key olmadan | Destination reddeder |
| Rate/body/header limit aşımı | Business işlemden önce reddedilir |

### Swagger
Local Docker stack'te normal business trafik Gateway'den geçer. Production overlay Swagger'ı kapatır.

---

## English

This document is the **complete public happy path** for a developer seeing the project for the first time. Normal clients call only the Gateway.

```text
{{gateway}} = http://localhost:8080
```

FinWallet.Api and fake-provider services are not called directly by normal clients.

### Variables
```text
{{customerA}} Customer A ID
{{customerB}} Customer B ID
{{tokenA}}    Customer A JWT
{{tokenB}}    Customer B JWT
{{walletA}}   Customer A TRY Wallet ID
{{walletB}}   Customer B TRY Wallet ID
{{bankA}}     Customer A BankAccount ID
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
Expected: HTTP 202. Save `customerId` as `customerA`.

OTP is never returned by the public response. FakeCommunication receives it as a simulated SMS; no public OTP-leak endpoint should exist.

### 2. Verify OTP
```http
POST {{gateway}}/api/v1/auth/registration/verify
Content-Type: application/json
```
```json
{
  "customerId": "{{customerA}}",
  "code": "<local-simulated-otp>"
}
```
Expected: HTTP 200.

### 3. Login
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
Save `accessToken` as `tokenA`.

### 4. Create TRY wallet
```http
POST {{gateway}}/api/v1/wallets
Authorization: Bearer {{tokenA}}
Content-Type: application/json
```
```json
{ "currency": "TRY" }
```
Save `walletId` as `walletA`. A new wallet starts with zero available and blocked balances.

### 5. Open external bank account
```http
POST {{gateway}}/api/v1/bank-accounts
Authorization: Bearer {{tokenA}}
Content-Type: application/json
```
```json
{ "walletId": "{{walletA}}" }
```
Provider pending may return HTTP 202. Once completed, save the internal `bankAccountId` as `bankA`.

### 6. Fund wallet with Bank -> Wallet deposit
```http
POST {{gateway}}/api/v1/bank-movements/deposits
Authorization: Bearer {{tokenA}}
Idempotency-Key: demo-deposit-a-0001
Content-Type: application/json
X-Correlation-Id: demo-deposit-a
```
```json
{
  "bankAccountId": "{{bankA}}",
  "amount": 1000.00
}
```
Expected:
- HTTP 200 `Completed` if provider completes immediately;
- HTTP 202 `Pending` if provider remains pending;
- replay with the same key+payload creates no second money movement.

From FakeBank's perspective this debits/withdraws from the external account; from FinWallet's perspective it is a `BankDeposit`.

### 7. Register + verify + login Customer B and create TRY wallet
Repeat steps 1-4 with different phone/email/device. Example phone: `+905322222222`.

Result:
```text
{{tokenB}}
{{walletB}}
```

### 8. Wallet transfer
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

Flow:
```text
completed replay check
-> durable session + server-side risk signals
-> internal fraud
-> FakeFraud via Gateway
-> durable FraudEvent
-> Allow/Approved
-> single MSSQL financial transaction
   -> balances
   -> FinancialTransaction
   -> balanced LedgerJournal/Entries
   -> Idempotency
   -> Outbox
```

Expected success: HTTP 200 and `WALLET_TRANSFER_COMPLETED`.

If fraud returns `Review`, HTTP 202 is returned and no money moves. After the internal fraud-review endpoint approves the event, resend the **same request with the same Idempotency-Key** to continue.

### 9. Safe replay
Send step 8 again with the same key.

Expected:
- same transaction ID;
- no second debit/credit;
- no second ledger journal;
- no second fraud evaluation for a completed replay;
- `wasReplay=true`.

Same key with different amount/destination returns HTTP 409 conflict.

### 10. Transaction history
```http
GET {{gateway}}/api/v1/transactions?take=50
Authorization: Bearer {{tokenA}}
```
Uses newest-first keyset pagination. Pass the last `transactionId` from the previous page as `beforeTransactionId` for the next page.

### 11. Wallet -> Bank withdrawal
```http
POST {{gateway}}/api/v1/bank-movements/withdrawals
Authorization: Bearer {{tokenA}}
Idempotency-Key: demo-withdraw-a-0001
Content-Type: application/json
```
```json
{
  "bankAccountId": "{{bankA}}",
  "amount": 100.00
}
```
FakeCutoff may return:
- process now -> Pending/Completed;
- after cutoff -> HTTP 202 `Scheduled` with processing/settlement dates.

Preparation moves funds from available -> blocked. A terminal provider failure releases the reservation. Provider success settles blocked funds and finalizes the ledger.

### 12. Purchase + campaign
```http
POST {{gateway}}/api/v1/purchases
Authorization: Bearer {{tokenA}}
Idempotency-Key: demo-purchase-a-0001
Content-Type: application/json
```
```json
{
  "walletId": "{{walletA}}",
  "merchantId": "<active-merchant-id>",
  "amount": 100.00
}
```
Purchase passes fraud controls, FakeCampaign computes eligibility/discount/sponsor, and FinWallet posts customer/merchant/platform economic effects to a balanced ledger.

### 13. Refund
```http
POST {{gateway}}/api/v1/transactions/<purchase-transaction-id>/refund
Authorization: Bearer {{tokenA}}
Idempotency-Key: demo-refund-0001
```
The original purchase is never overwritten; a new Refund transaction plus opposite ledger journal is created.

### 14. Internal wallet-transfer reversal
```http
POST {{gateway}}/api/v1/transactions/<wallet-transfer-id>/reversal
Authorization: Bearer {{tokenA}}
Idempotency-Key: demo-reversal-0001
```
Safe only for a completed internal WalletTransfer. External-bank movements are not directly reversed through this endpoint; provider compensation is required.

### Gateway security expectations
| Request | Expected |
|---|---|
| Register/verify/login/refresh without JWT | Forwarded |
| Protected public API without JWT | Gateway 401 |
| `/providers/*` without internal key | Rejected |
| `/api/v1/internal/*` without internal-service key | Rejected |
| Direct backend/provider call without downstream key | Destination rejects |
| Rate/body/header limit exceeded | Rejected before business processing |

### Swagger
Normal business traffic in the local Docker stack goes through Gateway. The production overlay disables Swagger.
