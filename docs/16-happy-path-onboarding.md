# First Run Happy Path: Registration to Wallet Transfer

This document is written for someone seeing FinWallet for the first time. All client calls go through YARP Gateway.

## Base URLs

Local development:

```text
Gateway:       http://localhost:8080
FinWallet.Api: http://localhost:8081   # do not call directly for normal client flows
```

Use only the Gateway URL in the steps below.

Example variables:

```text
{{gateway}} = http://localhost:8080
{{tokenA}}  = JWT returned for Customer A
{{tokenB}}  = JWT returned for Customer B
{{walletA}} = Customer A TRY wallet ID
{{walletB}} = Customer B TRY wallet ID
```

## 1. Register Customer A

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

Successful accepted response:

```json
{
  "isSuccess": true,
  "code": "REGISTRATION_ACCEPTED",
  "message": "Registration accepted and verification is pending.",
  "data": {
    "customerId": "11111111-1111-4111-8111-111111111111",
    "otpExpiresAt": "2026-08-09T19:45:00+00:00"
  },
  "errors": []
}
```

Save `customerId` as `customerAId`.

### Where is the OTP in local development?

FinWallet never returns the OTP in the registration response. FakeCommunication receives the raw code as the simulated SMS body. The simulator currently has no public OTP-read endpoint by design. During local development obtain the code from the simulator under debugger/test instrumentation rather than adding an OTP leak to the public API.

## 2. Verify Customer A

```http
POST {{gateway}}/api/v1/auth/registration/verify
Content-Type: application/json
X-Correlation-Id: demo-verify-a
```

