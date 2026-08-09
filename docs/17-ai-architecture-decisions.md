# AI-Assisted Architecture Decision Narrative

## Purpose

This document explains the engineering rationale used while designing and implementing FinWallet with AI assistance. It is not a transcript of private model reasoning. It records the externally reviewable architecture decisions, alternatives, constraints, trade-offs and implementation order that resulted in the current repository.

## 1. Starting problem

The target side project was intentionally financial rather than a generic CRUD application. The selected combination was:

```text
Digital Wallet
+ Double-entry Ledger
+ Fraud Detection
+ External Banking Integration
```

The goal was to demonstrate the problems that make financial backends difficult:

- money correctness;
- concurrency;
- idempotency;
- external provider failures;
- account lifecycle;
- authentication/session security;
- fraud decisions;
- auditability;
- reconciliation;
- infrastructure resilience.

A project that only stores `Balance += x` would not exercise those concerns.

## 2. Why modular monolith first

The first architecture decision was to avoid immediately splitting the system into many microservices.

Initial shape:

```text
FinWallet.Api
FinWallet.Application
FinWallet.Domain
FinWallet.Infrastructure
FinWallet.Shared.Contracts
```

Domain modules remain logically separated inside the codebase:

- Authentication;
- Registration;
- Wallet;
- BankAccount;
- Transaction;
- Ledger;
- Fraud.

### Why not microservices first?

A wallet transfer already requires difficult atomic invariants. Splitting Wallet, Ledger and Transaction into separate databases/services before the invariants were proven would force distributed consistency, messaging and reconciliation complexity too early.

The modular monolith keeps money-changing state in one MSSQL transactional boundary while still preserving module boundaries that can later become services if justified by scale/ownership.

### Trade-off

The process is less independently deployable than a full microservice architecture, but much easier to reason about during the financial-correctness phase.

## 3. Why MSSQL is the financial source of truth

MSSQL was selected as the durable financial authority because the project needs:

- ACID transactions;
- strong constraints;
- foreign keys;
- unique constraints;
- explicit row/range locking;
- compare-and-set updates;
- deterministic reconciliation queries.

Redis is used only where transient/fast state makes sense, such as OTP challenges. Redis is deliberately not allowed to decide whether money exists.

### Rejected design

```text
Redis balance -> eventually persist to SQL
```

This was rejected because Redis loss/failover/replication timing must not create or destroy financial truth.

## 4. Authentication was implemented before money APIs

The project first established:

1. customer registration;
2. password hashing;
3. OTP verification;
4. login lockout;
5. server-side sessions;
6. JWT access tokens;
7. refresh-token rotation/reuse detection.

Reason: every later wallet/bank/transfer operation needs a stable customer/session identity. Adding authorization after financial APIs would require reworking ownership queries and contracts.

ASP.NET Core Identity was intentionally not used because the project aims to expose the lower-level authentication/session design decisions rather than delegating them to a framework abstraction.

## 5. Why JWT plus server-side session state

JWT gives efficient request authentication, but a pure stateless JWT cannot be immediately revoked before expiry.

Therefore the token contains a minimal `sid` claim and high-risk financial flows verify server-side session state when needed.

This gives:

- short-lived access tokens;
- server-side revoke capability;
- device/session visibility;
- refresh-token family control.

JWT does not carry balances, IBANs or other financial/customer data.

## 6. Why Wallet and BankAccount are separate

A Wallet represents FinWallet's internal customer balance in one currency.

A BankAccount represents the relationship to an external banking provider account.

They were not merged because their lifecycles and sources of truth differ:

```text
Wallet
- customer-owned financial balance
- AvailableBalance
- BlockedBalance
- internal currency state

BankAccount
- external provider relationship
- external AccountId
- IBAN-like number
- Opening/Active/Rejected/etc.
```

This separation also prevents an external provider outage from redefining the internal accounting model.

## 7. Why FakeBank is a separate API

The bank simulator was intentionally created as a separate HTTP service instead of a class inside FinWallet.

FinWallet must not access FakeBank storage directly. It communicates only through an anti-corruption adapter.

This forces the project to handle real integration problems:

- HTTP timeout;
- 5xx;
- pending state;
- provider-generated identity;
- retry/idempotency;
- polling;
- provider state mismatch.

The adapter keeps FakeBank numeric enums/DTOs outside Domain/Application.

## 8. Why durable internal state is written before bank HTTP

For bank-account opening the sequence is:

