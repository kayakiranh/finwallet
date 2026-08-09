# Technologies and NuGet Packages

## Technology baseline

| Technology | Version/Policy | Purpose |
|---|---|---|
| .NET | 8 | Runtime and application platform |
| ASP.NET Core | 8 | Main REST API and fake provider APIs |
| C# | 12 | Application language |
| MSSQL | Planned integration | Durable persistence and financial source of truth |
| Redis | Planned integration | Transient distributed state, OTP, fraud counters, idempotency hot cache and coordination |
| JWT | Built-in ASP.NET Core authentication stack planned | Access-token authentication without ASP.NET Core Identity |
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

No explicit third-party or additional Microsoft NuGet package has been added in the foundation branch yet. Current projects rely only on the SDK/framework references provided by `Microsoft.NET.Sdk` and `Microsoft.NET.Sdk.Web`.

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
- JWT bearer authentication if an explicit package reference is required by the project setup
- test frameworks and test infrastructure

Package versions and licenses must be verified at the time they are introduced because dependency metadata can change.
