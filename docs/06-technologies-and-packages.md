# Technologies and NuGet Packages

## Technology baseline

| Technology | Version/Policy | Purpose |
|---|---|---|
| .NET | 8 | Runtime and application platform |
| ASP.NET Core | 8 | Main REST API and fake provider APIs |
| C# | 12 | Application language |
| MSSQL | Planned integration | Durable persistence and financial source of truth |
| Redis | Planned integration | Transient distributed state, OTP, fraud counters, idempotency hot cache and coordination |
| JWT | ASP.NET Core JwtBearer 8.0.29 | Short-lived access-token authentication without ASP.NET Core Identity |
| JSON Lines files | Project policy | Masked structured financial/application/audit logging |
| GitHub | Repository workflow | Source control, issues and pull requests |
| Codex | Development workflow | Agent-assisted implementation, review and maintenance |

## Package policy

FinWallet does not allow paid or freemium NuGet packages. Package selection follows this order:

1. .NET built-in capability.
2. Microsoft-maintained package when a package is required.
3. Fully free/open-source third-party package only when it provides clear value and its license is compatible with the project.

Every new package must be added through central package management in `Directory.Packages.props` and documented in this file before the feature is considered complete.

## Current NuGet inventory

### Microsoft.AspNetCore.Authentication.JwtBearer

| Field | Decision |
|---|---|
| Package | `Microsoft.AspNetCore.Authentication.JwtBearer` |
| Version | `8.0.29` |
| License | MIT |
| Owner project(s) | `FinWallet.Api`, `FinWallet.Infrastructure` |
| Purpose | ASP.NET Core JWT bearer validation in the API and supported JWT token primitives used by the Infrastructure token issuer |
| Why required | The Web API must validate signed bearer access tokens using the supported ASP.NET Core authentication stack without ASP.NET Core Identity, and Infrastructure must issue interoperable signed JWT access tokens without implementing the token standard manually. |
| Alternatives considered | Hand-written JWT parsing/signing/validation was rejected because implementing a security token standard manually adds unnecessary security risk. ASP.NET Core Identity was rejected by explicit project requirement. |
| Financial/security impact | Security-critical authentication dependency; version must remain on the supported .NET 8 patch line and be reviewed during dependency updates. |

The package is Microsoft-maintained, open source and MIT licensed. Version `8.0.29` is the current .NET 8 patch available when this package decision was made in August 2026.

## Password cryptography

Password derivation does not add a NuGet dependency. FinWallet uses the .NET `System.Security.Cryptography` one-shot `Rfc2898DeriveBytes.Pbkdf2` API with a fixed PBKDF2-HMAC-SHA512 version-1 scheme, 220,000 iterations, a 32-byte random salt and a 64-byte derived hash. These parameters are security code constants rather than runtime options. A persisted hash-version field exists only for safe future migration.

## Package approval record format

Every future package entry must include:

| Field | Required information |
|---|---|
| Package | Exact NuGet package ID |
| Version | Centrally managed version |
| License | SPDX/license name and compatibility note |
| Owner project(s) | Projects that reference the package |
| Purpose | Technical capability provided |
| Why required | Why built-in .NET functionality is insufficient |
| Alternatives considered | Native or free alternatives evaluated |
| Financial/security impact | Whether the dependency is on a critical path |

## Expected package areas requiring later decisions

The following capabilities will need explicit package decisions during implementation; no package is approved merely by appearing in this list:

- MSSQL client/data access
- Redis client
- structured file logging
- test frameworks and test infrastructure

Package versions and licenses must be verified at the time they are introduced because dependency metadata can change.
