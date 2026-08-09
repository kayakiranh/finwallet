# FinWallet API Guide

## Public entry point

Normal clients call YARP Gateway, not FinWallet.Api directly.

Local development:

```text
http://localhost:8080
```

Main business path prefix:

```text
/api/v1
```

Gateway routes anonymous authentication endpoints to FinWallet.Api. Other `/api/*` routes require a valid JWT at the Gateway before proxying.

FinWallet.Api remains independently authenticated/authorized and destination services require the Gateway downstream service credential, so the proxy is an outer boundary rather than the only security control.

## Controller-based APIs

All project HTTP services use ASP.NET Core controllers. Minimal API mappings are not used.

## ServiceResult

Controller success/failure bodies and platform authentication/rate-limit failures use `ServiceResult<T>`.

Clients should branch on:

1. HTTP status;
2. stable machine-readable `code`.

Do not parse human-readable `message` text for application logic.

`ServiceResult<T>` remains an HTTP contract; Domain/Application do not depend on it.

## Correlation

A caller may send:

```http
X-Correlation-Id: mobile-request-123
```

The shared web platform accepts only bounded alphanumeric/`-`/`_` values. Invalid or absent values are replaced with a generated correlation ID. The resulting value becomes the ASP.NET trace identifier and is returned in the response header.

Correlation ID is not a transaction ID or idempotency key and must not contain PII.

## Authentication

Protected endpoints use:

```http
Authorization: Bearer <access-token>
```

JWT is validated at Gateway and again by FinWallet.Api. High-risk transfer flows additionally validate durable server-side session state using `sid`.

Raw access/refresh tokens must never be logged.

## Idempotency

Money-changing wallet transfer requires:

```http
Idempotency-Key: <stable-client-generated-key>
```

Wallet creation is not a money movement and is idempotent through the `(CustomerId, Currency)` uniqueness invariant.

Provider request keys are separate from client idempotency and correlation IDs.

## Swagger

Every Web API has Swagger/OpenAPI through `FinWallet.Shared.Web`.

Development defaults:

```text
Gateway:             http://localhost:8080/swagger
FinWallet.Api:       http://localhost:8081/swagger
FakeBank.Api:        http://localhost:8082/swagger
FakeFraud.Api:       http://localhost:8083/swagger
FakeCutoff.Api:      http://localhost:8084/swagger
FakeCampaign.Api:    http://localhost:8085/swagger
FakeCommunication:   http://localhost:8086/swagger
```

Production Swagger is disabled by default through `appsettings.Production.json`.

Swagger visibility never bypasses endpoint authorization.

## Authentication endpoints

### POST `/api/v1/auth/register`

Anonymous at Gateway. Creates pending customer + credentials and sends OTP through FakeCommunication via Gateway.

Success: HTTP 202 / `REGISTRATION_ACCEPTED`.

### POST `/api/v1/auth/registration/verify`

Anonymous at Gateway. Verifies/consumes OTP and activates eligible customer.

Success: HTTP 200 / `REGISTRATION_VERIFIED`.

### POST `/api/v1/auth/login`

Anonymous at Gateway. Validates active customer credentials and creates device-bound server session + access/refresh tokens.

Success: HTTP 200 / `AUTHENTICATED`.

### POST `/api/v1/auth/refresh`

Anonymous at Gateway because the opaque refresh token is the credential for this operation. Rotation remains server-side and single-use.

### POST `/api/v1/auth/logout`

Not implemented yet.

## Wallet endpoints

### POST `/api/v1/wallets`

Requires JWT.

Request:

```json
{
  "currency": "TRY"
}
```

Supported values: `TRY`, `USD`, `EUR`.

Behavior:

- first customer/currency wallet: HTTP 201 / `WALLET_CREATED`;
- repeated request: HTTP 200 / `WALLET_EXISTS`;
- concurrent duplicate create converges on DB winner;
- new wallet begins with zero available/blocked balance;
- API cannot mint an initial balance.

### GET `/api/v1/wallets`

Requires JWT. Returns wallets owned by authenticated JWT subject.

## Bank-account endpoint

### POST `/api/v1/bank-accounts`

Requires JWT.

