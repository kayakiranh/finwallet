# FakeBank and FakeFraud Integration Guide

## Purpose

`FakeBank.Api` and `FakeFraud.Api` simulate third-party providers and intentionally stay outside the FinWallet modular monolith. Their DTO/status vocabularies are translated by Infrastructure adapters/anti-corruption layers rather than leaking into FinWallet Domain.

All HTTP APIs use controller-based ASP.NET Core Web API. Minimal API endpoint mappings are forbidden and all response bodies use `ServiceResult<T>`.

## FakeBank.Api

### Responsibility

FakeBank owns only simulated external-bank state:

- currency-specific external accounts;
- provider account identifiers and IBAN-like simulator numbers;
- provider-side deposit/withdrawal requests;
- Pending/Completed/Failed provider transaction lifecycle;
- provider request-key idempotency;
- provider statement data used by reconciliation.

FakeBank never writes FinWallet Wallet, Transaction or Ledger state.

### Controller endpoints

| Method | Route | Purpose |
|---|---|---|
| POST | `/api/v1/bank/accounts` | Open a currency-specific external bank account |
| POST | `/api/v1/bank/accounts/{accountId}/activate` | Finalize a Pending account as Active |
| POST | `/api/v1/bank/transactions` | Start provider Deposit/Withdrawal |
| POST | `/api/v1/bank/transactions/{transactionId}/finalize?succeed=true|false` | Complete or fail a Pending provider transaction |
| GET | `/api/v1/bank/transactions/{transactionId}` | Query provider transaction state |
| GET | `/api/v1/bank/accounts/{accountId}/statement` | Return completed statement rows for reconciliation |

Every endpoint returns `ServiceResult<T>`.

### Account opening

Example request:

```json
{
  "externalCustomerReference": "1fe01029-358d-441f-9bc9-5e6ad8cf0a6c",
  "currency": "TRY",
  "requestKey": "account-open-1fe01029-try"
}
```

Normal mode creates an Active account. `X-Fake-Mode=pending` creates the provider account in Pending state so asynchronous bank-account opening can be simulated. The account can later be activated through the activate endpoint.

FinWallet must store its own internal `BankAccountId` separately from the provider `AccountId` and IBAN-like value.

### Money movement

A provider Deposit/Withdrawal request contains:

- external `AccountId`;
- positive amount;
- matching currency;
- Deposit or Withdrawal provider transaction type;
- provider `RequestKey`.

Normal mode applies the provider financial effect once and returns the completed/current result. `X-Fake-Mode=pending` defers the financial effect until explicit finalization.

### Provider idempotency and concurrency

Each write request carries a provider `RequestKey`.

- same key + same normalized payload -> return the original provider result;
- same key + different payload -> reject as conflict;
- concurrent first requests with the same key are serialized under an operation-prefixed request-key lock;
- account state is created before the idempotency record becomes externally observable to a competing request;
- transaction state is created before the idempotency record becomes externally observable to a competing request;
- pending requests do not affect external-account balance until provider finalization;
- repeated finalization of an already-final transaction does not apply the financial effect twice;
- account financial mutations are serialized under an account-specific lock.

This closes the simulator race where a losing concurrent request could previously observe an idempotency record before the referenced account/transaction had been inserted.

The simulator currently keeps provider state in process memory. This is deterministic for local/integration tests but is not restart-durable. FinWallet must never depend on FakeBank in-memory state as its own source of truth.

### Failure simulation

`X-Fake-Mode` supports:

- `fail` -> HTTP 503 ServiceResult failure;
- `delay` -> approximately two-second provider delay;
- `timeout` -> long provider delay intended to exercise client timeout/cancellation;
- `pending` -> asynchronous account/transaction provider state.

Financial POST retries are unsafe unless the same provider `RequestKey` is preserved.

### Reconciliation statement

Only completed provider transactions appear in account statement data. FinWallet reconciliation later compares:

- internal external-bank transaction reference;
- provider transaction identifier;
- account;
- amount;
- currency;
- completion/value date.

Mismatches create reconciliation issues and never silently rewrite FinWallet financial balances.

## FakeFraud.Api

### Responsibility

FakeFraud is an external fraud vendor simulator and is **not** FinWallet's internal fraud engine.

Input deliberately excludes raw PII/secrets. It carries opaque references and risk signals such as:

- transaction/customer/device references;
- transaction type;
- amount/currency/country;
- new-device flag;
- five-minute transaction count;
- twenty-four-hour transaction amount;
- optional merchant identifier.

### Deterministic dummy rules

Initial examples:

- transaction >= 100,000 -> Deny;
- transaction >= 25,000 -> Review;
- >= 10 transactions / 5 min -> Deny;
- >= 5 transactions / 5 min -> Review;
- >= 150,000 total / 24h -> Deny;
- >= 75,000 total / 24h -> Review;
- new device + amount >= 10,000 -> Review;
- blocked merchant seed -> Deny;
- simulator-only high-risk-country seed -> Deny.

Deny signals take precedence over Review. If no external risk signal exists, result is Allow.

### Final FinWallet fraud decision

FinWallet combines internal and external decisions through an explicit policy:

```text
Internal Fraud Rules
        +
FakeFraud external decision
        |
        v
FraudDecisionPolicy
        |
 Allow / Review / Deny
```

An external Allow never overrides an internal Deny. External-provider unavailability for security-sensitive financial operations must use the explicit conservative failure policy defined by FinWallet rather than silently treating the transaction as safe.
