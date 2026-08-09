# Gateway, Swagger and Platform Security

## Topology

All client traffic enters through `FinWallet.Gateway`, implemented with YARP.

```text
Client
  |
  v
FinWallet.Gateway :8080
  |-- JWT / anonymous-auth route policy
  |-- rate limit
  |-- request/header/body limits
  |-- CORS
  |-- security headers
  |-- active/passive health
  |-- load balancing
  |
  +--> FinWallet.Api :8081
          |
          +--> Gateway /providers/bank/* ----------> FakeBank.Api :8082
          +--> Gateway /providers/fraud/* ---------> FakeFraud.Api :8083
          +--> Gateway /providers/cutoff/* --------> FakeCutoff.Api :8084
          +--> Gateway /providers/campaign/* ------> FakeCampaign.Api :8085
          +--> Gateway /providers/communication/* -> FakeCommunication.Api :8086
```

FinWallet does not call provider simulators directly. Provider base URLs point back to the Gateway.

## Authentication boundaries

### Customer traffic

Only the following routes are anonymous at the Gateway:

- `POST /api/v1/auth/register`;
- `POST /api/v1/auth/registration/verify`;
- `POST /api/v1/auth/login`;
- `POST /api/v1/auth/refresh`.

Other `/api/*` routes require the `GatewayAuthenticated` policy and therefore a valid JWT before proxying.

FinWallet.Api still validates the JWT. Gateway authentication is an outer security layer, not a replacement for service authorization and ownership checks.

### FinWallet -> provider traffic

FinWallet.Api sends `X-Internal-Service-Key` to the Gateway for `/providers/*`. The Gateway validates this with the `InternalService` policy.

The Gateway then replaces that header on the proxy request with a separate `DownstreamServiceKey`. This creates two trust boundaries:

1. internal caller -> Gateway;
2. Gateway -> destination service.

FinWallet.Api and simulator business endpoints require the downstream key. A caller that bypasses the Gateway and calls a pod/service directly therefore receives HTTP 401.

Health endpoints remain accessible for orchestrator/YARP health checks. Swagger exposure is controlled separately through configuration.

## YARP routing and load balancing

Default development configuration contains one destination per cluster, but every cluster is configured with `PowerOfTwoChoices`. Production may add additional destinations through YARP configuration/environment overrides without changing application code.

Critical clusters use active health checks. FinWallet also enables passive transport-failure health handling. Important YARP transport parameters are configuration-driven:

- load-balancing policy;
- destination addresses;
- active-health interval and timeout;
- passive-health reactivation period;
- `MaxConnectionsPerServer`;
- activity timeout;
- HTTP version policy;
- response buffering.

## Rate limiting and resource exhaustion

Application-level rate limiting protects against abusive L7 traffic and accidental overload. It does not claim to stop volumetric network DDoS by itself.

Controls in this repository:

- Gateway per-IP fixed-window rate limit;
- backend second-layer rate limit;
- zero queue by default to avoid turning overload into memory pressure;
- maximum request body size;
- maximum request header count;
- maximum total header bytes;
- request-header timeout;
- keep-alive timeout;
- maximum concurrent connections;
- provider-specific YARP request body limits;
- provider HTTP timeouts;
- bounded DB/Redis connection behavior.

Production volumetric DDoS still requires infrastructure controls such as cloud/edge DDoS protection, ingress/load-balancer limits and, when exposed to the public internet, WAF/CDN/bot controls.

## Swagger

Every Web API project references `FinWallet.Shared.Web` and calls `AddFinWalletWebPlatform` / `UseFinWalletWebPlatform`.

Swagger/OpenAPI generation is therefore consistently available to:

- Gateway;
- FinWallet.Api;
- FakeBank.Api;
- FakeFraud.Api;
- FakeCutoff.Api;
- FakeCampaign.Api;
- FakeCommunication.Api.

Development configuration enables Swagger. Production configuration disables it by default. Production teams may explicitly enable it only on an authenticated/internal network.

Swagger does not change runtime authorization. A documented endpoint still requires JWT/internal credentials exactly like a non-Swagger request.

## Shared HTTP security baseline

`FinWallet.Shared.Web` applies the following controls:

