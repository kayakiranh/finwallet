# Wallet Transfer Flow

## Endpoint

`POST /api/v1/transfers`

Requirements:

- valid JWT access token;
- valid server-side session identified by JWT `sid`;
- mandatory `Idempotency-Key` header;
- source wallet owned by JWT `sub` customer;
- distinct destination wallet;
- positive `DECIMAL(19,4)` compatible amount.

Request:

```json
{
  "sourceWalletId": "11111111-1111-1111-1111-111111111111",
  "destinationWalletId": "22222222-2222-2222-2222-222222222222",
  "amount": 125.50
}
```

Currency is never trusted from the client. It is derived from the source wallet and must match the destination wallet.

## Idempotency order

A completed idempotency replay is checked **before fraud evaluation**.

```text
request
-> completed replay lookup
   -> if completed + same immutable transfer payload: return original result
   -> if same key + different payload: conflict
-> server-side risk signal read
-> internal fraud
-> external fraud when needed
-> atomic MSSQL posting
```

This ordering matters. A request completed yesterday must not be re-evaluated against today's changed fraud signals when the client merely retries the same idempotency key.

The atomic posting store still performs its own Serializable idempotency locking. The precheck is only an optimization/semantic guard; it is not the financial correctness mechanism.

## Server-side session validation

A valid JWT signature is not sufficient for a money-changing endpoint.

The risk read verifies that JWT `sid` still maps to a CustomerSession that:

- belongs to JWT `sub` customer;
- is not revoked;
- has not expired;
- belongs to an Active customer.

A revoked server-side session therefore cannot continue moving money merely because its short-lived JWT has not expired yet.

## Server-derived fraud signals

The client cannot supply trust/risk flags.

`SqlWalletTransferRiskSignalStore` derives:

- customer country from `Customers`;
- transfer currency from Wallets;
- device identity from current server-side session;
- first-seen time for the same customer/device from session history;
- new-device flag using a fixed 24-hour window;
- successful WalletTransfer count over the previous five minutes;
- successful same-currency transfer amount over the previous 24 hours;
- known-beneficiary flag based on prior successful transfers to the destination wallet.

Raw DeviceId is not sent to FakeFraud. A stable SHA-256 device reference is sent instead.

Risk reads are intentionally outside the financial posting transaction. They are preflight signals, not financial truth. The posting store re-locks and re-validates wallet ownership, status, currency and balance immediately before money movement.

## Fraud decision flow

Internal fraud uses `InternalFraudEngine` and currency-aware rules.

If internal decision is `Deny`, the flow stops immediately because external `Allow` can never override internal `Deny`.

For internal `Allow` or `Review`, FakeFraud is required. External-provider timeout/network/malformed response is handled fail-closed: no financial posting starts.

Combined decision:

```text
Internal + External
        |
        v
FraudDecisionPolicy
        |
 Allow / Review / Deny
```

Behavior:

- Allow -> atomic posting starts;
- Review -> no money movement; HTTP layer returns review-required status;
- Deny -> no money movement;
- external fraud unavailable -> no money movement.

Manual review persistence/queue is not implemented yet. Until FraudEvents/review workflow exists, Review does not create a financial transaction or alter wallet balances.

## Atomic posting

Only after fraud returns final `Allow` does `SqlWalletTransferPostingStore` begin the financial transaction.

It commits as one MSSQL unit:

- durable idempotency;
- source wallet debit;
- destination wallet credit;
- FinancialTransaction;
- source/destination Wallet Liability ledger accounts;
- balanced LedgerJournal + LedgerEntries;
- persisted SQL Debit/Credit equality check.

No external HTTP call runs inside this SQL transaction.

## Double-entry accounting

Wallet-to-wallet transfer is a liability reclassification:

```text
Debit   source wallet liability       amount
Credit  destination wallet liability  amount
```

The journal is validated by Domain before persistence and by SQL aggregate after persistence, before COMMIT.

## Replay response

Completed transfer response contains immutable fields only:

- FinancialTransaction ID;
- source wallet ID;
- destination wallet ID;
- amount;
- currency;
- original completion time;
- replay flag.

Current wallet balances are deliberately excluded because they may have changed after the original transfer and would break deterministic replay semantics.
