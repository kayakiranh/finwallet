# FinWallet Agent Rules

This file is the repository-level source of truth for every coding/review/documentation agent.

## Product
FinWallet is a .NET 8 multi-currency digital wallet platform with double-entry ledger, internal fraud controls, external fake provider integrations, financial reconciliation, masked structured file logging, and strong transaction consistency.

## Fixed technology decisions
- .NET 8 / ASP.NET Core Web API
- MSSQL is the financial source of truth
- Redis is transient/distributed support only; never the financial source of truth
- JWT access tokens + custom refresh-token/session model
- ASP.NET Core Identity is forbidden
- Paid or freemium NuGet packages are forbidden
- Prefer .NET built-ins and Microsoft packages; third-party packages must be fully free/open-source and documented
- Built-in dependency injection
- Structured JSON file logging with central masking/redaction

## HTTP API standard
- Minimal API endpoint mappings are forbidden in every HTTP project, including all fake provider APIs.
- Every HTTP project uses controller-based ASP.NET Core Web API with `AddControllers`, `MapControllers`, `ControllerBase` and attribute routing.
- `Program.cs` is composition/bootstrap only; business endpoints must not be declared through `MapGet`, `MapPost`, `MapPut`, `MapDelete` or route-handler lambdas.
- Every API response body, including health, success and error responses, uses the shared `ServiceResult<T>` contract.
- Controller actions return typed `ActionResult<ServiceResult<T>>` or an equivalent typed ServiceResult response.
- Application and Domain layers must not reference `ServiceResult<T>`; controllers map internal use-case results to the shared HTTP contract.
- Anonymous error payloads and ProblemDetails are not the public API contract. Stable machine-readable error codes are carried inside `ServiceResult<T>`.
- API-specific request/response DTOs remain in their API projects and must not leak provider transport models into Domain.

## Architecture
- Main FinWallet application: Modular Monolith, clean boundaries without ceremonial over-engineering
- Layers: Api -> Application -> Domain; Infrastructure implements external/persistence concerns
- External simulators are separate HTTP APIs:
  - FakeBank.Api
  - FakeFraud.Api
  - FakeCutoff.Api
  - FakeCampaign.Api
  - FakeCommunication.Api (SMS + Email)
- External DTOs/status codes must not leak into Domain; use Adapter + Anti-Corruption Layer
- No full microservices architecture for Wallet/Ledger/Transaction core
- No Event Sourcing
- No generic repository/service framework
- No mandatory mediator package/framework
- CQRS-lite is allowed as a code-organization concept only

## Financial invariants
- Every financial movement must be represented in the double-entry ledger
- Ledger is append-only during normal operations; corrections use reversal/compensating entries
- Every journal must balance: total debit == total credit
- Wallet balance is current state/projection; ledger is the authoritative financial history
- Currency mismatches must fail before financial commit
- No completed transaction may return to an earlier processing state
- No financial write may rely solely on Redis for correctness
- External HTTP calls must not keep a SQL transaction open

## Concurrency and idempotency
- All money-changing commands must be idempotent
- Use Redis for hot/fast idempotency coordination and MSSQL unique guarantees for durability
- Reusing an idempotency key with a different request payload must fail
- Wallet concurrency must be protected in MSSQL using atomic operations/constraints and optimistic concurrency where appropriate
- Redis distributed locks may be used only as an additional coordination layer
- Duplicate callbacks must be safe through Inbox/idempotent-consumer semantics

## Authentication and customer rules
- Every interactive user is a Customer; keep the Customer table intentionally small
- Credentials, sessions and refresh tokens are separate concerns/tables
- Password security algorithm is a fixed security decision, not a runtime-selectable appsettings option
- Keep a password-hash version only for future secure migrations
- Registration is restricted by supported country and country/phone-prefix compatibility
- Registration OTP uses FakeCommunication.Api

## Fraud
- FinWallet has its own internal rule-based fraud engine
- FinWallet also calls FakeFraud.Api as an external provider
- Final decision policy combines internal and external decisions
- External fraud failure behavior must be explicit and conservative for financial operations
- Fraud velocity counters may use Redis; durable fraud events belong in MSSQL

## Cutoff and campaign
- Cutoff/business-calendar/holiday calculations belong to FakeCutoff.Api, not FinWallet Domain
- Campaign eligibility/discount calculation belongs to FakeCampaign.Api
- FinWallet remains responsible for financial accounting of discounts returned by Campaign
- Never silently charge the undiscounted amount when a customer-confirmed discounted purchase cannot be validated

## Multi-currency
- A Customer may own separate wallets/accounts for multiple supported currencies
- Money must always carry currency
- BankAccount and Wallet are separate concepts
- Currency conversion is out of scope unless explicitly added through an ADR

## External bank and workflows
- FakeBank.Api behaves like a real external bank
- Bank operations may be asynchronous/pending and completed via callback/polling
- Long-running external bank operations use orchestration/Saga-style state transitions and compensation where needed
- Maintain internal and external references separately

## Reconciliation
- Reconcile Wallet state vs Ledger
- Reconcile internal bank-related ledger/transactions vs FakeBank statements
- Reconciliation differences create issues; never silently rewrite financial balances

## Notifications
- Registration: fake SMS OTP
- Financial operations: fake SMS/email through FakeCommunication.Api
- Notification failure must not roll back a completed financial transaction
- Use Transactional Outbox for post-commit external side effects

## Logging and audit
- All financial lifecycle events are structured and written as JSON lines to rolling files
- Central masking/redaction is mandatory
- Never log passwords, OTPs, JWTs, refresh tokens, Authorization headers or secrets
- Phone/email/IBAN/account identifiers must be masked when logged
- CorrelationId, TransactionId and provider references are distinct and traceable
- Application logs, financial logs and audit events are distinct concerns

## Reliability patterns
Use where appropriate:
- Transactional Outbox
- Inbox / Idempotent Consumer
- State Machine
- Saga + Compensation for long external bank workflows
- Adapter + Anti-Corruption Layer
- Timeout, Circuit Breaker and controlled Retry
- Cache-Aside
- Fail-fast validation
- Explicit fail-open/fail-closed integration policies

## Code documentation standard
- Every class, record, struct, enum, interface, method, constructor and property must have an XML documentation comment, regardless of public/internal/private visibility when practical.
- Every `<summary>` must contain both Turkish and English descriptions using `TR:` and `EN:` prefixes.
- Method parameters, return values, generic type parameters and meaningful exceptions must also be documented in Turkish and English.
- Comments must explain intent, business meaning and important constraints; do not merely restate the identifier name.
- Public APIs compile with XML documentation generation enabled and CS1591 treated as an error.
- Generated code is the only default exception; any additional exception requires an explicit ADR or documented justification.

## Testing expectations
Every financial feature must include relevant unit/integration/end-to-end tests. Critical scenarios include:
- concurrent overspend attempts
- duplicate commands and duplicate callbacks
- Redis unavailable
- bank/fraud/campaign/cutoff/communication timeout or failure
- refresh-token reuse
- ledger imbalance prevention
- repeated refund/reversal attempts
- reconciliation mismatches

## Documentation is part of Definition of Done
Any architectural, API, package, persistence or integration change must update the corresponding file under /docs.
Every new NuGet dependency must document: version, license, purpose, why required, and native/free alternatives considered.

## Review priority
When trade-offs exist, prioritize in this order:
1. Financial correctness
2. Security
3. Data consistency and idempotency
4. Recoverability and reconciliation
5. Observability
6. Maintainability
7. Performance
8. Convenience
