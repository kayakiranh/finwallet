# External Integrations

## Integration principles

FinWallet treats every simulator as an external provider even though all simulators live in the same repository.

Rules:

- Every simulator is an ASP.NET Core controller-based Web API; Minimal API route mappings are forbidden.
- Every simulator success/error body uses `ServiceResult<T>` from the shared HTTP contract project.
- FinWallet never reads a simulator database directly.
- Provider DTOs remain in Infrastructure or simulator projects and never become Domain models.
- Correlation IDs are propagated across HTTP boundaries.
- Internal IDs and provider references are stored separately.
- External HTTP calls do not run while a FinWallet SQL transaction is open.
- Retry behavior is provider/operation specific and must never duplicate a financial operation.
- Timeout/circuit-breaker/fail-open/fail-closed policies are explicit per provider.

## FakeCommunication.Api

### Responsibility

Simulates SMS and email providers. Registration OTP and financial notifications are delivered here instead of using a real commercial service. The current implemented HTTP surface includes SMS; email remains part of the communication-provider backlog.

### POST `/api/v1/communication/sms`

Response: HTTP `202 Accepted` with `ServiceResult<SendMessageResponse>`.

The message body can contain an OTP and must never be emitted to production logs.

### Development inspection

`GET /api/v1/dev/messages/{messageId}` returns `ServiceResult<FakeMessageRecord>` so a developer can inspect a simulated OTP/message without a real SMS provider. This is a simulator-only development endpoint and must never be copied into the main FinWallet API.

### Failure simulation

`X-Fake-Mode` supports `fail`, `delay` and `timeout`.

### FinWallet adapter

`FakeCommunicationGateway` implements the Application `ICommunicationGateway` boundary. Provider DTOs remain isolated under `FinWallet.Infrastructure.Communication`.

## FakeBank.Api

FakeBank is an external-bank simulator. It owns provider-side accounts, transaction lifecycle, duplicate protection and reconciliation statement data. It never writes FinWallet Wallet, BankAccount or Ledger state.

Implemented controller endpoints:

- `POST /api/v1/bank/accounts` — open a currency-specific external account;
- `GET /api/v1/bank/accounts/{accountId}` — read-only account state lookup for polling;
- `POST /api/v1/bank/accounts/{accountId}/activate` — simulator control that completes a pending opening;
- `POST /api/v1/bank/transactions` — start Deposit/Withdrawal;
- `POST /api/v1/bank/transactions/{transactionId}/finalize` — simulator control that completes/fails a pending transaction;
- `GET /api/v1/bank/transactions/{transactionId}` — transaction status lookup;
- `GET /api/v1/bank/accounts/{accountId}/statement` — completed movements for reconciliation.

`X-Fake-Mode` supports `fail`, `delay`, `timeout` and `pending` on the relevant write endpoints.

Provider-side write requests contain a stable `RequestKey`:

- same key + same normalized payload returns the original provider result;
- same key + different payload is a conflict;
- concurrent first requests sharing the same key are serialized by the simulator;
- repeated transaction finalization cannot apply a financial effect twice.

### FinWallet bank boundary

FinWallet Domain owns `BankAccount`, which links an internal Wallet to an external account while keeping internal and provider identifiers separate. `BankAccount` does not own the authoritative financial ledger.

Application owns `IBankProvider` and provider-independent account/transaction/statement result models. Infrastructure owns `FakeBankProvider`, which:

- unwraps provider `ServiceResult<T>` responses;
- maps FakeBank numeric enums into Application enums;
- maps provider currency strings into `CurrencyCode`;
- propagates `X-Correlation-Id`;
- keeps provider DTOs out of Domain/Application;
- classifies network, timeout and 5xx failures as retryable;
- never retries a financial POST by itself.

The provider base URL is a deployment value at `FinWallet:Integrations:FakeBank:BaseUrl` and the typed HttpClient has a fixed short timeout.

## FakeFraud.Api

FakeFraud is an external fraud-provider simulator and remains independent from FinWallet internal fraud rules.

`POST /api/v1/fraud/evaluate` returns `ServiceResult<FraudEvaluationResponse>` with deterministic `Allow`, `Review` or `Deny` behavior. FinWallet Infrastructure maps this into the provider-independent `IExternalFraudProvider` boundary. Internal and external fraud decisions are combined by `FraudDecisionPolicy`; an external Allow never overrides an internal Deny.

## FakeCutoff.Api

### POST `/api/v1/cutoffs/evaluate`

Implemented as `CutoffController`. It returns `ServiceResult<CutoffEvaluationResponse>` and owns business hours, timezone interpretation, weekends, simulated holiday seed data, processing date, settlement date and bank/country/currency/transaction-type cutoff rules.

The current holiday data is deterministic simulator data, not a production/legal holiday source.

## FakeCampaign.Api

### POST `/api/v1/campaigns/evaluate`

Implemented as `CampaignController`. It returns `ServiceResult<CampaignEvaluationResponse>` and owns merchant/campaign eligibility, minimum transaction amount, discount type/value, maximum discount and campaign sponsor identity.

FinWallet remains responsible for accounting. A campaign response may calculate the discount, but the FinWallet ledger determines who funds that discount and ensures the merchant/customer/system entries remain balanced.
