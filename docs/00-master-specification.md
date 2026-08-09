# FinWallet Master Specification v1

## 1. Product Goal
FinWallet is a .NET 8 multi-currency digital-wallet side project designed to exercise real financial-system concerns: transaction consistency, double-entry accounting, idempotency, concurrency, external integrations, fraud checks, cutoff/business calendars, campaigns, notifications, reconciliation, security, and observability.

## 2. Core Scope
### Customer and authentication
- All interactive users are Customers.
- Custom JWT access token, refresh token rotation and session model.
- No ASP.NET Core Identity.
- Registration restricted by supported country and country/phone-prefix compatibility.
- Registration OTP via FakeCommunication.Api SMS.

### Wallets and accounts
- A Customer can hold separate wallets for multiple supported currencies.
- A Customer can open matching external bank accounts through FakeBank.Api.
- Wallet and BankAccount are different domain concepts.
- No FX conversion in v1.

### Financial operations
- Deposit / Bank-to-Wallet funding
- Withdrawal / Wallet-to-Bank transfer
- Wallet-to-Wallet transfer
- Merchant purchase
- Refund
- Reversal / compensation where required

### Ledger
- Double-entry ledger for every financial movement.
- Append-only normal operation.
- Balanced journal invariant.
- Wallet balances are current state; Ledger is authoritative financial history.

### Fraud
- Internal rule-based fraud checks inside FinWallet.
- External fraud evaluation through FakeFraud.Api.
- Combined decision: Allow / Review / Deny.
- Redis may hold velocity counters; durable fraud events remain in MSSQL.

### Cutoff
- FakeCutoff.Api owns business-hours, country/bank/currency/transaction-type cutoff, holidays, processing date and settlement date calculations.
- FinWallet consumes the result and controls transaction workflow.

### Campaigns
- FakeCampaign.Api owns eligibility and discount calculations.
- Campaigns may be merchant-specific, merchant-group/category based, date-bound, currency-bound and capped.
- FinWallet owns the accounting impact of any returned discount and sponsor allocation.

### Communication
- FakeCommunication.Api provides SMS and email simulation.
- Registration uses SMS OTP.
- Financial operations use asynchronous SMS/email notifications.
- Notification failure cannot reverse a completed transaction.

### Reconciliation
- Wallet current state vs Ledger-derived balance.
- Internal bank-related transaction/ledger state vs FakeBank statement.
- Differences are recorded and investigated; balances are never silently rewritten.

### Logging
- Structured JSON Lines file logs.
- Separate application, financial and audit concerns.
- Central masking/redaction.
- Never log password, OTP, JWT, refresh token, Authorization header or secret values.

## 3. Technology Constraints
- .NET 8
- ASP.NET Core Web API
- MSSQL
- Redis
- JWT
- Docker for local dependencies/environment
- Built-in .NET dependency injection
- Paid/freemium NuGet packages forbidden
- Prefer .NET built-ins / Microsoft packages; third-party dependencies must be fully free/open-source and justified in documentation

## 4. Architecture
### Main application
Modular Monolith with projects:
- FinWallet.Api
- FinWallet.Application
- FinWallet.Domain
- FinWallet.Infrastructure

### External simulators
- FakeBank.Api
- FakeFraud.Api
- FakeCutoff.Api
- FakeCampaign.Api
- FakeCommunication.Api

### Test projects
- FinWallet.UnitTests
- FinWallet.IntegrationTests
- FinWallet.EndToEndTests

## 5. Required Design Patterns
Use where justified, without ceremonial over-engineering:
- DDD-lite: Entity, Value Object, Aggregate, Invariant, Domain Event
- State Machine for transaction lifecycles
- Adapter + Anti-Corruption Layer for every external provider
- Application Orchestrator / explicit workflow pipeline
- Chain of Responsibility for internal fraud rules
- Policy Pattern for fraud/integration decisions
- Repository only at meaningful aggregate/persistence boundaries
- Unit of Work / explicit SQL transaction boundary
- Double-entry ledger and reversal model
- Transactional Outbox
- Inbox / Idempotent Consumer
- Saga + Compensation only for long external-bank workflows
- Optimistic Concurrency + atomic database operations
- Idempotent Command
- Cache-Aside / TTL for Redis data
- Timeout / Circuit Breaker / controlled Retry
- Fail-fast validation
- Explicit fail-open/fail-closed provider policy
- Structured Logging + central redaction
- Reconciliation / matching strategy
- CQRS-lite only as organizational separation; no separate read database/event-sourcing infrastructure

