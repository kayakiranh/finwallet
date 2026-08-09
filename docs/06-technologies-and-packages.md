# Technologies and NuGet Packages

## Technology baseline

| Technology | Version/Policy | Purpose |
|---|---|---|
| .NET | 8 | Runtime/application platform |
| ASP.NET Core | 8 | Controller-based Web APIs |
| C# | 12 | Application language |
| YARP | 2.3.0 | Reverse proxy, routing, health and load balancing |
| Swagger / Swashbuckle | 10.2.3 | Per-service OpenAPI/Swagger generation and UI |
| MSSQL | Microsoft.Data.SqlClient 7.0.2 | Durable persistence and financial source of truth |
| Redis | StackExchange.Redis 3.0.17 | Transient distributed OTP/support state |
| JWT | ASP.NET Core JwtBearer 8.0.29 | Gateway/API bearer authentication |
| xUnit v3 | 3.2.2 | Unit-test framework |
| Moq | 4.20.72 | Application-boundary mocks |
| GitHub | Repository workflow | Source control, issues, pull requests and CI |

## Package policy

FinWallet does not allow paid/freemium NuGet dependencies.

Selection order:

1. Built-in .NET/ASP.NET Core functionality.
2. Microsoft-maintained package where appropriate.
3. Fully free/open-source third-party package only when it has clear value and compatible licensing.

All versions are centrally pinned in `Directory.Packages.props`.

## Current package inventory

### Microsoft.AspNetCore.Authentication.JwtBearer — 8.0.29

- Purpose: JWT validation at Gateway/API.
- License: MIT.
- Used by: `FinWallet.Api`, `FinWallet.Gateway` and token infrastructure dependencies.
- Security impact: critical authentication path.
- Alternative rejected: hand-written JWT parsing/signing/validation.

### Microsoft.Data.SqlClient — 7.0.2

- Purpose: explicit async SQL, transactions and locking-sensitive financial persistence.
- License: MIT.
- Used by: `FinWallet.Infrastructure`.
- MSSQL remains the durable financial authority.
- SQL values must be parameterized.
- EF/generic repository abstractions are deliberately not added to the atomic money path.

### StackExchange.Redis — 3.0.17

- Purpose: Redis access for transient OTP/distributed support state with Lua/atomic primitives.
- License: MIT.
- Used by: `FinWallet.Infrastructure`.
- A single `ConnectionMultiplexer` is registered as singleton.
- Redis never becomes the wallet/ledger/idempotency financial source of truth.

### Yarp.ReverseProxy — 2.3.0

- Purpose: `FinWallet.Gateway` reverse proxy.
- License: MIT.
- Maintainer: Microsoft/dotnet project ecosystem.
- Capabilities used:
  - route matching;
  - route authorization policies;
  - request transforms;
  - load balancing;
  - active/passive health;
  - destination connection/timeout tuning;
  - provider path transforms.
- Why selected: native ASP.NET Core integration and configuration-driven routing without introducing a commercial gateway dependency.
- Financial impact: not a financial source of truth; gateway outages affect availability but cannot alter ledger correctness.

### Swashbuckle.AspNetCore — 10.2.3

- Purpose: Swagger/OpenAPI generation and UI for every Web API.
- License: MIT.
- Owned through: `FinWallet.Shared.Web`.
- Swagger is enabled by default in local development and disabled by default in production configuration.
- API authorization remains enforced independently from documentation visibility.

### xunit.v3 — 3.2.2

- Purpose: unit tests.
- License: Apache-2.0.
- Used by: `FinWallet.Application.Tests`.
- Test project uses Microsoft Testing Platform integration for .NET 8.
- This replaces the previous state where the repository had no test project.

### Moq — 4.20.72

- Purpose: strict mocks for Application orchestration boundaries.
- License: BSD-3-Clause.
- Used by: `FinWallet.Application.Tests`.
- Appropriate for verifying call ordering/avoidance such as "provider must not be called when owned wallet is absent".
- Not used as a substitute for real MSSQL/Redis/YARP concurrency tests.

## Framework-only capabilities

The following features intentionally do not add separate NuGet packages:

- Kestrel HTTP limits;
- ASP.NET Core rate limiting;
- CORS;
- security headers middleware;
- `HttpClientFactory` and `SocketsHttpHandler`;
- PBKDF2-HMAC-SHA512 via `System.Security.Cryptography`;
- HMAC/SHA-256 primitives;
- cancellation tokens and `TimeProvider`.

## Password cryptography note

PBKDF2 parameters remain part of credential hash version 1 rather than a loose runtime tuning switch. Existing rows do not persist per-password iteration metadata, so changing the work factor only in appsettings would invalidate existing password verification. A future work-factor increase must use a versioned migration/rehash design.

## Package approval record

Any future package addition must document:

| Field | Required information |
|---|---|
| Package | Exact package ID |
| Version | Centrally pinned version |
| License | License and compatibility |
| Owner project(s) | Projects referencing it |
| Purpose | Capability provided |
| Why required | Why built-in .NET is insufficient |
| Alternatives | Native/free alternatives considered |
| Financial/security impact | Critical-path implications |

## Next dependency work

No package is pre-approved merely because it is useful. Future candidates such as OpenTelemetry exporters, structured logging providers, containerized integration-test infrastructure or SBOM/vulnerability tooling must go through the same review.
