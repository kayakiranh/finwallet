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

Request:

```json
{
  "recipient": "+905321234567",
  "messageType": "RegistrationOtp",
  "body": "FinWallet verification code: 123456",
  "correlationId": "request-correlation-id"
}
```

Response: HTTP `202 Accepted` with `ServiceResult<SendMessageResponse>`.

The message body can contain an OTP and must never be emitted to production logs.

### Development inspection

`GET /api/v1/dev/messages/{messageId}` returns `ServiceResult<FakeMessageRecord>` so a developer can inspect a simulated OTP/message without a real SMS provider. This is a simulator-only development endpoint and must never be copied into the main FinWallet API.

### Failure simulation

`X-Fake-Mode` supports:

- `fail` — returns HTTP 503 with a ServiceResult failure;
- `delay` — delays the response approximately two seconds;
- `timeout` — delays long enough for the FinWallet client timeout/cancellation behavior to be tested.

The simulator does not log OTP/message bodies.

### FinWallet adapter

`FakeCommunicationGateway` implements the Application `ICommunicationGateway` boundary. It maps the internal registration intent into the external provider DTO, calls `/api/v1/communication/sms`, and propagates `X-Correlation-Id`. Provider DTOs remain isolated under `FinWallet.Infrastructure.Communication`.

## FakeBank.Api

The service is controller-based and currently exposes a ServiceResult liveness endpoint. Planned contract responsibilities:

- external customer/account creation;
- currency-specific bank accounts;
- async/pending withdrawals and deposits;
- callback/polling completion;
- statement endpoint used for reconciliation;
- deterministic external references and duplicate-request protection.

Financial POST retry is forbidden unless protected by a provider idempotency/external-reference contract.

## FakeFraud.Api

The service is controller-based and currently exposes a ServiceResult liveness endpoint. Planned contract responsibilities:

- evaluate transaction/customer/device/merchant dummy risk signals;
- return `Allow`, `Review` or `Deny` plus provider reference/reasons;
- support slow response, timeout and provider failure simulation;
- remain independent of FinWallet internal fraud rules.

FinWallet combines internal and external fraud results using an explicit decision policy. High-risk financial operations fail closed when required fraud evaluation is unavailable.

## FakeCutoff.Api

### POST `/api/v1/cutoffs/evaluate`

Implemented as `CutoffController`. It returns `ServiceResult<CutoffEvaluationResponse>` and owns:

- business hours;
- timezone interpretation;
- weekends;
- simulated official-holiday seed data;
- processing date;
- settlement date;
- bank/country/currency/transaction-type cutoff rules.

FinWallet sends transaction context and consumes the returned processing/settlement decision. FinWallet does not duplicate holiday or cutoff calculations internally.

The current holiday data is deterministic simulator data, not a production/legal holiday source.

## FakeCampaign.Api

### POST `/api/v1/campaigns/evaluate`

Implemented as `CampaignController`. It returns `ServiceResult<CampaignEvaluationResponse>` and owns:

- merchant/campaign eligibility;
- minimum transaction amount;
- discount type/value;
- maximum discount;
- campaign sponsor identity (`Platform` or `Merchant`).

FinWallet remains responsible for accounting. A campaign response may calculate the discount, but the FinWallet ledger determines who funds that discount and ensures the merchant/customer/system entries remain balanced.

The system must not silently charge an undiscounted amount when a customer-confirmed discounted transaction cannot be revalidated.
