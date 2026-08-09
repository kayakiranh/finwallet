# FakeBank and FakeFraud Integration Guide

## Purpose

`FakeBank.Api` and `FakeFraud.Api` simulate third-party providers and intentionally stay outside the FinWallet modular monolith. Their DTO/status vocabularies must later be translated by Infrastructure adapters/anti-corruption layers rather than leaking into FinWallet Domain.

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

### Idempotency rule

Each write request carries a provider `RequestKey`.

- same key + same normalized payload -> return the original provider result;
- same key + different payload -> reject as conflict;
- pending requests do not affect external-account balance until provider finalization;
- repeated finalization of an already-final transaction must not apply the financial effect twice.

The simulator implementation currently keeps provider state in process memory. This is suitable for deterministic local/integration simulation but is not restart-durable. A later test-infrastructure slice may give FakeBank its own persistence if restart durability is needed; FinWallet must never rely on FakeBank in-memory state as its own source of truth.

### Reconciliation statement

Only completed provider transactions appear in account statement data. FinWallet reconciliation will later compare:

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
- simulated high-risk-country seed -> Deny.

Deny signals take precedence over Review. If no external risk signal exists, result is Allow.

### Final FinWallet fraud decision

Later FinWallet processing combines:

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

## Failure simulation

Both external APIs will expose deterministic failure modes at the HTTP boundary:

- `fail`;
- `delay`;
- `timeout`;
- FakeBank additionally supports `pending` asynchronous-provider behavior.

HTTP endpoint wiring is intentionally reviewed separately from provider state/rule models so concurrency/idempotency behavior can be corrected before the simulator becomes callable.
