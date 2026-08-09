# FinWallet API Guide

## API conventions

### Controller-based Web API

Every FinWallet HTTP service uses ASP.NET Core controller-based Web API. Minimal API endpoint mappings are forbidden. `Program.cs` is limited to composition/bootstrap concerns and registers controllers through `AddControllers()` / `MapControllers()`.

### Base path

Main FinWallet business endpoints use `/api/v1`.

### ServiceResult response envelope

Every API response body uses the shared `ServiceResult<T>` contract from `FinWallet.Shared.Contracts`. This includes controller results, central exception responses and JWT authentication/authorization failures.

Clients must branch on HTTP status and stable `code`; human-readable messages must not be parsed for logic. `ServiceResult<T>` is an HTTP transport contract and Domain/Application do not depend on it.

### Correlation

The current API uses ASP.NET Core request trace identifiers when propagating correlation to external providers. A dedicated validated `X-Correlation-Id` component remains part of the observability backlog. Correlation IDs are not financial transaction IDs or idempotency keys and must not contain PII.

### Authentication

Protected endpoints use `Authorization: Bearer <access-token>`. JWT access tokens are short lived. Raw access/refresh tokens must never be logged.

JWT challenge and forbidden responses also use `ServiceResult<object>`:

- HTTP 401 / `UNAUTHORIZED`;
- HTTP 403 / `FORBIDDEN`.

### Idempotency

Money-changing endpoints require `Idempotency-Key`. Registration/login do not use financial idempotency. External-provider idempotency keys are separate from request correlation IDs.

### Error handling

Expected application/domain exceptions are converted centrally into safe `ServiceResult<object>` failures. Unexpected exception details are never returned to clients.

## Health

### GET `/health/live`

All HTTP services expose controller-based liveness endpoints and return `ServiceResult<HealthResponse>`.

## Authentication and registration endpoints

### POST `/api/v1/auth/register`

Creates a durable pending customer registration and sends a verification OTP through FakeCommunication.

Successful response: HTTP `202 Accepted` with `ServiceResult<RegisterCustomerResponse>`.

Rules:

- registration country is allow-listed;
- phone/country combination is normalized and validated;
- password is persisted only as PBKDF2 hash material;
- Customer + CustomerCredential are committed atomically;
- OTP/provider work happens after SQL commit.

### POST `/api/v1/auth/registration/verify`

Verifies/consumes registration OTP and activates an eligible pending customer. Success returns HTTP 200 with a body-bearing `ServiceResult<object>`.

### POST `/api/v1/auth/login`

Authenticates an active customer and creates a device-bound server-side session. Success returns HTTP 200 with `ServiceResult<AuthenticationTokensResponse>`.

Rules include generic invalid-credential responses, temporary lockout, short-lived JWT access tokens and atomically persisted session + initial refresh-token hash.

### POST `/api/v1/auth/refresh`

Rotates a single-use refresh token. Concurrent rotation uses MSSQL compare-and-set semantics; reuse detection revokes the related session/token family.

### POST `/api/v1/auth/logout`

Not implemented yet.

## Bank account endpoints

### POST `/api/v1/bank-accounts`

Requires a valid access token. Opens an external bank account for an internal Wallet owned by the authenticated JWT `sub` customer, or resumes an already durable pending opening.

Request:

```json
{
  "walletId": "f98c4910-44c4-42fb-9ff1-c2cd9c0f73bd"
}
```

Pending response: HTTP `202 Accepted` with `ServiceResult<BankAccountResponse>`.

```json
{
  "isSuccess": true,
  "code": "BANK_ACCOUNT_PENDING",
  "message": "Bank account opening is pending at the external provider.",
  "data": {
    "bankAccountId": "878a328e-c563-4772-a62f-afad3e4c8a5f",
    "walletId": "f98c4910-44c4-42fb-9ff1-c2cd9c0f73bd",
    "currency": "TRY",
    "externalAccountId": "89c1f38e-ce7e-4234-9743-eebbd67f69a2",
    "externalIban": "FWTRY...",
    "status": "Opening"
  },
  "errors": []
}
```

When the provider account is already final/active, HTTP 200 is returned with code `BANK_ACCOUNT_READY`.

Processing sequence:

```text
JWT customer
  -> owned Wallet lookup
  -> find/create durable BankAccount(Opening)
  -> SQL commit
  -> FakeBank HTTP call
  -> validate provider identity/currency
  -> apply provider state
  -> MSSQL status + UpdatedAt compare-and-set
```

Important rules:

- wallet lookup includes authenticated customer ownership; another customer's wallet is indistinguishable from a missing wallet;
- one internal BankAccount may exist per Wallet;
- internal BankAccount ID and provider Account ID are always separate;
- the provider request key is deterministically derived from the durable internal BankAccount ID;
- if the provider creates an account but the HTTP response is lost, a retry uses the same provider request key and cannot create a second external account;
- no external HTTP call runs inside an open FinWallet SQL transaction;
- pending openings are polled through the provider's read-only account lookup;
- stale provider results cannot overwrite newer BankAccount state because MSSQL update uses both expected status and expected `UpdatedAt` snapshot;
- provider currency/account identity changes are treated as upstream contract failures;
- provider IBAN-like data may be returned to the authenticated account owner but must be masked in logs.

Failure mapping:

- 401 `UNAUTHORIZED` / `INVALID_ACCESS_TOKEN` — authentication subject is missing or invalid;
- 404 `WALLET_NOT_FOUND` — wallet absent or not owned by authenticated customer;
- 409 `BANK_ACCOUNT_CONFLICT` — concurrent internal state change;
- 503 provider code — retryable timeout/network/provider failure;
- 502 provider code — non-retryable upstream contract/state inconsistency.

## Financial endpoint conventions

All future money-changing operations require:

- authenticated active customer/session;
- durable idempotency;
- correlation ID separate from idempotency key;
- currency-aware amount;
- internal/external fraud evaluation where applicable;
- cutoff evaluation for bank workflows where applicable;
- balanced ledger commit;
- structured masked financial logging;
- outbox-driven post-commit notifications;
- `ServiceResult<T>` response bodies for success and failure.
