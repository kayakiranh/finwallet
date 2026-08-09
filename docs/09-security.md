# Security Design

## Security principles

FinWallet prioritizes financial correctness, least privilege and credential safety over convenience.

- All normal HTTP traffic passes through YARP Gateway.
- Gateway authentication does not replace service/domain authorization.
- MSSQL is the durable authentication/financial source of truth.
- Redis is transient support only.
- Secrets are not committed in production configuration.
- Production starts fail-fast when required secrets are absent.
- Passwords, OTPs, JWTs, refresh tokens, service keys and connection secrets must never be logged.
- Financial ownership/risk facts are server-derived, not trusted from client request flags.
- Cryptographic algorithms and accounting invariants are not arbitrary runtime toggles.

## Gateway trust model

### Public/customer boundary

YARP Gateway is the public application entry point.

Anonymous routes are limited to registration verification/login/refresh endpoints. Other `/api/*` routes require a valid JWT at the Gateway before a request is proxied.

FinWallet.Api validates the JWT again and high-risk financial flows validate server-side session state (`sid`) against MSSQL.

### Internal provider boundary

FinWallet.Api calls provider simulators through Gateway `/providers/*` routes with an `InternalServiceKey`.

Gateway validates the caller key, then replaces it on the downstream proxy request with a distinct `DownstreamServiceKey`.

FinWallet.Api and simulator business endpoints require the downstream key. This prevents a direct call to a backend service from being treated as equivalent to traffic that passed through the Gateway.

Health endpoints are exempt so the gateway/orchestrator can check liveness. Swagger exposure is separately controlled by environment configuration.

## HTTP attack-surface controls

The shared `FinWallet.Shared.Web` baseline applies:

- Kestrel server header disabled;
- TRACE/CONNECT blocked;
- body-bearing POST/PUT/PATCH restricted to JSON;
- body-size limits;
- header-count and total-header-size limits;
- request-header timeout;
- keep-alive timeout;
- maximum concurrent connection limit;
- per-IP fixed-window rate limiting;
- zero queue by default;
- explicit CORS origin allow-list;
- validated/bounded correlation ID;
- no-store/no-cache responses;
- restrictive CSP;
- clickjacking protection;
- MIME-sniffing protection;
- restrictive referrer/permissions/cross-origin policies.

These controls reduce L7 abuse/resource exhaustion. They are not a replacement for volumetric DDoS protection at cloud/edge/ingress/WAF layers.

## Registration country policy

Registration uses an explicit allow-list. The selected country and normalized phone calling code must agree. Normalized phone is unique in MSSQL, so concurrent registrations cannot create duplicate customers even if both pass an application pre-check.

## Password policy and storage

Passwords are never stored raw.

Current V1 credential scheme:

- PBKDF2-HMAC-SHA512;
- 220,000 iterations;
- 32-byte random salt;
- 64-byte derived hash;
- constant-time comparison;
- persisted hash version.

The work factor is intentionally not a simple appsettings switch. The current credential schema stores a hash version, not per-credential iteration metadata. Changing the iteration count at runtime would make existing hashes unverifiable. Future work-factor changes require a versioned migration/rehash strategy.

## JWT access tokens

JWTs:

- use fixed HMAC-SHA256 signing;
- contain only minimal subject/session/JTI/issued-at claims;
- do not contain balance/IBAN/contact data;
- use issuer/audience/signing key from configuration/secret store;
- use an access-token lifetime configurable only within a safe 2-30 minute range;
- use bounded configurable validation clock skew.

The signing algorithm itself is not configurable.

## Sessions and refresh tokens

A login creates a server-side device session. Refresh tokens are opaque random values; only their deterministic hash is persisted.

Rotation uses MSSQL compare-and-set semantics. Reuse of an already consumed refresh token revokes the associated session/token family.

High-risk money flows validate the `sid` claim against durable session state, so a valid JWT alone is not sufficient after the server-side session has been revoked.

## OTP in Redis

Registration OTP state is transient and stored in Redis.

- raw OTP is not stored in Redis;
- digest uses HMAC with deployment pepper;
- issue/verify operations use Lua for atomic state changes;
- verification fails closed when Redis is unavailable;
- Redis cannot independently activate a customer without durable MSSQL state.

## SQL injection and data access