```json
{
  "walletId": "f98c4910-44c4-42fb-9ff1-c2cd9c0f73bd"
}
```

Flow:

```text
Gateway JWT
-> FinWallet JWT/ownership
-> durable BankAccount(Opening)
-> SQL operation completes
-> FinWallet calls Gateway /providers/bank/* with internal caller key
-> Gateway validates caller and injects downstream key
-> FakeBank
-> validate provider identity/currency
-> CAS-update internal BankAccount state
```

No provider HTTP call executes while a FinWallet SQL transaction is held open.

Provider request-key is deterministic from durable internal BankAccount ID, so timeout/lost-response retry does not create a duplicate provider account.

## Wallet transfer endpoint

### POST `/api/v1/transfers`

Requires:

- JWT;
- active durable financial session;
- `Idempotency-Key`;
- valid source ownership/destination/currency/lifecycle;
- internal + external fraud Allow;
- sufficient source balance.

Request:

```http
POST /api/v1/transfers
Authorization: Bearer <JWT>
Idempotency-Key: transfer-000001
Content-Type: application/json
```

```json
{
  "sourceWalletId": "aaaaaaaa-1111-4111-8111-111111111111",
  "destinationWalletId": "bbbbbbbb-2222-4222-8222-222222222222",
  "amount": 125.50
}
```

Execution order:

```text
completed durable replay check
-> durable server session/risk signals
-> internal fraud
-> FakeFraud through Gateway
-> final fraud decision
-> atomic MSSQL posting
```

Atomic posting includes:

- durable idempotency state;
- source/destination balance changes;
- FinancialTransaction;
- LedgerJournal;
- LedgerEntries;
- persisted Debit/Credit equality verification.

External fraud is fail-closed. If it times out/fails/malforms, financial posting does not start.

Completed replay uses the same idempotency key/request and returns the immutable original transaction without a second money movement or second fraud evaluation.

## Funding status

New wallets start with zero balance. A public BankDeposit/funding endpoint is not implemented yet. Therefore a successful transfer from a newly registered wallet requires a controlled integration fixture that creates a balanced funding transaction/ledger state. Directly updating `Wallets.AvailableBalance` is invalid because it bypasses the ledger.

See `16-happy-path-onboarding.md`.

## Provider/internal routes

Provider routes are not public client APIs:

```text
/providers/bank/*
/providers/fraud/*
/providers/cutoff/*
/providers/campaign/*
/providers/communication/*
```

They require Gateway `InternalService` authorization. Destination provider APIs then require the separate downstream service credential.

## HTTP/platform failures

Examples:

- missing/invalid JWT at Gateway: 401 `GATEWAY_UNAUTHORIZED`;
- direct provider call without internal caller key: 403/authorization rejection at Gateway;
- direct backend call without downstream key: 401 `INTERNAL_SERVICE_UNAUTHORIZED`;
- rate limit: 429 `RATE_LIMITED`;
- unsupported write content type: 415 `UNSUPPORTED_MEDIA_TYPE`;
- blocked TRACE/CONNECT: 405 `METHOD_NOT_ALLOWED`;
- request too large: rejected by Kestrel/YARP before business processing.

## Financial error examples

- invalid financial session: 401 `TRANSFER_SESSION_INVALID`;
- fraud deny: 403 `TRANSFER_FRAUD_DENIED`;
- fraud review: 202 `TRANSFER_REVIEW_REQUIRED` and no money movement;
- missing wallet: 404;
- idempotency payload conflict: 409 `IDEMPOTENCY_CONFLICT`;
- insufficient balance: 409 `INSUFFICIENT_BALANCE`;
- fraud dependency unavailable: 503 `FRAUD_DEPENDENCY_UNAVAILABLE`;
- currency mismatch: 400 `CURRENCY_MISMATCH`.

## Request limits

Gateway and backends use config-driven:

- per-IP fixed-window rate limits;
- request-body limits;
- header-count/total-size limits;
- request-header timeout;
- keep-alive timeout;
- max concurrent connections.

Public limits are intentionally stricter at Gateway; backends retain second-layer limits for bypass/misrouting/internal-runaway protection.
