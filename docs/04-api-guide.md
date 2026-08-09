# FinWallet API Guide

## API conventions

### Base path

Main FinWallet endpoints use `/api/v1`.

### Correlation

Clients may supply `X-Correlation-Id`. Values are accepted only when they are at most 128 characters and contain safe ASCII identifier characters. Invalid/missing values are replaced with a generated identifier. The API returns the effective value in the response header, uses it as the ASP.NET `TraceIdentifier`, and propagates it to external provider calls.

Correlation IDs are not transaction IDs and must not contain PII, tokens, account numbers or other sensitive data.

### Authentication

Protected endpoints use `Authorization: Bearer <access-token>`.

JWT access tokens are short lived, issuer/audience/signature/lifetime are validated, and accepted algorithms are restricted to the fixed FinWallet signing algorithm. Raw access tokens and refresh tokens must never be written to logs.

### Idempotency

All future money-changing endpoints require `Idempotency-Key`. Registration/login endpoints do not use the financial idempotency mechanism. Registration uniqueness is durably protected by the normalized-phone MSSQL unique constraint and OTP verification is single-use in Redis.

### Error format

Expected API errors use Problem Details with a stable `code` extension and `traceId`. Client applications must branch on HTTP status + error code rather than parsing human-readable text.

Examples include:

- `REGISTRATION_NOT_ALLOWED`
- `REGISTRATION_CONFLICT`
- `OTP_RESEND_RATE_LIMIT`
- `INVALID_REGISTRATION_OTP`
- `INVALID_CREDENTIALS`
- `AUTH_TEMPORARILY_LOCKED`
- `INVALID_REFRESH_TOKEN`
- `REFRESH_TOKEN_REUSE_DETECTED`

Unexpected exceptions return a generic `UNEXPECTED_ERROR` without internal exception details.

## Runtime configuration

Authentication persistence intentionally has no in-memory production fallback. The API requires the following configuration values at startup. Environment variables can use ASP.NET Core's `__` separator.

| Configuration key | Environment variable example | Purpose |
|---|---|---|
| `FinWallet:Sql:ConnectionString` | `FinWallet__Sql__ConnectionString` | MSSQL durable source of customer/auth state |
| `FinWallet:Redis:ConnectionString` | `FinWallet__Redis__ConnectionString` | Redis registration OTP state |
| `FinWallet:Security:RegistrationOtpPepper` | `FinWallet__Security__RegistrationOtpPepper` | HMAC secret; minimum 32 UTF-8 bytes |
| `FinWallet:Security:Jwt:Issuer` | `FinWallet__Security__Jwt__Issuer` | JWT issuer |
| `FinWallet:Security:Jwt:Audience` | `FinWallet__Security__Jwt__Audience` | JWT audience |
| `FinWallet:Security:Jwt:SigningKey` | `FinWallet__Security__Jwt__SigningKey` | JWT signing secret; minimum 32 UTF-8 bytes |
| `FinWallet:Integrations:FakeCommunication:BaseUrl` | `FinWallet__Integrations__FakeCommunication__BaseUrl` | FakeCommunication API base URL |

Secrets must come from the deployment secret mechanism/environment and must not be committed to source control.

## Authentication and registration endpoints

### POST `/api/v1/auth/register`

Creates a durable PendingVerification customer and attempts to send the initial verification OTP through FakeCommunication.

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
- Baseline supports `TR/+90` and `AZ/+994`.
- Country selection and phone calling code/length must match.
- Phone is normalized before lookup and also protected by a MSSQL unique constraint.
- Password must satisfy the fixed server-side password policy.
- Raw password is never persisted.
- Customer + CustomerCredential are committed atomically in MSSQL.
- OTP is created after the MSSQL transaction completes.
- Redis stores only a customer-bound HMAC-SHA256 digest, never the raw OTP.
- SMS delivery occurs outside the MSSQL transaction.
- FakeCommunication failure does **not** roll back the durable customer identity.

Successful response: `202 Accepted`

```json
{
  "customerId": "d80b3773-ae17-4ca4-87e0-dca42d40ad6a",
  "otpExpiresAt": "2026-08-09T13:40:00Z",
  "otpDeliverySucceeded": true
}
```

When `otpDeliverySucceeded=false`, the registration still exists and the client can use the resend endpoint. The OTP itself is never returned by FinWallet.

### POST `/api/v1/auth/registration/resend-otp`

Issues a new OTP for a customer that is still PendingVerification and attempts SMS delivery again.

Request:

```json
{
  "customerId": "d80b3773-ae17-4ca4-87e0-dca42d40ad6a"
}
```

Successful response: `200 OK`

```json
{
  "otpExpiresAt": "2026-08-09T13:45:00Z",
  "otpDeliverySucceeded": true
}
```

Rules:

- A fixed resend cooldown is enforced in Redis.
- Creating a new challenge replaces the previous active OTP after cooldown.
- Unknown/non-pending customers use the same generic verification failure behavior.
- Provider failure returns `otpDeliverySucceeded=false` without changing durable customer state.

### POST `/api/v1/auth/registration/verify`

Verifies and atomically consumes the SMS OTP, then activates a PendingVerification customer.

Request:

```json
{
  "customerId": "d80b3773-ae17-4ca4-87e0-dca42d40ad6a",
  "code": "123456"
}
```

Successful response: `204 No Content`

Rules:

- OTP comparison, failed-attempt increment and successful deletion happen atomically in Redis Lua.
- Maximum failed attempts are fixed by security policy.
- A consumed OTP cannot be replayed.
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

Successful response: `200 OK`

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
- Raw refresh token is returned once to the client; MSSQL stores only its SHA-256 lookup hash.
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

Successful response has the same token shape as login.

Rules:

- Raw refresh tokens are never persisted.
- Server lookup uses a deterministic SHA-256 hash of the opaque token.
- MSSQL rotation uses compare-and-set semantics (`ConsumedAt IS NULL AND RevokedAt IS NULL`).
- Concurrent rotation attempts therefore have one durable winner.
- The losing/reused token path revokes the session and remaining refresh-token family.
- Expired/revoked/unknown tokens return a generic invalid-refresh response.

### POST `/api/v1/auth/logout`

Planned endpoint that will revoke the current session and all refresh tokens associated with it. Existing access JWTs remain short-lived; higher-risk protected endpoints can additionally validate session state when needed.

## Financial endpoint conventions

Financial endpoint details are added in later phases. All money-changing operations will require:

- authenticated active customer/session;
- `Idempotency-Key`;
- correlation ID;
- currency-aware amount;
- internal and external fraud evaluation where applicable;
- cutoff evaluation for bank workflows where applicable;
- balanced ledger commit;
- structured masked financial logging;
- outbox-driven post-commit SMS/email notifications.
