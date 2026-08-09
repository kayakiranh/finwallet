# FinWallet Documentation Index

This directory is part of the product Definition of Done. Documents must evolve with the codebase.

## Core documents

- [00 - Master Specification](00-master-specification.md)
- 01 - Technical Analysis
- 02 - Architecture
- 03 - Design Patterns
- [04 - API Guide](04-api-guide.md)
- [05 - External Integrations](05-external-integrations.md)
- [06 - Technologies and Packages](06-technologies-and-packages.md)
- [07 - Database](07-database.md)
- 08 - Financial Flows
- [09 - Security](09-security.md)
- [10 - Testing](10-testing.md)
- [11 - Code Documentation Standard](11-code-documentation-standard.md)
- [12 - Agent and Codex Workflow](12-agent-codex-workflow.md)
- [13 - Project Management and Delivery Roadmap](13-project-management.md)
- [14 - Wallet Transfer](14-wallet-transfer.md)
- [15 - Gateway, Swagger and Platform Security](15-gateway-platform-security.md)
- [16 - First Run Happy Path: Registration to Wallet Transfer](16-happy-path-onboarding.md)
- [17 - AI-Assisted Architecture Decision Narrative](17-ai-architecture-decisions.md)
- [18 - MSSQL, Redis, HTTP and Gateway Performance Review](18-performance-review.md)
- Final Technical Review — created during release hardening.

## Recommended reading order for a new engineer

1. `00-master-specification.md` — product scope/invariants.
2. `17-ai-architecture-decisions.md` — why the architecture evolved this way.
3. `15-gateway-platform-security.md` — current runtime topology and trust boundaries.
4. `04-api-guide.md` — endpoint conventions.
5. `16-happy-path-onboarding.md` — executable API call order.
6. `07-database.md` — durable state/ledger constraints.
7. `09-security.md` — security model.
8. `10-testing.md` — mocks versus real-infrastructure tests.
9. `18-performance-review.md` — tuning and benchmark guidance.

## Current runtime topology

Normal client and service-to-service HTTP traffic is routed through `FinWallet.Gateway`.

```text
Client -> YARP Gateway -> FinWallet.Api
                        -> Fake provider APIs (internal routes only)

FinWallet.Api -> YARP Gateway -> Fake provider APIs
```

Gateway and destination services use separate internal trust keys so direct backend access is not treated as equivalent to proxied traffic.

## Architecture Decision Records

Architecture decisions live under `docs/adr/` and must include context, decision, alternatives and consequences. The AI-assisted narrative is a higher-level chronological explanation; ADRs remain authoritative for individual decisions.

## Delivery ownership

GitHub issues are the authoritative backlog. Current implementation areas include authentication/session security, MSSQL/Redis persistence, ledger/transaction engine, fake providers, gateway/platform security, financial flows, reconciliation and release hardening.

## Documentation ownership

Documentation is continuously updated during implementation. A feature is incomplete if its API, persistence, integration, package, security, runtime configuration or architecture changes are not reflected here.