```text
validate owned Wallet
-> create durable BankAccount(Opening)
-> finish SQL operation
-> call external bank
-> validate provider result
-> CAS-update BankAccount state
```

The external HTTP request is never executed inside a long-lived SQL transaction.

The durable internal BankAccount ID is used to derive a deterministic provider request key. If the provider creates an account and the HTTP response is lost, the next call uses the same request key rather than opening a second account.

## 9. Why double-entry ledger was added before transfer API

Before exposing a money-changing endpoint, the project created ledger primitives:

- LedgerAccount;
- LedgerJournal;
- LedgerEntry;
- Debit/Credit sides;
- Posted/Draft lifecycle;
- reversal semantics.

Core invariant:

```text
SUM(Debit) = SUM(Credit)
```

A balance table alone answers "how much money is currently available?". The ledger answers "why does that balance exist?".

Both are needed.

## 10. Why FinancialTransaction is separate from LedgerJournal

`FinancialTransaction` is the business operation record.

`LedgerJournal` is the accounting effect.

They are related but not equivalent. Separating them supports:

- business status/history;
- failure state;
- idempotency resource identity;
- accounting reversal;
- later reconciliation/operations reporting.

## 11. Why the wallet transfer store is intentionally large/explicit

The atomic transfer posting store uses explicit SQL rather than hiding everything behind generic repositories.

One MSSQL transaction owns:

```text
IdempotencyRecord
+ source wallet update
+ destination wallet update
+ FinancialTransaction
+ LedgerJournal
+ LedgerEntries
```

This is one place where explicitness is preferred over abstraction because the transaction and locking order are part of the business correctness model.

### Lock ordering

Source/destination wallet rows are locked in deterministic GUID order. This reduces opposite-direction transfer deadlock risk.

### Double validation

Ledger balance is checked in Domain and persisted SQL totals are checked again before commit.

## 12. Why durable idempotency is in MSSQL

A financial retry can happen after:

- mobile network loss;
- client timeout;
- gateway timeout;
- duplicate button press;
- retrying load balancer/client.

Therefore the final duplicate guarantee cannot be a process-local cache.

Idempotency identity is:

```text
Scope + CustomerId + IdempotencyKey
```

A canonical request hash also ensures that reusing a key with a different payload fails rather than silently returning the wrong transaction.

Completed replay returns immutable transaction fields instead of today's wallet balances.

## 13. Why fraud runs before the money SQL transaction

The transfer orchestration order is:

```text
completed idempotency replay check
-> load server-side risk signals
-> internal fraud rules
-> external FakeFraud
-> combine decision
-> atomic financial posting only on Allow
```

External HTTP is intentionally outside the SQL transaction.

If external fraud is unavailable, the flow fails closed and does not start the money transaction.

### Why risk flags are server-derived

The client does not submit values such as:

- `isNewDevice`;
- velocity count;
- known beneficiary;
- 24h total.

Those values are calculated from server-side durable state. Allowing the caller to declare itself low-risk would defeat the purpose of fraud controls.

## 14. Why completed replay occurs before fraud

A previously completed idempotent transfer should remain replayable even if today's fraud signals are different.

Re-running fraud on a completed replay could create this contradiction:

```text
Yesterday: transaction completed.
Today: same HTTP retry is denied by changed risk rules.
```

Therefore completed durable replay is resolved before expensive/risk-sensitive fraud evaluation.

## 15. Why the API remained controller-based

The project deliberately standardizes on ASP.NET Core controllers.

Reasons:

- consistent filters/attributes;
- explicit request/response actions;
- straightforward Swagger generation;
- familiar enterprise Web API structure;
- avoids mixing Minimal API and controller conventions in one codebase.

## 16. Why ServiceResult is only an HTTP contract

All API bodies use a shared `ServiceResult<T>` envelope for stable client behavior.

Domain/Application do not depend on this type.

This prevents an HTTP transport decision from leaking into financial business objects.

## 17. Why YARP Gateway was introduced after core financial correctness

Gateway was intentionally not the first feature. Routing infrastructure does not solve incorrect money logic.

After authentication, provider integration, wallet persistence, ledger and atomic transfer were established, YARP was added as the platform edge.

Its responsibilities are:

- route authentication;
- internal-service authorization;
- rate limiting;
- request/resource bounds;
- CORS;
- security headers;
- active/passive health checks;
- load balancing;
- centralized provider routing.

It does not own financial business rules.

