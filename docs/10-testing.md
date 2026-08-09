# Testing Strategy

## Current status

Before the gateway/platform hardening phase, the solution did not contain an xUnit test project and did not use Moq. This was a real testing gap rather than a documentation gap.

The solution now includes `tests/FinWallet.Application.Tests` using:

- xUnit v3;
- Moq;
- .NET 8 / Microsoft Testing Platform integration.

The first test covers `OpenBankAccountHandler` and uses strict mocks for `IWalletStore`, `IBankAccountStore` and `IBankProvider`. It verifies that a missing owned wallet fails before any external-bank call is attempted.

## What should be mocked

Unit tests should mock boundaries whose behavior is not the subject of the test:

- `IBankProvider`;
- `IExternalFraudProvider`;
- `ICommunicationGateway`;
- Application persistence interfaces when testing pure orchestration;
- time through `TimeProvider` where time-sensitive behavior is being tested.

Strict mocks are preferred for financial orchestration tests because unexpected provider/database calls are usually meaningful bugs.

## What should not be proven with mocks

Mocks cannot prove financial persistence correctness. The following behaviors require integration/concurrency tests against real infrastructure:

- MSSQL transaction rollback;
- `UPDLOCK` / `HOLDLOCK` / Serializable behavior;
- unique-key races;
- refresh-token compare-and-set;
- wallet-transfer deterministic row-lock ordering;
- idempotency range locking;
- double-entry SQL SUM(Debit) = SUM(Credit) verification;
- SQL connection pooling behavior;
- Redis Lua atomicity;
- Redis reconnect/fail-closed behavior;
- YARP routing, authorization, health checks and load balancing.

## Required next integration suite

A future `FinWallet.IntegrationTests` project should provision isolated MSSQL and Redis instances and execute at minimum:

1. Concurrent duplicate registration.
2. Concurrent refresh-token rotation.
3. Ten identical transfer requests with one `Idempotency-Key`.
4. Same key with a different transfer payload.
5. One hundred concurrent debits from one source wallet.
6. Opposite-direction transfers to expose deadlock regressions.
7. Redis unavailable during OTP verification.
8. Fraud provider timeout before transfer posting.
9. Gateway request without JWT.
10. Direct backend request without downstream service key.
11. Gateway rate-limit rejection.
12. Ledger imbalance injection proving rollback.

## Financial invariant test

A load/concurrency test should always reconcile the durable result instead of trusting HTTP success counts alone:

```text
Initial wallet balances
+ durable credits
- durable debits
= final wallet balances

and

SUM(Ledger Debit) = SUM(Ledger Credit)
```

For a controlled test beginning with 100,000 units and many concurrent transfer attempts, successful `FinancialTransactions`, final wallet balances and ledger journals must reconcile exactly.

## CI policy

CI must restore, build the complete solution in Release mode with warnings treated as errors, then execute the test projects. New projects must be included in `FinWallet.sln`; otherwise a green solution build would not validate them.

## Mocking conclusion

Mocking is now present for Application orchestration. It is intentionally not used as a substitute for real MSSQL/Redis/YARP tests. The financial concurrency and infrastructure test suite remains a release-hardening requirement.
