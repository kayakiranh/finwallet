# Agent and Codex Development Workflow

## Purpose

This document defines how FinWallet development is delegated, reviewed and merged when Codex or other coding agents are used. The repository, `AGENTS.md`, architecture documents, ADRs and GitHub issues remain the source of truth; agent chat history is not a substitute for repository documentation.

## Operating model

FinWallet uses issue-driven, branch-isolated development. Each agent receives one bounded responsibility, reads the repository rules first, works on an isolated branch/worktree, produces small commits, validates its scope, and opens a reviewable pull request.

Recommended roles:

1. Solution Architect
2. Financial Domain Agent
3. Security/Auth Agent
4. Integration Agent
5. Persistence/Concurrency Agent
6. QA/Chaos Agent
7. Code Review Agent
8. Documentation Agent

## Codex execution model

Codex can work through the Codex app, CLI, IDE integration or cloud execution. For this project the preferred mode is repository-scoped work with isolated tasks. When multiple agents run in parallel, each task must use an isolated worktree/branch so that unrelated changes do not share a working directory.

Official OpenAI references:

- Codex overview: https://openai.com/codex/
- Codex app and multi-agent/worktree model: https://openai.com/index/introducing-the-codex-app/
- Using Codex with ChatGPT plans: https://help.openai.com/en/articles/11369540-using-codex-with-your-chatgpt-plan

## Source-of-truth order

When instructions conflict, use this order:

1. Explicit current task/issue acceptance criteria
2. `AGENTS.md`
3. Master specification
4. Accepted ADRs
5. Architecture/security/database/API documents
6. Existing implementation conventions
7. Agent assumptions

An agent must not silently override a higher-level rule.

## Before an agent starts

The agent must:

1. Read `AGENTS.md`.
2. Read the assigned GitHub issue.
3. Read relevant ADRs and documents.
4. Inspect affected modules and tests.
5. Identify dependencies on other open tasks.
6. Stop scope expansion unless required for correctness.

## Branch and worktree model

Branch names use:

```text
agent/<bounded-task-name>
```

Examples:

```text
agent/security-auth
agent/persistence-ledger
agent/fake-bank
agent/wallet-transfer
```

Parallel agents must not intentionally modify the same high-conflict files unless the issue explicitly coordinates that work.

## Commit policy

Commits must be small and cohesive. A commit should represent one technical idea that can be reviewed independently.

Preferred examples:

```text
docs: require bilingual XML documentation
chore: add solution build rules
domain: add currency value object
domain: add money value object
auth: add customer credential model
```

Avoid commits such as:

```text
implement everything
fix stuff
large update
```

Do not mix unrelated refactoring, documentation, package changes and feature behavior in one commit when they can reasonably be separated.

## Coding agent responsibilities

A coding agent must:

- preserve financial invariants;
- add TR/EN XML documentation to every class/interface/method/property and other documented declarations;
- avoid paid/freemium dependencies;
- update package inventory for every new package;
- add or update tests for changed behavior;
- update API/architecture/database/security documentation when contracts change;
- avoid external HTTP calls inside SQL transactions;
- preserve idempotency and concurrency guarantees;
- never make Redis the financial source of truth.

## Review agent responsibilities

The review agent does not assume the implementation is correct. It checks:

- architecture boundary violations;
- financial correctness;
- ledger balancing;
- lost-update/double-spend risks;
- idempotency gaps;
- duplicate callback handling;
- security/token/OTP weaknesses;
- sensitive logging leakage;
- external integration retry/timeout mistakes;
- missing cancellation propagation;
- unnecessary abstractions;
- undocumented NuGet packages;
- missing TR/EN XML documentation.

Review outcomes are:

- PASS
- PASS WITH ISSUES
- FAIL

## QA/Chaos agent responsibilities

The QA/Chaos agent actively tries to break the system. It should create or execute scenarios such as:

- simultaneous overspend attempts;
- repeated idempotency keys;
- same key with altered payload;
- duplicate bank callbacks;
- Redis outage;
- provider timeout/500/slow response;
- fraud provider unavailable;
- campaign provider unavailable after a discounted price was shown;
- cutoff provider unavailable;
- notification provider failure after financial commit;
- repeated refund/reversal;
- ledger mismatch;
- reconciliation mismatch;
- refresh token replay;
- OTP brute force.

## Pull request flow

1. Agent finishes a bounded issue on its branch/worktree.
2. Agent runs relevant build/tests/format/static checks.
3. Agent reviews its own diff.
4. Agent updates documentation.
5. Agent opens a draft PR.
6. Independent review agent inspects the PR.
7. Findings are fixed with additional small commits.
8. CI must pass.
9. Financial/security-sensitive PRs receive a final architecture review.
10. PR is merged only after acceptance criteria are satisfied.

## Codex tasks vs automations vs skills

### Task/thread
Use for a bounded engineering objective, such as implementing wallet creation or reviewing a pull request.

### Parallel agents
Use when tasks have stable contracts and low file overlap, for example implementing separate fake provider APIs after their contracts are frozen.

### Skills
Use reusable skills for repeatable workflows such as code review, CI diagnosis, package-license checks or documentation verification. Skills should encode process, not replace architecture decisions.

### Automations
Use automations only for recurring work with a clear cadence or trigger, such as periodic issue triage or recurring CI/repository checks. Feature implementation should remain issue/task driven rather than being hidden in an automation.

## Coordination gates

Parallel implementation is allowed only after these are stable enough:

- domain boundaries;
- database/ledger rules;
- external API contracts;
- authentication rules;
- error/idempotency conventions.

Before those gates, architecture agents may work in parallel on documents, but feature agents should not independently invent incompatible contracts.

## Failure handling

If an agent cannot validate a change because a dependency/environment is unavailable, it must record exactly what was and was not validated. It must not claim tests passed without execution.

If two agent branches conflict semantically, resolve the conflict against `AGENTS.md`, ADRs and the master specification rather than choosing the newest implementation automatically.

## Definition of Done for agent-generated code

A task is complete only when:

- acceptance criteria are met;
- code builds in the intended environment;
- relevant tests pass;
- financial/security invariants remain valid;
- TR/EN XML documentation is complete;
- package inventory is current;
- affected technical/API documentation is current;
- review findings are resolved or explicitly accepted;
- commits remain understandable and scoped.
