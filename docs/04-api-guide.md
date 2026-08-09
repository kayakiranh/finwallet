# FinWallet API Guide

## API conventions

### Base path

Main FinWallet endpoints use `/api/v1`.

### Correlation

Clients may supply `X-Correlation-Id`. The API layer will validate or create a correlation identifier and propagate it to external provider calls. Correlation IDs are not transaction IDs and must not contain PII.

### Authentication

Protected endpoints use `Authorization: Bearer <access-token>`.

JWT access tokens are short lived. Raw access tokens and refresh tokens must never be written to logs.

### Idempotency

All future money-changing endpoints require `Idempotency-Key`. Registration/login endpoints do not use the financial idempotency mechanism; registration uniqueness is protected by normalized phone uniqueness and OTP verification.

### Error format

API errors will use Problem Details with a stable application error code. Client applications must branch on the error code/status rather than parsing human-readable messages.

## Authentication and registration endpoints

The contracts below are the Phase 2 target API surface. Routes are connected only after the durable MSSQL/Redis implementations are available; no in-memory production substitute is used.

### POST `/api/v1/auth/register`

Creates a pending customer registration and sends a verification OTP through FakeCommunication.

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
- Phase 2 baseline supports `TR/+90` and `AZ/+994`.
- Country selection and phone calling code must match.
- Phone is normalized before uniqueness lookup.
- Password must satisfy the fixed server-side password policy.
- Password is converted to PBKDF2 hash material and the raw password is never persisted.
- Customer and CustomerCredential are persisted atomically in PendingVerification state.
- OTP issuance/SMS occurs after the durable DB transaction has completed.
- A communication-provider failure leaves the customer PendingVerification and is recoverable through OTP resend rather than rolling back durable customer identity.

Successful response (planned `202 Accepted`):

```json
{
  "customerId": "d80b3773-ae17-4ca4-87e0-dca42d40ad6a",
  "otpExpiresAt": "2026-08-09T13:40:00Z"
}
```

The OTP itself is never returned by FinWallet.

### POST `/api/v1/auth/register/verify`

Verifies and consumes the SMS OTP and activates a PendingVerification customer.

Request:

```json
{
  "customerId": "d80b3773-ae17-4ca4-87e0-dca42d40ad6a",
  "code": "123456"
}
```

Rules:

- OTP verification/consumption must be atomic in the OTP store.
- A successful repeated verification after the customer is already Active is treated idempotently.
- Incorrect/expired/consumed OTPs return the same generic verification error.

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

Successful response (planned `200 OK`):

```json
{
  "customerId": "d80b3773-ae17-4ca4-87e0-dca42d40ad6a",
  "sessionId": "b50c09dc-2ff4-4f6a-ac5f-79f9890f35f2",
  "accessToken": "<jwt>",
  "accessTokenExpiresAt": "2026-08-09T13:50:00Z",
  "refreshToken": "<opaque-token>",
  "refreshTokenExpiresAt": "2026-08-23T13:40:00Z"
}
```

Rules:

- Unknown phone and wrong password return the same invalid-credentials response.
- Missing-user requests still perform expensive password work to reduce coarse enumeration timing differences.
- Five consecutive failures trigger a fixed temporary credential lock.
- Successful login resets failed-login state and atomically persists the session plus initial refresh-token hash.
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
- Reuse of a previously consumed refresh token revokes the associated session and remaining refresh tokens.
- Expired/revoked/unknown tokens return a generic invalid-refresh response.

### POST `/api/v1/auth/logout`

Planned Phase 2/3 endpoint that revokes the current session and all refresh tokens associated with it. Existing access JWTs remain short-lived; protected endpoint authorization will additionally be able to consult session revocation state where the operation requires it.

## Financial endpoint conventions

Financial endpoint details are added in Phase 6. All money-changing operations will require:

- authenticated active customer/session;
- `Idempotency-Key`;
- correlation ID;
- currency-aware amount;
- internal and external fraud evaluation where applicable;
- cutoff evaluation for bank workflows where applicable;
- balanced ledger commit;
- structured masked financial logging;
- outbox-driven post-commit SMS/email notifications.
