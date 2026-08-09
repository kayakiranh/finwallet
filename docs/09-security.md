# Security Design

## Security principles

FinWallet prioritizes financial correctness and credential safety over runtime configurability or convenience.

- ASP.NET Core Identity is not used.
- Every interactive principal is a Customer.
- Customer identity, credential material, sessions and refresh tokens are separated.
- Security algorithms/work factors are fixed in code and cannot be weakened through application configuration.
- Deployment secrets remain external to source control.
- Passwords, OTPs, JWTs, refresh tokens, Authorization headers and signing secrets are never logged.
- MSSQL is the durable authentication/financial source of truth; Redis is transient support only.

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

| Country | Calling code | National digits |
|---|---:|---:|
| TR | +90 | 10 |
| AZ | +994 | 9 |

Phone numbers are normalized into an E.164-like `+<digits>` value before lookup. The selected registration country must match the normalized phone calling code and expected national-number length. MSSQL additionally enforces a unique normalized phone number so two concurrent registration requests cannot create duplicate customers even if both pass an application-level pre-check.

## Password policy and storage

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

## MSSQL authentication persistence

Authentication persistence uses explicit parameterized `Microsoft.Data.SqlClient` commands rather than an ORM in the initial implementation.

Durable tables:

- `Customers`;
- `CustomerCredentials`;
- `CustomerSessions`;
- `RefreshTokens`.

Important database constraints:

- normalized phone number is unique;
- refresh-token hash is unique;
- session/token relationships use foreign keys;
- lifecycle timestamp checks reject obviously inconsistent state;
- rowversion columns exist for later optimistic-concurrency paths.

Registration writes Customer and CustomerCredential in one short SQL transaction. External SMS calls occur only after that transaction has completed.

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
- device identifier is bounded to 128 characters;
- session can be revoked independently of JWT expiration;
- `sid` claim links an access token to its server-side session;
- last activity is updated on successful refresh;
- persisted session state is restored through explicit domain factories rather than reflection/private-setter mutation.

## Refresh tokens and concurrency

Refresh tokens are opaque cryptographic random values:

- 64 random bytes before URL-safe encoding;
- raw token is returned to the client only;
- raw token is never persisted;
- server persists SHA-256 token hash for lookup;
- each refresh token is single-use;
- each token has a maximum fourteen-day lifetime and cannot outlive the absolute session expiration;
- use of an already-consumed token is treated as reuse and revokes the associated session/token family.

Concurrent rotation is protected in MSSQL, not only in application memory. The persistence operation inserts the replacement inside the transaction and conditionally consumes the original token with:

- matching token ID/session/hash;
- `ConsumedAt IS NULL`;
- `RevokedAt IS NULL`.

Only one concurrent request can affect the original token row. A losing request rolls back its replacement insert, is treated as replay/reuse, and triggers session/token-family revocation.

## Registration OTP in Redis

Registration OTP is intentionally transient and therefore stored in Redis, but customer activation requires a successful Redis verify-and-consume result and durable MSSQL customer state.

Fixed OTP policy:

- six numeric digits generated with `RandomNumberGenerator`;
- five-minute TTL;
- maximum five failed verification attempts;
- thirty-second resend cooldown;
- a new allowed issue replaces the previous active challenge;
- successful verification atomically deletes the challenge, preventing replay.

Raw OTP is not stored in Redis. Redis stores a customer-bound HMAC-SHA256 digest using a deployment pepper secret of at least 32 UTF-8 bytes. The pepper is provided by a secret store/environment and is never a runtime-selectable algorithm/work-factor setting.

OTP issue and verification state changes use Redis Lua scripts so cooldown checks, attempt increments and verify-and-consume behavior are atomic on the Redis server. Redis failure fails closed: the OTP service throws/fails and customer activation is not performed.

FakeCommunication receives the raw OTP only for simulated SMS delivery.

## Sensitive logging policy

Never log:

- raw password;
- password hash or salt;
- OTP;
- OTP HMAC digest or pepper;
- JWT access token;
- refresh token or refresh-token hash;
- Authorization header;
- JWT signing key or other secrets;
- MSSQL/Redis connection secrets.

Phone/email/account identifiers must pass centralized masking before financial/application/audit logs are written.

## Threat scenarios

### Concurrent registration

Control: normalized phone unique constraint in MSSQL is the final guarantee; application existence checks are only an early user-friendly optimization.

### Credential stuffing / brute force

Controls:

- fixed password hashing cost;
- login failure counters and temporary locks;
- API rate limiting at the HTTP boundary;
- generic invalid-credential response.

### OTP brute force/replay

Controls:

- short TTL;
- five-attempt limit;
- resend cooldown;
- HMAC digest rather than raw OTP storage;
- atomic verify-and-consume Lua script;
- registration rate limiting at the API boundary.

### Refresh-token theft/replay

Controls:

- opaque random token;
- server stores only hash;
- single-use rotation;
- MSSQL compare-and-set rotation;
- reuse detection revokes session/token family;
- absolute session lifetime.

### JWT theft

Controls:

- ten-minute token lifetime;
- minimal claims;
- session identifier correlation;
- signing key stored outside source;
- server-side session revocation support for high-risk authorization paths.

### Log leakage

Controls:

- central masking/redaction requirement;
- structured logging fields rather than arbitrary request-body logging;
- explicit forbidden-sensitive-data list;
- security/QA tests will inspect generated log files for forbidden values.

## Remaining work

- dependency-injection/startup wiring for MSSQL, Redis and JWT settings;
- API rate limiting and authentication endpoint wiring;
- logout endpoint and authorization/session checks;
- authentication integration/security/concurrency tests against real MSSQL/Redis containers;
- centralized masked structured file logging.
