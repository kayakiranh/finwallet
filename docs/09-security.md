# Security Design

## Security principles

FinWallet prioritizes financial correctness and credential safety over runtime configurability or convenience.

- ASP.NET Core Identity is not used.
- Every interactive principal is a Customer.
- Customer identity, credential material, sessions and refresh tokens are separated.
- Security algorithms/work factors are fixed in code and cannot be weakened through application configuration.
- Deployment secrets remain external to source control.
- Passwords, OTPs, JWTs, refresh tokens, Authorization headers and signing secrets are never logged.

## Customer data separation

`Customer` remains intentionally small and contains identity/contact/lifecycle data only.

Credential state belongs to `CustomerCredential`:

- password hash;
- password salt;
- hash version;
- failed-login count;
- temporary lock state;
- password-changed timestamp.

Session state belongs to `CustomerSession` and refresh-token state belongs to `RefreshToken`.

## Registration country policy

Registration uses an explicit allow-list rather than accepting arbitrary country/phone combinations.

Phase 2 baseline:

| Country | Calling code | National digits |
|---|---:|---:|
| TR | +90 | 10 |
| AZ | +994 | 9 |

Phone numbers are normalized into an E.164-like `+<digits>` value before lookup. The selected registration country must match the normalized phone calling code and expected national-number length.

Adding another registration country is a deliberate business/security change and should update tests and this document.

## Password policy

The password policy is fixed:

- minimum length: 12 characters;
- maximum length: 128 characters;
- control characters are rejected;
- the policy is not selectable in appsettings.

Password storage uses the .NET one-shot `Rfc2898DeriveBytes.Pbkdf2` API:

- PBKDF2-HMAC-SHA512;
- 220,000 iterations;
- 32-byte cryptographically random salt per password;
- 64-byte derived hash;
- constant-time hash comparison;
- schema version `1` persisted for future migration only.

The raw password is never persisted.

## Login lockout

`CustomerCredential` applies fixed temporary lock behavior:

- five consecutive failed login attempts;
- fifteen-minute temporary lock;
- successful login clears the failed-attempt counter and lock state.

Unknown phone numbers receive generic `InvalidCredentials` behavior. The login handler performs expensive password-hash work for unknown users when possible to reduce coarse account-existence timing differences.

## JWT access tokens

Access tokens:

- are signed JWTs;
- use fixed HMAC-SHA256 signing;
- have a fixed ten-minute lifetime;
- contain a minimal claim set: customer subject, session identifier (`sid`), JTI and issued-at time;
- do not contain phone, email, balance, IBAN or other customer/financial data.

JWT deployment values:

- issuer: configuration;
- audience: configuration;
- signing key: secret store/environment;
- signing key must contain at least 32 UTF-8 bytes.

Algorithm and lifetime are not runtime options.

## Sessions

A successful login creates a device-bound `CustomerSession`.

- absolute session lifetime: 30 days;
- session can be revoked independently of JWT expiration;
- `sid` claim links an access token to its server-side session;
- last activity is updated on successful refresh;
- future high-risk authorization can combine JWT validation with server-side session state.

## Refresh tokens

Refresh tokens are opaque cryptographic random values:

- 64 random bytes before URL-safe encoding;
- raw token is returned to the client only;
- raw token is never persisted;
- server persists SHA-256 token hash for lookup;
- each refresh token is single-use;
- each token has a maximum fourteen-day lifetime and cannot outlive the absolute session expiration;
- successful refresh consumes the old token and creates a replacement in one persistence transaction;
- use of an already-consumed token is treated as reuse and revokes the associated session/token family.

## OTP

Registration OTP rules are implemented behind `IRegistrationOtpService` so Redis details do not enter Application/Domain.

The Phase 3 implementation must provide:

- cryptographically secure numeric OTP generation;
- short TTL;
- bounded verification attempts;
- resend cooldown;
- replacement/invalidation of previous active challenges;
- atomic verify-and-consume behavior;
- no raw OTP logging;
- Redis loss behavior that cannot accidentally activate a customer.

FakeCommunication receives the raw OTP only for the simulated SMS delivery call.

## Sensitive logging policy

Never log:

- raw password;
- password hash or salt;
- OTP;
- JWT access token;
- refresh token or refresh-token hash;
- Authorization header;
- JWT signing key or other secrets.

Phone/email/account identifiers must pass centralized masking before financial/application/audit logs are written.

## Threat scenarios

### Credential stuffing / brute force

Controls:

- fixed password hashing cost;
- login failure counters and temporary locks;
- API rate limiting added at the HTTP boundary;
- generic invalid-credential response.

### OTP brute force

Controls:

- short TTL;
- attempt limit;
- resend cooldown;
- atomic consume;
- registration rate limiting.

### Refresh-token theft/replay

Controls:

- opaque random token;
- server stores only hash;
- single-use rotation;
- reuse detection revokes session;
- absolute session lifetime.

### JWT theft

Controls:

- ten-minute token lifetime;
- minimal claims;
- session identifier correlation;
- signing key stored outside source;
- session revocation for high-risk authorization paths.

### Log leakage

Controls:

- central masking/redaction requirement;
- structured logging fields rather than arbitrary request-body logging;
- explicit forbidden-sensitive-data list;
- security/QA tests will inspect generated log files for forbidden values.

## Remaining Phase 2/3 work

- durable MSSQL implementations of auth stores;
- Redis OTP implementation;
- API rate limiting and auth middleware wiring;
- logout/session revocation endpoint;
- authentication unit/integration/security tests;
- secret configuration validation at application startup.