Financial/authentication persistence uses explicit parameterized `Microsoft.Data.SqlClient` commands. User values are not concatenated into SQL in financial paths.

Dynamic SQL is only acceptable when the dynamic fragment is a fixed code-owned constant, never request text.

## Financial authorization

Ownership is enforced server-side:

- JWT subject determines current customer;
- queries include customer ownership when loading owned wallets/bank accounts;
- another customer's wallet is not treated as an authorized resource merely because its GUID is known;
- transfer source ownership is validated before fraud and again inside locked posting SQL;
- destination/currency/lifecycle are revalidated inside the atomic posting transaction.

This is a direct control against BOLA/IDOR-style failures.

## Fraud security

Transfer clients do not submit trust flags such as `isNewDevice`, `knownBeneficiary`, velocity or 24-hour amount.

Signals are derived from server-side session/customer/transaction state.

Processing order:

```text
completed idempotency replay
-> server-side risk signals
-> internal fraud rules
-> external fraud provider
-> combined decision
-> atomic posting only on Allow
```

External fraud timeout/network/malformed response fails closed. No money is posted when the required fraud decision is unavailable.

## Idempotency and replay protection

Money-changing transfer requests require `Idempotency-Key`.

Durable identity:

```text
Scope + CustomerId + IdempotencyKey
```

A canonical request hash prevents the same key being reused with a different transfer payload.

Completed replay returns the existing immutable transaction rather than executing the financial effect again.

## Ledger/data integrity

Wallet balance is not the only financial truth. Every posted transfer is represented in double-entry accounting.

Important invariants:

- positive bounded amounts;
- currency consistency;
- debit = credit;
- one financial SQL transaction for balances/transaction/ledger/idempotency;
- reversal creates a new journal instead of mutating history;
- MSSQL FKs/unique/check constraints backstop Application validation.

## Sensitive logging policy

Never log:

- password or password hash/salt;
- OTP, OTP digest or pepper;
- JWT access token;
- refresh token/hash;
- Authorization header;
- internal/downstream service keys;
- JWT signing key;
- SQL/Redis connection strings with credentials;
- unmasked phone/email/IBAN/account numbers.

Correlation IDs are allowed only after format/length validation and must not contain PII.

## OWASP Top 10 / API Security mapping

The detailed platform mapping is maintained in `15-gateway-platform-security.md`. High-level coverage includes:

- Broken Access Control / BOLA: gateway + service auth, owner-aware SQL, server-derived customer identity.
- Security Misconfiguration: prod Swagger off, empty secret defaults, bounded HTTP configuration, no server header.
- Supply Chain: central NuGet versions, explicit dependency inventory, CI build/test.
- Cryptographic Failures: PBKDF2, random salts, HMAC OTP digest, bounded JWT lifetime, secret-store requirements.
- Injection: parameterized SQL and typed DTOs.
- Insecure Design / Sensitive Business Flows: ledger, durable idempotency, fraud, transaction boundaries.
- Authentication Failures: JWT/session/refresh rotation/login lockout/OTP/rate limits.
- Data/Software Integrity: DB constraints, append-only/reversal accounting, atomic posting.
- Logging/Alerting: sensitive-data policy and correlation; centralized alerting remains an operations integration.
- Exceptional Conditions: fail-closed providers, central errors, cancellation, rollback, health checks.
- Unrestricted Resource Consumption: rate/body/header/connection/timeout limits.
- SSRF/Unsafe API Consumption: provider destinations are server configuration; provider DTO/state is validated behind adapters.

## Production perimeter requirement

Application controls do not absorb a large volumetric DDoS attack. Public production deployment should additionally provide infrastructure-layer protections appropriate to the hosting platform, such as:

- managed DDoS protection;
- L4/L7 load balancer limits;
- ingress/network policies;
- WAF rules;
- bot/abuse controls where relevant;
- TLS termination and certificate rotation;
- network segmentation so backend services are not publicly routable.

## Remaining security work

- integration tests proving gateway bypass denial and rate limits;
- dependency vulnerability/SBOM scanning in CI;
- centralized structured masked logging + alerting/SIEM;
- infrastructure NetworkPolicy/ingress manifests;
- logout/session revoke endpoint completion;
- durable FraudEvents/manual review;
- real reconciliation and incident runbooks.
