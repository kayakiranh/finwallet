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

Money-changing endpoints require `Idempotency-Key`. Wallet creation is not a money movement; its idempotency is provided by the customer/currency uniqueness invariant. External-provider idempotency keys are separate from request correlation IDs.

### Error handling

Expected application/domain exceptions are converted centrally into safe `ServiceResult<object>` failures. Unexpected exception details are never returned to clients.

## Health

### GET `/health/live`

All HTTP services expose controller-based liveness endpoints and return `ServiceResult<HealthResponse>`.

## Authentication and registration endpoints

### POST `/api/v1/auth/register`

Creates a durable pending customer registration and sends a verification OTP through FakeCommunication. Success returns HTTP `202 Accepted` with `ServiceResult<RegisterCustomerResponse>`.

### POST `/api/v1/auth/registration/verify`

Verifies/consumes registration OTP and activates an eligible pending customer. Success returns HTTP 200 with a body-bearing `ServiceResult<object>`.

### POST `/api/v1/auth/login`

Authenticates an active customer and creates a device-bound server-side session. Success returns HTTP 200 with `ServiceResult<AuthenticationTokensResponse>`.

### POST `/api/v1/auth/refresh`

Rotates a single-use refresh token. Concurrent rotation uses MSSQL compare-and-set semantics; reuse detection revokes the related session/token family.

### POST `/api/v1/auth/logout`

Not implemented yet.

## Wallet endpoints

Wallet endpoints require a valid access token and derive ownership exclusively from the validated JWT `sub` customer identifier.

### POST `/api/v1/wallets`

Creates a zero-balance wallet for one supported currency.

Request:

```json
{
  "currency": "TRY"
}
```

Supported values are `TRY`, `USD` and `EUR`.

Behavior:

- first create for a customer/currency returns HTTP 201 / `WALLET_CREATED`;
- repeated create for the same customer/currency returns HTTP 200 / `WALLET_EXISTS` with the same durable wallet;
- concurrent create requests converge on the database winner through `UNIQUE(CustomerId, Currency)` + `TryInsertAsync` + reload;
- new wallets start with available and blocked balance equal to zero;
- wallet creation does not accept an initial balance and therefore cannot mint money.

Example success data:

```json
{
  "walletId": "f98c4910-44c4-42fb-9ff1-c2cd9c0f73bd",
  "currency": "TRY",
  "availableBalance": 0,
  "blockedBalance": 0,
  "status": "Active",
  "createdAt": "2026-08-09T15:00:00Z"
}
```

### GET `/api/v1/wallets`

Returns all wallets owned by the authenticated customer ordered by currency. An empty customer wallet set returns HTTP 200 with an empty collection and code `WALLETS_RETRIEVED`.

## Bank account endpoints

### POST `/api/v1/bank-accounts`

Requires a valid access token. Opens an external bank account for an internal Wallet owned by the authenticated JWT `sub` customer, or resumes an already durable pending opening.

Request:

```json
{
  "walletId": "f98c4910-44c4-42fb-9ff1-c2cd9c0f73bd"
}
```

Pending response: HTTP `202 Accepted` with `ServiceResult<BankAccountResponse>`. When the provider account is already final/active, HTTP 200 is returned with code `BANK_ACCOUNT_READY`.

Processing sequence:

```text
JWT customer
  -> owned Wallet lookup
  -> find/create durable BankAccount(Opening)
  -> SQL operation completes
  -> FakeBank HTTP call
  -> validate provider identity/currency
  -> apply provider state
  -> MSSQL status + UpdatedAt compare-and-set
```

Important rules:

- another customer's wallet is indistinguishable from a missing wallet;
- one internal BankAccount may exist per Wallet;
- internal BankAccount ID and provider Account ID are always separate;
- provider request key is deterministically derived from the durable internal BankAccount ID;
- a lost provider HTTP response can be retried without creating a duplicate external account;
- no external HTTP call runs inside an open FinWallet SQL transaction;
- pending openings use provider read-only polling;
- stale results cannot overwrite newer BankAccount state because MSSQL update uses expected status + expected `UpdatedAt`;
- provider IBAN-like data may be returned to the authenticated owner but must be masked in logs.

Failure mapping:

- 401 `UNAUTHORIZED` / `INVALID_ACCESS_TOKEN`;
- 404 `WALLET_NOT_FOUND`;
- 409 `BANK_ACCOUNT_CONFLICT`;
- 503 retryable provider failure;
- 502 non-retryable provider contract/state inconsistency.

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
