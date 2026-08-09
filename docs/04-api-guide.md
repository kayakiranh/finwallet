# FinWallet API Guide

## API conventions

### Controller-based Web API

Every FinWallet HTTP service uses ASP.NET Core controller-based Web API. Minimal API endpoint mappings are forbidden. `Program.cs` is limited to composition/bootstrap concerns and registers controllers through `AddControllers()` / `MapControllers()`.

### Base path

Main FinWallet business endpoints use `/api/v1`.

### ServiceResult response envelope

Every API response body uses the shared `ServiceResult<T>` contract from `FinWallet.Shared.Contracts`.

Success example:

```json
{
  "isSuccess": true,
  "code": "AUTHENTICATED",
  "message": "Authentication completed successfully.",
  "data": {
    "customerId": "d80b3773-ae17-4ca4-87e0-dca42d40ad6a"
  },
  "errors": []
}
```

Failure example:

```json
{
  "isSuccess": false,
  "code": "INVALID_CREDENTIALS",
  "message": "The supplied credentials are invalid.",
  "data": null,
  "errors": []
}
```

Client applications must branch on HTTP status and the stable `code`; human-readable messages must not be parsed for logic.

`ServiceResult<T>` is an HTTP transport contract. Domain and Application layers do not depend on it.

### Correlation

The current authentication API uses ASP.NET Core request trace identifiers when propagating correlation to FakeCommunication. A dedicated validated `X-Correlation-Id` propagation component remains part of the observability backlog. Correlation IDs are not transaction IDs and must never contain PII.

### Authentication

Protected endpoints use `Authorization: Bearer <access-token>`.

JWT access tokens are short lived. Raw access tokens and refresh tokens must never be written to logs.

### Idempotency

All money-changing endpoints require `Idempotency-Key`. Registration/login endpoints do not use the financial idempotency mechanism; registration uniqueness is protected by normalized phone uniqueness and OTP verification.

### Error handling

Expected registration/authentication exceptions are converted centrally into `ServiceResult<object>` failures with stable machine-readable codes. Unexpected exception details are not returned to clients.

## Health

### GET `/health/live`

All HTTP services expose controller-based liveness endpoints and return `ServiceResult<HealthResponse>`.

## Authentication and registration endpoints

The endpoints below are implemented as actions on `AuthenticationController`.

### POST `/api/v1/auth/register`

Creates a durable pending customer registration and sends a verification OTP through FakeCommunication.

Request:

```json
{
  "countryCode": "TR",
  "phoneNumber": "+90 532 123 45 67",
  "email": "customer@example.com",
  "password": "customer supplied secret"
}
```

Processing rules:

- Supported registration countries are explicitly allow-listed.
- Current baseline supports `TR/+90` and `AZ/+994`.
- Country selection and phone calling code must match.
- Phone is normalized before uniqueness lookup.
- Password must satisfy the fixed server-side password policy.
- Password is converted to PBKDF2 hash material and the raw password is never persisted.
- Customer and CustomerCredential are persisted atomically in PendingVerification state.
- OTP issuance/SMS occurs after the durable DB transaction has completed.

Successful response: HTTP `202 Accepted` with `ServiceResult<RegisterCustomerResponse>`.

```json
{
  "isSuccess": true,
  "code": "REGISTRATION_ACCEPTED",
  "message": "Registration accepted and verification is pending.",
  "data": {
    "customerId": "d80b3773-ae17-4ca4-87e0-dca42d40ad6a",
    "otpExpiresAt": "2026-08-09T13:40:00Z"
  },
  "errors": []
}
```

The OTP itself is never returned by FinWallet.

### POST `/api/v1/auth/registration/verify`

Verifies and consumes the SMS OTP and activates a PendingVerification customer.

Request:

```json
{
  "customerId": "d80b3773-ae17-4ca4-87e0-dca42d40ad6a",
  "code": "123456"
}
```

Rules:

- OTP verification/consumption is atomic in Redis.
- A successful repeated verification after the customer is already Active is treated idempotently.
- Incorrect/expired/consumed OTPs return the same generic verification error.
- Success returns HTTP 200 with a body-bearing `ServiceResult<object>`; the API does not use 204 because every API response follows the ServiceResult contract.

### POST `/api/v1/auth/login`

Authenticates an active customer and creates a new device-bound session.

Request:

```json
{
  "phoneNumber": "+905321234567",
  "password": "customer supplied secret",
  "deviceId": "mobile-installation-identifier"
}
```

Successful response: HTTP `200 OK` with `ServiceResult<AuthenticationTokensResponse>`.

Rules:

- Unknown phone and wrong password return the same invalid-credentials response.
- Missing-user requests still perform expensive password work to reduce coarse enumeration timing differences.
- Failed-login state is updated under a short MSSQL row lock to prevent lost updates during concurrent invalid logins.
- Five consecutive failures trigger a fixed temporary credential lock.
- Successful login rechecks the current credential under lock before session creation; a concurrent lock or password change prevents session creation.
- Session, credential reset and initial refresh-token hash are persisted atomically.
- Access token lifetime is fixed at ten minutes.
- Session absolute lifetime is fixed at thirty days.
- Individual refresh tokens have a maximum fourteen-day lifetime and never outlive the session.

### POST `/api/v1/auth/refresh`

Rotates a single-use refresh token and issues a new access/refresh pair.

Request:

```json
{
  "refreshToken": "<opaque-token>"
}
```

Rules:

- Raw refresh tokens are never persisted.
- Server lookup uses a deterministic SHA-256 hash of the opaque token.
- The current refresh token is consumed and replaced atomically.
- Concurrent rotation uses MSSQL compare-and-set behavior so only one request can win.
- Reuse of a previously consumed refresh token revokes the associated session and remaining refresh tokens.
- Expired/revoked/unknown tokens return a generic invalid-refresh response.

### POST `/api/v1/auth/logout`

Not implemented yet. The planned endpoint revokes the current session and all refresh tokens associated with it.

## Financial endpoint conventions

Financial endpoint details are added with the transaction/ledger phase. All money-changing operations require:

- authenticated active customer/session;
- `Idempotency-Key`;
- correlation ID;
- currency-aware amount;
- internal and external fraud evaluation where applicable;
- cutoff evaluation for bank workflows where applicable;
- balanced ledger commit;
- structured masked financial logging;
- outbox-driven post-commit SMS/email notifications;
- `ServiceResult<T>` response bodies for success and failure.
