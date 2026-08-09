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
- 07 - Database
- 08 - Financial Flows
- [09 - Security](09-security.md)
- 10 - Testing
- [11 - Code Documentation Standard](11-code-documentation-standard.md)
- [12 - Agent and Codex Workflow](12-agent-codex-workflow.md)
- [13 - Project Management and Delivery Roadmap](13-project-management.md)
- Final Technical Review — created during release hardening.

## Architecture Decision Records

Architecture decisions live under `docs/adr/` and must include context, decision, alternatives and consequences.

## Delivery ownership

GitHub issues are the authoritative backlog. Phase/agent/status labels emulate the target Project-board state until a GitHub Project board and milestone objects are attached.

Current implementation epics:

- Issue #8 — Registration, authentication and session security.
- Issue #9 — MSSQL, Redis, idempotency and persistence.
- Issue #10 — Double-entry ledger and transaction engine.
- Issue #11 — Fake provider APIs and resilience.
- Issue #12 — Financial flows and campaign/bank workflows.
- Issue #13 — Reconciliation, masked logging and operations.
- Issue #14 — Chaos/security/concurrency/release hardening.

## Documentation ownership

Documentation is continuously updated during implementation. A feature is incomplete if its API, persistence, integration, package, security or architecture changes are not reflected here.