- removes the Kestrel `Server` response header;
- blocks TRACE and CONNECT;
- requires JSON for body-bearing POST/PUT/PATCH operations;
- validates or regenerates bounded correlation IDs;
- no-store/no-cache response policy;
- `X-Content-Type-Options: nosniff`;
- `X-Frame-Options: DENY`;
- strict referrer policy;
- restrictive permissions policy;
- cross-origin resource policy;
- restrictive CSP for API responses;
- a Swagger-specific CSP;
- explicit CORS origin allow-list;
- rate limiting;
- request/header/body/connection bounds.

## OWASP mapping

The platform baseline is designed against the current OWASP Top 10 web application categories and OWASP API Security risks.

### Broken Access Control / API BOLA / Broken Function Authorization

Controls:

- JWT validation at Gateway and API;
- owner-aware SQL queries for customer financial resources;
- server-derived `sub`/`sid` ownership;
- provider routes restricted to internal service credentials;
- direct-backend bypass protection;
- no client-supplied customer ownership identifiers for authorization decisions.

### Security Misconfiguration

Controls:

- prod secrets default empty and startup fails without secret injection;
- Swagger off by default in production;
- CORS allow-list rather than wildcard credentials;
- bounded HTTP limits;
- disabled server header;
- restrictive headers and CSP;
- separate development and production configuration.

### Software Supply Chain Failures

Controls:

- central NuGet version management;
- only explicit dependencies;
- Microsoft-maintained YARP;
- package inventory documentation;
- CI restore/build/test;
- warnings as errors.

Dependency scanning and signed-artifact/SBOM enforcement remain deployment-pipeline work.

### Cryptographic Failures

Controls:

- PBKDF2-HMAC-SHA512 password hashing;
- cryptographic salts;
- constant-time comparison;
- HMAC-based OTP digest;
- short JWT lifetime;
- secrets outside production source configuration;
- no credential/token logging.

### Injection

Controls:

- parameterized SqlClient commands;
- no dynamic user-provided SQL fragments in financial paths;
- typed DTO binding;
- bounded content type/body rules;
- output returned as JSON rather than templated HTML.

### Insecure Design / Sensitive Business Flows

Controls:

- double-entry ledger;
- durable idempotency;
- server-side fraud signals;
- external fraud fail-closed;
- transaction ownership checks;
- blocked/available balance separation for future external settlement flows;
- no trust in client-supplied risk flags.

### Authentication Failures

Controls:

- Gateway JWT validation;
- API JWT validation;
- server-side sessions;
- refresh-token rotation/reuse detection;
- login lockout;
- OTP attempt limits;
- rate limits.

### Software/Data Integrity Failures

Controls:

- MSSQL constraints/FKs/unique keys;
- append-only ledger concept;
- reversal instead of mutation;
- atomic financial SQL transaction;
- deterministic idempotency fingerprints.

### Logging and Alerting Failures

The codebase defines sensitive-data masking requirements and correlation IDs. Full centralized alerting/SIEM integration is still deployment/operations work. Raw credentials, OTPs, JWTs, refresh tokens, signing keys and connection secrets must never be logged.

### Mishandling Exceptional Conditions

Controls:

- centralized `ServiceResult` exception mapping;
- provider timeouts;
- external fraud fail-closed;
- SQL transaction rollback;
- cancellation-token propagation;
- YARP active/passive health behavior;
- startup configuration validation.

### API Unrestricted Resource Consumption

Controls are the layered rate, body, header, connection and timeout limits described above.

### API SSRF / Unsafe Consumption of APIs

Provider destinations are server-owned configuration values; clients cannot select arbitrary provider URLs. External provider DTOs are contained behind anti-corruption adapters and returned provider identity/currency state is validated before durable FinWallet state changes.

## Configuration model

Configuration uses standard .NET precedence:

1. `appsettings.json`;
2. `appsettings.{Environment}.json`;
3. environment variables / deployment secret injection;
4. explicit runtime providers if later added.

Operations may tune network/pool/timeouts/rate thresholds without rebuilding. Cryptographic algorithms, double-entry invariants and persistence semantics remain code-level constraints rather than insecure runtime switches.