## 18. Why service-to-service calls also go through Gateway

FinWallet provider adapters now target Gateway `/providers/*` routes.

Two separate service keys are used:

```text
FinWallet -> Gateway        InternalServiceKey
Gateway -> destination      DownstreamServiceKey
```

The destination services reject direct business calls without the downstream key. This makes the gateway an enforceable topology boundary rather than only a client convention.

## 19. Why Swagger is shared through FinWallet.Shared.Web

Adding independent Swagger/rate/CORS/header code to seven APIs would create configuration drift.

`FinWallet.Shared.Web` centralizes the cross-cutting HTTP platform baseline while each service keeps its own business DI registrations.

This is intentionally a small shared host library rather than a new application framework.

## 20. Why security/performance values are partly configurable

Operational values are configuration-driven:

- addresses;
- timeouts;
- pool sizes;
- rate limits;
- body/header/connection limits;
- CORS origins;
- JWT lifetime within a safe range;
- Swagger exposure;
- YARP destinations/load-balancer config.

Some values deliberately remain code/schema invariants.

Example: PBKDF2 iteration count cannot simply be changed in appsettings because the current credential schema persists only a hash-version, not per-password iteration metadata. Changing it at runtime would make existing credentials unverifiable. A future work-factor change must be implemented as a versioned password-hash migration.

Similarly, double-entry equality and financial decimal constraints are correctness rules, not operations toggles.

## 21. Why Redis was not expanded into a general cache

Redis is valuable but easy to overuse.

Current use focuses on transient OTP state. The review retained one shared `ConnectionMultiplexer` and added configurable reconnect/timeout/keepalive behavior.

Wallet balances, idempotency final truth and ledger state remain MSSQL-owned.

## 22. Why mocks were added but not treated as sufficient

The original repository had no unit-test project. A new xUnit v3 + Moq project now verifies Application orchestration behavior.

Mock tests answer questions such as:

> "When wallet ownership validation fails, was the external provider avoided?"

They cannot answer:

> "Does SQL Serializable + range locking actually prevent two concurrent money effects?"

Those require real MSSQL/Redis/YARP integration and concurrency tests.

## 23. Implementation sequence used

The project evolved in roughly this dependency order:

1. Define project scope and financial invariants.
2. Establish Domain/Application/Infrastructure/API boundaries.
3. Build customer registration model.
4. Add password hashing and OTP verification.
5. Add login/session/JWT/refresh-token lifecycle.
6. Add fake communication boundary.
7. Build fraud domain rules and FakeFraud adapter.
8. Define wallet domain model.
9. Define double-entry ledger domain model.
10. Build FakeBank as an independent HTTP service.
11. Add BankAccount aggregate and anti-corruption adapter.
12. Persist Wallet/BankAccount state.
13. Expose wallet creation/listing.
14. Add FinancialTransaction/Idempotency/Ledger SQL schema.
15. Build atomic wallet-transfer posting store.
16. Add server-derived fraud signals and public transfer orchestration.
17. Add YARP Gateway and enforce gateway-only traffic.
18. Add shared Swagger/security host baseline.
19. Add configuration/performance tuning.
20. Add initial xUnit/Moq tests.
21. Refresh operational/security/onboarding documentation.

The sequence is dependency-driven: each public financial capability is exposed only after the persistence/security invariant it needs exists.

## 24. Next architecture steps

The most natural next implementation order is:

1. BankDeposit funding flow;
2. BankWithdrawal using Available -> Blocked -> Settled/Released balance transitions;
3. cutoff integration;
4. durable FraudEvents/manual-review state;
5. outbox/inbox;
6. notifications;
7. transaction-history read model;
8. reconciliation runs/issues;
9. real MSSQL/Redis/YARP integration/concurrency tests;
10. OpenTelemetry/structured masked logging/operational dashboards.

BankDeposit is especially important because a newly registered wallet currently starts at zero and cannot be funded through the public API.

## 25. Core architecture rule

The recurring rule behind the implementation is:

```text
Do not expose the easy endpoint before the hard invariant underneath it exists.
```

Examples:

- BankAccount API came after durable BankAccount persistence.
- Transfer API came after ledger + idempotency + atomic posting.
- Fraud is evaluated before posting and fails closed.
- Gateway came after ownership/auth models existed.
- Tests distinguish mockable orchestration from real concurrency guarantees.

This keeps the project useful as a financial-engineering example rather than a collection of endpoints that only works on the happy path.
