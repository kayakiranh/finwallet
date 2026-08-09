# External Integrations

## Integration principles

FinWallet treats every simulator as an external provider even though the simulator source lives in the same repository.

Rules:

- provider services are controller-based ASP.NET Core APIs;
- provider success/failure bodies use `ServiceResult<T>`;
- FinWallet never reads provider storage directly;
- provider DTOs/enums remain behind Infrastructure anti-corruption adapters;
- internal and provider identifiers remain separate;
- external HTTP does not execute while a FinWallet financial SQL transaction is held open;
- retries are operation-specific and must preserve idempotency;
- provider timeout/fail-open/fail-closed behavior is explicit;
- normal FinWallet provider calls go through YARP Gateway rather than directly to simulator hosts.

## Gateway provider routes

FinWallet adapters use these configured base paths:

```text
/providers/bank/
/providers/fraud/
/providers/cutoff/
/providers/campaign/
/providers/communication/
```

Local default:

```text
http://localhost:8080/providers/...
```

Production default service DNS:

```text
http://finwallet-gateway:8080/providers/...
```

### Two-stage service authentication

FinWallet -> Gateway uses `FinWallet:Gateway:InternalServiceKey` in `X-Internal-Service-Key`.

Gateway validates that credential using its `InternalService` authorization policy. Before proxying, YARP replaces the request header with `Gateway:Security:DownstreamServiceKey`.

Destination services require the downstream key for business endpoints. Therefore direct pod/service calls without the Gateway credential are rejected.

Health endpoints remain open for health checks. Swagger is controlled by environment configuration.

## HttpClient policy

Provider adapters use HttpClientFactory and a shared `SocketsHttpHandler` baseline:

- configurable provider timeout;
- configurable max connections per server;
- configurable pooled connection lifetime;
- configurable pooled idle timeout;
- GZip/Deflate/Brotli decompression;
- cookies disabled;
- internal service header injected by a delegating handler.

Automatic retries are deliberately not added to arbitrary financial POSTs. A timeout does not prove that a remote side effect did not occur.

## FakeCommunication.Api

Responsibility: simulated SMS/communication provider.

Primary endpoint:

```text
POST /api/v1/communication/sms
```

FinWallet calls it through:

```text
POST /providers/communication/api/v1/communication/sms
```

OTP message bodies are sensitive and must never appear in production logs.

`X-Fake-Mode` supports failure/delay/timeout simulation where implemented.

`FakeCommunicationGateway` owns the provider DTO mapping and correlation propagation.

## FakeBank.Api

FakeBank owns provider-side account/transaction state. It never mutates FinWallet Wallet/Ledger tables.

Implemented provider concepts include:

- open account;
- read account state;
- activate/pending account simulation;
- start deposit/withdrawal provider transaction;
- finalize transaction;
- read transaction state;
- account statement for reconciliation.

Provider write requests use stable request keys so:

- same key + same normalized payload returns original result;
- same key + different payload conflicts;
- concurrent first use is serialized provider-side;
- repeated finalization does not apply an effect twice.

### FinWallet bank adapter

`IBankProvider` is Application-owned. `FakeBankProvider` is Infrastructure-owned.

Adapter responsibilities:

- unwrap provider envelope;
- map provider enums/currency;
- propagate correlation;
- validate provider identity state in the use case;
- classify retryable/non-retryable provider failures;
- never automatically retry a financial POST.

Bank-account opening stores durable internal `BankAccount(Opening)` before provider HTTP and derives deterministic provider RequestKey from the internal BankAccount ID.

## FakeFraud.Api

External fraud remains independent from FinWallet internal rules.

Provider endpoint:

```text
POST /api/v1/fraud/evaluate
```

FinWallet route:

```text
POST /providers/fraud/api/v1/fraud/evaluate
```

The request receives only server-derived non-PII risk signals. Raw device ID is not sent; the risk store derives a stable hashed device reference.

External fraud is mandatory for transfer decisions after internal rules unless internal fraud already returned Deny. Timeout/network/malformed provider response is fail-closed and prevents financial posting.

## FakeCutoff.Api

Provider endpoint:

```text
POST /api/v1/cutoffs/evaluate
```

Gateway route:

```text
/providers/cutoff/*
```

It owns simulated banking calendars/business-hour/cutoff interpretation. Current holiday/calendar data is deterministic simulator data and is not a legal production calendar source.

This integration is intended for the BankDeposit/BankWithdrawal flow.

## FakeCampaign.Api

Provider endpoint:

```text
POST /api/v1/campaigns/evaluate
```

Gateway route:

```text
/providers/campaign/*
```

Campaign provider may calculate eligibility/discount/sponsor identity. FinWallet remains responsible for accounting the resulting economic effect through a balanced ledger.

## Health and load balancing

YARP clusters use configuration-driven destinations and `PowerOfTwoChoices`. Development has one destination per service. Production can add replicas without source-code changes.

Critical provider clusters have active health checks. FinWallet's main API cluster additionally uses passive transport-failure health handling.

## SSRF / unsafe upstream prevention

Clients cannot pass an arbitrary provider URL. Provider destinations come only from server-owned configuration/YARP clusters.

A client payload therefore cannot turn a normal financial request into `HttpClient.GetAsync(userProvidedUrl)`.

## Direct access rule

The simulator ports exist for local debugging and health/Swagger inspection. Business integration traffic is considered valid only through Gateway trust boundaries. Production network policy should additionally make backend services non-publicly routable.
