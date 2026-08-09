# Fraud Evaluation Audit

## Purpose

Every new money-changing operation should answer two separate questions:

1. What risk decision was produced before money movement?
2. Which durable FinancialTransaction, if any, was allowed by that decision?

`database/004_fraud_events_schema.sql` introduces `FraudEvents` for this purpose.

FraudEvents are audit records, not logs and not a replacement for FinancialTransactions or LedgerJournals.

## Privacy boundary

FraudEvents deliberately do **not** store:

- raw DeviceId;
- JWT/access/refresh tokens;
- phone number;
- e-mail;
- IBAN;
- OTP;
- raw provider exception/error text.

Device identity is stored only as the server-derived SHA-256 `DeviceReference` already used for the external fraud provider.

Provider failures are stored using safe machine-readable codes such as `EXTERNAL_FRAUD_UNAVAILABLE`.

## FraudEvent identity

For wallet transfers, one GUID is used consistently as:

```text
FraudEvent.Id
    = internal fraud evaluation reference
    = opaque FakeFraud transaction/evaluation reference sent by FinWallet
```

The external provider still returns its own independent `ExternalProviderReference`, which is stored separately.

## Stored risk snapshot

A WalletTransfer FraudEvent records the server-derived inputs used for that decision:

- customer/session IDs;
- source/destination wallet IDs;
- transaction type;
- amount/currency;
- customer country code;
- hashed device reference;
- new-device flag;
- five-minute successful-transfer count;
- 24-hour same-currency successful-transfer amount;
- known-beneficiary flag.

This makes a later review reproducible without trusting client-supplied risk flags.

## Internal/external decision state

`ExternalEvaluationStatus` uses three states:

### `NotRequired`

Used only when internal FinWallet fraud already returned `Deny`.

Database constraints require:

- InternalDecision = Deny;
- FinalDecision = Deny;
- all external provider result fields are null;
- no external failure code.

The external provider is intentionally not called because an external Allow could never override the internal Deny.

### `Completed`

External provider evaluation completed successfully.

Database constraints require:

- ExternalDecision;
- FinalDecision;
- ExternalProviderReference;
- ExternalRiskScore;
- JSON reason-code array;
- no external failure code.

### `Unavailable`

Required external evaluation could not complete.

Database constraints require:

- no external decision;
- no final decision;
- no provider reference/score/reasons;
- a safe external failure code.

Wallet transfers fail closed and financial posting does not begin.

## Allow / Review / Deny behavior

### Allow

Sequence:

```text
server-side risk signals
-> internal fraud
-> external fraud
-> final Allow
-> INSERT FraudEvent
-> atomic wallet-transfer posting
-> FinancialTransactions.FraudEventId = FraudEvent.Id
```

The FraudEvent is durable before the money movement starts. The atomic posting store requires a non-empty FraudEventId and writes it into the FinancialTransaction row.

### Review

The FraudEvent is durable, but no FinancialTransaction, Wallet balance update or LedgerJournal is created.

The manual review queue/state machine is still a later feature. Until that exists, Review remains a durable risk audit plus HTTP review-required outcome.

### Deny

The FraudEvent is durable, but no financial posting occurs.

### External unavailable

The FraudEvent records the fail-closed dependency failure with a safe code; no financial posting occurs.

## FinancialTransaction link

Migration 004 adds nullable `FinancialTransactions.FraudEventId` plus:

- FK to `FraudEvents(Id)`;
- filtered unique index so one FraudEvent cannot authorize multiple FinancialTransactions.

It remains nullable for backwards compatibility with transactions created before this migration and for future transaction types that have not yet been wired through fraud.

New WalletTransfer posting code always supplies a FraudEventId.

## Concurrent identical requests

Completed idempotency replay is checked before fraud evaluation, so normal retries do not create a second FraudEvent.

Two genuinely concurrent first requests can both complete fraud evaluation before the durable posting idempotency winner is known. In that rare race, both fraud evaluations remain valid audit events but only the posting winner's FraudEvent is linked to the FinancialTransaction. The losing request receives the same immutable completed transfer result after the posting-store idempotency lock resolves.

Audit events are append-only and are not deleted merely because an evaluation lost an idempotency race.

## Failure semantics

FraudEvent persistence is part of the security precondition for a new wallet transfer. If the durable audit insert fails, financial posting does not start.

Once money posting begins, MSSQL atomicity is still owned by `SqlWalletTransferPostingStore` for:

- Wallet balances;
- FinancialTransaction;
- LedgerJournal/Entries;
- Idempotency result.

The FraudEvent exists before that transaction and is referenced by the resulting FinancialTransaction when posting succeeds.
