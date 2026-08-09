# Technologies and NuGet Packages

## Technology baseline

| Technology | Version/Policy | Purpose |
|---|---|---|
| .NET | 8 | Runtime and application platform |
| ASP.NET Core | 8 | Main REST API and fake provider APIs |
| C# | 12 | Application language |
| MSSQL | Microsoft.Data.SqlClient 7.0.2 | Durable persistence and financial source of truth |
| Redis | StackExchange.Redis 3.0.17 | Transient distributed state, OTP, fraud counters, idempotency hot cache and coordination |
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

### Microsoft.Data.SqlClient

| Field | Decision |
|---|---|
| Package | `Microsoft.Data.SqlClient` |
| Version | `7.0.2` |
| License | MIT |
| Owner project(s) | `FinWallet.Infrastructure` |
| Purpose | Official SQL Server provider used for explicit async SQL commands, transactions and concurrency-sensitive persistence. |
| Why required | MSSQL is the durable financial source of truth and requires a supported SQL Server protocol/provider. |
| Alternatives considered | EF Core was not selected for the first persistence slice because auth/financial concurrency SQL should remain explicit. Dapper was not added because the initial store does not require an additional mapping abstraction over `Microsoft.Data.SqlClient`. |
| Financial/security impact | Critical persistence dependency. SQL parameters are mandatory and transaction boundaries remain explicit. |

`Microsoft.Data.SqlClient 7.0.2` is Microsoft-maintained, MIT licensed and targets .NET 8.

### StackExchange.Redis

| Field | Decision |
|---|---|
| Package | `StackExchange.Redis` |
| Version | `3.0.17` |
| License | MIT |
| Owner project(s) | `FinWallet.Api`, `FinWallet.Infrastructure` |
| Purpose | Redis access for OTP state, velocity counters, hot idempotency coordination and other transient distributed state requiring atomic Redis operations; the API project references it only in the composition root to register the shared connection multiplexer. |
| Why required | The built-in distributed-cache abstraction does not expose the atomic compare/script primitives required by the planned OTP and concurrency-sensitive transient workflows. |
| Alternatives considered | `IDistributedCache` alone was rejected for these operations because it intentionally exposes a simpler cache abstraction. Redis remains optional for financial correctness; durable money state is never stored here. |
| Financial/security impact | Security-sensitive for OTP and rate/velocity state, but never the financial source of truth. Redis unavailability must fail safely for authentication/fraud operations that depend on it. |

`StackExchange.Redis 3.0.17` is MIT licensed and compatible with .NET 8.

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

The following capabilities still require explicit package decisions during implementation; no package is approved merely by appearing in this list:

- structured file logging
- test frameworks and test infrastructure

Package versions and licenses must be verified at the time they are introduced because dependency metadata can change.