## 6. Financial Correctness Rules
1. No financial movement may bypass Ledger.
2. Every journal must balance before commit.
3. MSSQL is the final consistency authority.
4. Redis loss must never permit duplicate money, negative balance or ledger corruption.
5. External HTTP calls must not keep a SQL transaction open.
6. Duplicate commands and duplicate provider callbacks must be safe.
7. A completed transaction cannot transition backward to processing.
8. Corrections use reversal/compensation, not mutation/deletion of history.
9. Currency must be part of Money and validated before financial commit.
10. Reconciliation never silently repairs balances.

## 7. External Provider Behavior
Each fake provider must support deterministic happy paths plus configurable/dummy failure behavior such as delay, timeout, 5xx, reject and duplicate callback where relevant.

### FakeBank.Api
- Customer/account creation as needed by integration contract
- Open currency-specific account
- Withdrawal/deposit/transfer-like bank operations used by FinWallet
- Pending/approved/rejected/failed states
- Status lookup and/or callback
- Statement/transaction feed for reconciliation

### FakeFraud.Api
- Evaluate transaction using dummy rules/data
- Return provider reference, score/signals, Allow/Review/Deny
- Simulate latency/timeout/error

### FakeCutoff.Api
- Input: transaction type, currency, country, bank/provider context, request time
- Own holiday/business-day dataset
- Return whether processable now, processing date, settlement date and reason

### FakeCampaign.Api
- Input: customer reference, merchant, amount, currency, date/context
- Return eligibility, campaign id, original amount, discount, final amount and sponsor type
- Campaign usage limits must be concurrency-safe inside the simulator when limits exist

### FakeCommunication.Api
- SMS and Email endpoints
- OTP delivery simulation
- Financial notification simulation
- Success/delay/failure modes

## 8. Security Requirements
- Custom customer credentials/session design
- Secure fixed password-hashing strategy; not runtime selectable
- Hash-version field allowed for future migration
- Refresh-token rotation and reuse detection
- Login/OTP rate limits and brute-force controls
- Country + phone prefix restriction at registration
- Secret management through environment/user-secrets/deployment secrets
- Parameterized SQL/ORM-safe access
- Masked logs and distinct audit records

## 9. Concurrency and Idempotency
- Every money-changing command requires an idempotency key.
- Redis can serve hot lookup/coordination; MSSQL must contain durable unique guarantee.
- Same key + different request hash => conflict.
- Duplicate callback => single effect through Inbox semantics.
- Wallet updates use DB-level atomic protection and optimistic concurrency where appropriate.
- Redis locks, if used, are secondary optimization/coordination only.

## 10. Transaction Workflow Principles
External workflows persist state between steps instead of holding long database transactions.
Typical withdrawal:
Created -> FraudPending -> FraudApproved -> Scheduled/Processing -> BankPending -> Completed
Alternative terminal states: Rejected, Failed, Cancelled, Reversed.
Funds may move Available -> Blocked before external completion, then finalize or compensate.

## 11. Logging and Audit
### Financial structured log fields (as applicable)
- timestamp
- level
- eventType
- correlationId
- transactionId
- customer public/internal reference
- transactionType
- amount
- currency
- masked source/target account
- merchant/campaign references
- internal/external fraud decision/reference
- cutoff/provider reference
- status
- durationMs

### Never log
- password or password-derived secrets
- OTP
- JWT/access token
- refresh token
- Authorization header
- raw secrets
- unmasked sensitive account/phone/email identifiers

## 12. Documentation Deliverables
- docs/01-technical-analysis.md
- docs/02-architecture.md
- docs/03-design-patterns.md
- docs/04-api-guide.md
- docs/05-external-integrations.md
- docs/06-technologies-and-packages.md
- docs/07-database.md
- docs/08-financial-flows.md
- docs/09-security.md
- docs/10-testing.md
- docs/11-final-technical-review.md
- docs/adr/*

## 13. Definition of Done
A feature is not done until:
- code compiles
- relevant unit/integration/E2E tests pass
- financial/concurrency/idempotency rules are covered where applicable
- external failure behavior is defined where applicable
- structured logging and masking are correct
- documentation is updated
- new package usage is documented and license/paid/freemium restrictions verified
- architecture review finds no forbidden dependency direction or pattern creep

## 14. Explicit Out of Scope for v1
- Real bank/fraud/SMS/email/campaign/cutoff providers
- Credit-card acquiring/payment gateway
- Full core banking
- Loans/credit scoring
- Stock/crypto trading
- FX conversion
- Event Sourcing
- Kafka/RabbitMQ unless later approved by ADR
- Full microservices decomposition
- Kubernetes deployment
