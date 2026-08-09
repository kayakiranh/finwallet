# FinWallet Delivery Roadmap

## Purpose

This document defines how the repository is managed as a software-team backlog. GitHub issues are the authoritative work items; implementation happens on `agent/*` branches and is integrated through pull requests.

## Board model

The intended GitHub Project columns are:

1. Backlog
2. Ready
3. In Progress
4. In Review
5. Blocked
6. Done

Until a GitHub Project board is attached, equivalent `status:*` labels are used on issues so the backlog can be migrated without changing issue semantics.

## Milestone plan

| Milestone | Scope | Primary agent |
|---|---|---|
| M0 — Architecture Baseline | Architecture, contracts, DB/security design and governance docs | Solution Architect |
| M1 — Foundation | Solution structure, customer/wallet primitives and documentation rules | Foundation / Financial Domain |
| M2 — Identity & Registration | Country/phone registration policy, OTP, password security, JWT, refresh/session lifecycle | Security/Auth |
| M3 — Persistence & Concurrency | MSSQL, Redis, durable idempotency, outbox/inbox and concurrency controls | Persistence/Concurrency |
| M4 — Financial Core | Double-entry ledger, transaction state machine, reversal and accounting rules | Financial Domain |
| M5 — External Providers | Bank, Fraud, Cutoff, Campaign and Communication simulators plus adapters | Integration |
| M6 — Financial Flows | Deposit, withdrawal, wallet transfer, purchase, campaign accounting, refund/reversal | Financial Domain + Integration |
| M7 — Reconciliation & Hardening | Reconciliation, structured masked logging, chaos/concurrency/security testing | QA/Chaos + Review |

## Current epics

- Phase 0 issues: architecture, integrations, persistence design, security design and documentation governance.
- Phase 1: foundation and wallet domain — completed by PR #7.
- Phase 2: registration, authentication and session security — Issue #8, current implementation phase.
- Phase 3: MSSQL, Redis, idempotency and persistence — Issue #9.
- Phase 4: double-entry ledger and transaction engine — Issue #10.
- Phase 5: fake provider APIs and resilience contracts — Issue #11.

## Team roles

### Solution Architect
Owns architectural boundaries, ADRs, dependency direction and cross-cutting design decisions.

### Security/Auth Agent
Owns registration, password security, JWT, refresh token/session lifecycle, OTP and authentication threat controls.

### Financial Domain Agent
Owns wallet invariants, ledger correctness, transaction state, reversal and accounting behavior.

### Persistence/Concurrency Agent
Owns MSSQL/Redis persistence, transaction boundaries, atomic updates, idempotency, outbox/inbox and concurrency guarantees.

### Integration Agent
Owns external simulator contracts, adapters, anti-corruption mappings, correlation, timeout/retry/circuit-breaker behavior and callbacks.

### QA/Chaos Agent
Attempts to break financial correctness through concurrency, duplication, provider failure, timeout, corrupted workflow and reconciliation scenarios.

### Code Review Agent
Reviews architecture, security, financial correctness, data consistency, package policy, XML documentation and over-engineering.

### Documentation Agent
Keeps technical analysis, architecture, APIs, packages, security, database, financial flows and operating procedures synchronized with code.

## Pull request workflow

1. Epic/feature issue exists before implementation.
2. Agent branch is created from current `main`.
3. Changes are committed in small cohesive commits.
4. Documentation and tests are part of the same feature scope.
5. Draft PR is opened early enough for review visibility.
6. Review findings are corrected through additional small commits.
7. PR is merged only when Definition of Done is met.
8. The linked issue moves to Done/closed.

## Commit policy

A commit should normally represent one understandable change such as one domain concept, one application contract, one persistence capability, one endpoint, one test group, or one documentation update. Large mixed commits are rejected during review.

## Status labels

- `status:backlog`
- `status:ready`
- `status:in-progress`
- `status:review`
- `status:blocked`
- `status:done`

## Agent labels

- `agent:architecture`
- `agent:security-auth`
- `agent:financial-domain`
- `agent:persistence`
- `agent:integrations`
- `agent:qa-chaos`
- `agent:review`
- `agent:documentation`

## Definition of Done

A work item is complete only when code, TR/EN XML documentation, relevant tests, API/architecture/package documentation, security implications and failure behavior are all addressed. Financial features additionally require explicit concurrency, idempotency and reconciliation consideration.