```json
{
  "customerId": "11111111-1111-4111-8111-111111111111",
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

## 3. Login Customer A

```http
POST {{gateway}}/api/v1/auth/login
Content-Type: application/json
X-Correlation-Id: demo-login-a
```

```json
{
  "phoneNumber": "+905321111111",
  "password": "Example-Password-A-123!",
  "deviceId": "demo-device-a"
}
```

Successful response shape:

```json
{
  "isSuccess": true,
  "code": "AUTHENTICATED",
  "message": "Authentication completed successfully.",
  "data": {
    "customerId": "11111111-1111-4111-8111-111111111111",
    "sessionId": "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa",
    "accessToken": "<JWT>",
    "accessTokenExpiresAt": "2026-08-09T19:50:00+00:00",
    "refreshToken": "<OPAQUE_REFRESH_TOKEN>",
    "refreshTokenExpiresAt": "2026-08-23T19:40:00+00:00"
  },
  "errors": []
}
```

Store `data.accessToken` as `tokenA`. Do not log it.

## 4. Create Customer A TRY Wallet

```http
POST {{gateway}}/api/v1/wallets
Authorization: Bearer {{tokenA}}
Content-Type: application/json
X-Correlation-Id: demo-wallet-a
```

```json
{
  "currency": "TRY"
}
```

First creation returns HTTP 201 and code `WALLET_CREATED`. Calling the same customer/currency again returns the same durable wallet with code `WALLET_EXISTS` rather than creating a duplicate.

Example data:

```json
{
  "isSuccess": true,
  "code": "WALLET_CREATED",
  "message": "Wallet created successfully.",
  "data": {
    "walletId": "aaaaaaaa-1111-4111-8111-111111111111",
    "currency": "TRY",
    "availableBalance": 0.0000,
    "blockedBalance": 0.0000,
    "status": "Active"
  },
  "errors": []
}
```

Save `walletId` as `walletA`.

## 5. Open Customer A External Bank Account

This step is optional for an internal wallet-to-wallet transfer, but it is part of the intended customer onboarding/business story.

```http
POST {{gateway}}/api/v1/bank-accounts
Authorization: Bearer {{tokenA}}
Content-Type: application/json
X-Correlation-Id: demo-bank-account-a
```

```json
{
  "walletId": "aaaaaaaa-1111-4111-8111-111111111111"
}
```

A provider-pending opening returns HTTP 202:

```json
{
  "isSuccess": true,
  "code": "BANK_ACCOUNT_PENDING",
  "message": "Bank account opening is pending at the external provider.",
  "data": {
    "bankAccountId": "baaaaaaa-1111-4111-8111-111111111111",
    "walletId": "aaaaaaaa-1111-4111-8111-111111111111",
    "currency": "TRY",
    "externalAccountId": "eaaaaaaa-1111-4111-8111-111111111111",
    "externalIban": "<PROVIDER_ACCOUNT_NUMBER>",
    "status": "Opening"
  },
  "errors": []
}
```

Repeat the same request later if the account remains Opening. The durable internal BankAccount ID produces a deterministic provider request key, so a lost HTTP response does not create a second provider account.

## 6. Register, verify and login Customer B

Repeat steps 1-3 with a different phone/email/device.

Example registration request:

```json
{
  "countryCode": "TR",
  "phoneNumber": "+905322222222",
  "email": "customer.b@example.test",
  "password": "Example-Password-B-123!"
}
```

Store the returned access token as `tokenB`.

## 7. Create Customer B TRY Wallet

```http
POST {{gateway}}/api/v1/wallets
Authorization: Bearer {{tokenB}}
Content-Type: application/json
```

```json
{
  "currency": "TRY"
}
```

Save the returned wallet ID as `walletB`.

## 8. Verify wallet state

For either customer:

```http
GET {{gateway}}/api/v1/wallets
Authorization: Bearer {{tokenA}}
```

The gateway rejects this request before proxying when the JWT is missing/invalid.

## 9. Funding prerequisite — current implementation gap

A newly created wallet starts at zero balance. The current public FinWallet API does **not yet expose a BankDeposit/funding endpoint**.

Therefore a fully executable `register -> newly created wallet -> successful transfer` cannot currently be completed using public endpoints alone.

Do not solve this by manually running:

```sql
UPDATE Wallets SET AvailableBalance = ...
```

That would create money outside the double-entry ledger and invalidate reconciliation.

Until the BankDeposit flow is implemented, a successful transfer test requires a controlled integration fixture that creates a balanced funding FinancialTransaction + LedgerJournal/Entries + wallet balance in one atomic operation. That fixture should live in the integration-test environment, not in the production API.

The next financial feature should make this step a real bank-deposit endpoint.

## 10. Execute wallet-to-wallet transfer

Assuming `walletA` has been validly funded and both wallets are Active/TRY:

```http
POST {{gateway}}/api/v1/transfers
Authorization: Bearer {{tokenA}}
Idempotency-Key: demo-transfer-a-to-b-0001
Content-Type: application/json
X-Correlation-Id: demo-transfer-a-to-b
```

```json
{
  "sourceWalletId": "aaaaaaaa-1111-4111-8111-111111111111",
  "destinationWalletId": "bbbbbbbb-2222-4222-8222-222222222222",
  "amount": 125.50
}
```

Successful response shape:

```json
{
  "isSuccess": true,
  "code": "WALLET_TRANSFER_COMPLETED",
  "message": "Wallet transfer completed successfully.",
  "data": {
    "transactionId": "cccccccc-3333-4333-8333-333333333333",
    "sourceWalletId": "aaaaaaaa-1111-4111-8111-111111111111",
    "destinationWalletId": "bbbbbbbb-2222-4222-8222-222222222222",
    "amount": 125.50,
    "currency": "TRY",
    "completedAt": "2026-08-09T19:55:00+00:00",
    "wasReplay": false
  },
  "errors": []
}
```

The exact response property names are defined by `WalletTransferResponse`; use Swagger generated from the current branch as the contract source when importing into Postman.

## 11. Replay the transfer safely

Send the exact same request with the exact same `Idempotency-Key`.

Expected result:

- no second money movement;
- no second ledger posting;
- no second fraud evaluation for a completed replay;
- HTTP 200;
- code `WALLET_TRANSFER_REPLAYED`;
- same immutable transaction ID;
- `wasReplay = true`.

Using the same key with a different amount/destination returns an idempotency conflict.

## Gateway/auth expectations

| Request | Expected gateway behavior |
|---|---|
| Register/login/verify/refresh without JWT | Allowed to FinWallet.Api |
| Wallet/bank-account/transfer without JWT | Rejected at Gateway |
| Client calls `/providers/*` without internal service key | Rejected at Gateway |
| Direct call to FinWallet.Api/simulator business endpoint without downstream key | Rejected by destination service |
| Request exceeds gateway rate/body/header bounds | Rejected before business processing |

## Swagger URLs in development

Each service has Swagger enabled by default in development. Examples:

```text
http://localhost:8080/swagger   Gateway
http://localhost:8081/swagger   FinWallet.Api contract inspection
http://localhost:8082/swagger   FakeBank
http://localhost:8083/swagger   FakeFraud
```

Normal business calls should still be executed through `http://localhost:8080`.
