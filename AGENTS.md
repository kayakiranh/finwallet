# FinWallet Agent Kuralları / Agent Rules

> Bu dosya coding/review/documentation agent'ları için repository-level source of truth'tür. / This file is the repository-level source of truth for coding, review and documentation agents.

## Türkçe

### Ürün
FinWallet .NET 8 multi-currency digital wallet side projectidir. Güncel core: customer auth/session, Wallet/BankAccount, double-entry ledger, durable idempotent wallet transfer, internal/external fraud ve YARP Gateway. BankDeposit/Withdrawal, reconciliation, outbox/inbox ve centralized observability henüz tamamlanmış değildir.

### Sabit teknoloji kararları
- .NET 8 / ASP.NET Core controller-based Web API.
- MSSQL financial source of truth.
- Redis transient support state; financial authority değildir.
- JWT access token + custom refresh/session.
- ASP.NET Core Identity yok.
- YARP Gateway normal public ve FinWallet->provider HTTP trafiğinin boundary'sidir.
- Paid/freemium NuGet yasak; fully free/open-source ve dokümante package gerekir.
- Built-in DI.
- Package versionları merkezi yönetilir.

### HTTP standardı
- Minimal API business endpoint mapping yasaktır.
- Tüm HTTP projectleri `AddControllers` / `MapControllers` / `ControllerBase` kullanır.
- `Program.cs` composition/bootstrap içindir.
- API response body'leri `ServiceResult<T>` standardını kullanır.
- Domain/Application `ServiceResult<T>` bilmez.
- API DTO provider transport modelini Domain'e sızdırmaz.
- Protected client traffic Gateway JWT kontrolünden geçer; FinWallet.Api auth/ownership kontrolünü tekrar yapar.
- Provider traffic `/providers/*` üzerinden Gateway'e gider ve internal/downstream service credential modelini kullanır.

### Mimari
- Ana uygulama modular monolith'tir.
- Dependency direction: `Api -> Application -> Domain`; Infrastructure portları implement eder.
- External simulatorlar: FakeBank, FakeFraud, FakeCutoff, FakeCampaign, FakeCommunication.
- Provider DTO/status Domain'e sızmaz; Adapter + Anti-Corruption Layer kullanılır.
- Wallet/Ledger/Transaction core için full microservice decomposition yok.
- Event Sourcing yok.
- Generic repository/service framework zorunlu değil; correctness-critical SQL explicit kalabilir.
- CQRS-lite yalnız code organization olarak kabul edilir.

### Finansal invariant'lar
- Her money movement double-entry ledger'da temsil edilir.
- Posted journal: total Debit == total Credit.
- Ledger normal operasyonda append-only; correction reversal/compensation ile yapılır.
- Wallet balance current state'tir; ledger authoritative financial history'dir.
- Currency mismatch commit öncesi fail eder.
- Completed transaction processing state'e geri dönmez.
- Redis financial write correctness'in tek garantisi olamaz.
- External HTTP açık financial SQL transaction içinde çalışmaz.

### Concurrency / idempotency
- Money-changing command idempotent olmalıdır.
- Final durable guarantee MSSQL unique/locking state'tir.
- Same key + different payload fail eder.
- Wallet concurrency MSSQL atomic operation/constraint/locking ile korunur.
- Redis lock varsa yalnız secondary coordination'dır.
- Duplicate callback ileride Inbox/idempotent-consumer semantics ile safe olmalıdır.

### Authentication
- Interactive user = Customer.
- Credential/session/refresh token ayrı concern/table.
- Password algorithm arbitrary appsettings switch değildir; migration versioned olmalıdır.
- Registration country + phone-prefix policy ile sınırlıdır.
- OTP FakeCommunication üzerinden gider; transient state Redis'tedir.

### Fraud
- Internal rule-based fraud + FakeFraud external provider vardır.
- Client risk flag'lerine güvenilmez; signals server state'ten türetilir.
- Final policy internal/external decision combine eder.
- Required fraud failure financial operation'da conservative/fail-closed olmalıdır.
- Durable FraudEvents henüz açık iştir.

### External bank / cutoff / campaign
- FakeBank gerçek external provider gibi HTTP üzerinden davranır.
- Internal/external IDs ayrı tutulur.
- Long-running bank operations SQL transaction açık tutmaz; state/compensation yaklaşımı kullanır.
- Cutoff calendar logic FakeCutoff'a, campaign eligibility FakeCampaign'e aittir.
- FinWallet accounting etkisinden her zaman sorumludur.

### Reconciliation ve notification
- Wallet vs Ledger ve bank-related internal state vs FakeBank statement reconcile edilmelidir.
- Mismatch sessiz balance rewrite ile düzeltilmez.
- Notification failure completed money transaction'ı rollback etmez.
- Transactional Outbox planlanmıştır fakat henüz tamamlanmamıştır.

### Logging
- Password, OTP, JWT, refresh token, Authorization header, service key ve secret loglanmaz.
- Phone/email/IBAN/account identifier maskelenmelidir.
- CorrelationId, TransactionId ve provider reference ayrı kavramlardır.
- Centralized structured logging/telemetry halen roadmap işidir; uygulanmış gibi varsayılmamalıdır.

### Code documentation
- Uygun manually-written declaration'larda XML docs gerekir.
- Summary/param/returns/exception açıklamaları TR: ve EN: içerir.
- `CS1591` error olarak uygulanır.
- Root README ve `docs/**/*.md` Türkçe + İngilizce tutulur.
- Code davranışı değişince iki dil de aynı PR'da güncellenir.

### Test beklentisi
Financial feature için unit + uygun integration/concurrency/E2E test gerekir. Kritik senaryolar: overspend concurrency, duplicate idempotency, Redis outage, provider timeout, refresh-token reuse, ledger imbalance, Gateway bypass/rate limit ve reconciliation mismatch.

Moq gerçek SQL/Redis/YARP concurrency garantisinin yerine geçmez.

### Review önceliği
1. Financial correctness
2. Security
3. Data consistency / idempotency
4. Recoverability / reconciliation
5. Observability
6. Maintainability
7. Performance
8. Convenience

---

## English

### Product
FinWallet is a .NET 8 multi-currency digital-wallet side project. The current core includes customer auth/session, Wallet/BankAccount, double-entry ledger, durable idempotent wallet transfer, internal/external fraud and YARP Gateway. BankDeposit/Withdrawal, reconciliation, outbox/inbox and centralized observability are not yet complete.

### Fixed technology decisions
- .NET 8 / ASP.NET Core controller-based Web API.
- MSSQL is the financial source of truth.
- Redis is transient support state, never the financial authority.
- JWT access tokens plus custom refresh/session model.
- No ASP.NET Core Identity.
- YARP Gateway is the boundary for normal public and FinWallet-to-provider HTTP traffic.
- Paid/freemium NuGet packages are forbidden; third-party packages must be fully free/open-source and documented.
- Built-in DI.
- Central package-version management.

### HTTP standard
- Minimal API business endpoint mappings are forbidden.
- Every HTTP project uses `AddControllers`, `MapControllers` and `ControllerBase`.
- `Program.cs` is for composition/bootstrap.
- API response bodies use `ServiceResult<T>`.
- Domain/Application do not depend on `ServiceResult<T>`.
- API DTOs do not leak provider transport models into Domain.
- Protected client traffic passes Gateway JWT validation; FinWallet.Api independently repeats auth/ownership checks.
- Provider traffic uses Gateway `/providers/*` and the internal/downstream service-credential model.

### Architecture
- Main application is a modular monolith.
- Dependency direction: `Api -> Application -> Domain`; Infrastructure implements ports.
- External simulators: FakeBank, FakeFraud, FakeCutoff, FakeCampaign, FakeCommunication.
- Provider DTO/status types remain behind Adapter + Anti-Corruption Layer.
- No full microservice decomposition for Wallet/Ledger/Transaction core.
- No Event Sourcing.
- No mandatory generic repository/service framework; correctness-critical SQL may remain explicit.
- CQRS-lite is allowed only as code organization.

### Financial invariants
- Every money movement is represented in the double-entry ledger.
- Posted journals satisfy total Debit == total Credit.
- Ledger is append-only in normal operation; corrections use reversal/compensation.
- Wallet balance is current state; ledger is authoritative financial history.
- Currency mismatch fails before commit.
- Completed transactions do not return to processing.
- Redis cannot be the sole guarantee for financial writes.
- External HTTP never runs inside an open financial SQL transaction.

### Concurrency / idempotency
- Money-changing commands must be idempotent.
- Final durable guarantees belong to MSSQL unique/locking state.
- Same key + different payload fails.
- Wallet concurrency is protected by MSSQL atomic operations, constraints and locking.
- Redis locks, if used, are secondary coordination only.
- Duplicate callbacks should be safe through future Inbox/idempotent-consumer semantics.

### Authentication
- Every interactive user is a Customer.
- Credential/session/refresh-token concerns are separate.
- Password algorithm is not an arbitrary appsettings switch; changes require versioned migration.
- Registration is restricted by country + phone-prefix policy.
- OTP is delivered through FakeCommunication with transient Redis state.

### Fraud
- FinWallet has internal rule-based fraud plus FakeFraud external evaluation.
- Client-supplied risk flags are not trusted; signals are server-derived.
- Final policy combines internal and external decisions.
- Required fraud failure must be conservative/fail-closed for financial operations.
- Durable FraudEvents remain open work.

### External bank / cutoff / campaign
- FakeBank behaves as a real external provider over HTTP.
- Internal and external references remain separate.
- Long-running bank operations never hold SQL transactions during HTTP; they use persisted state/compensation approaches.
- Cutoff calendar logic belongs to FakeCutoff and campaign eligibility to FakeCampaign.
- FinWallet always owns accounting effects.

### Reconciliation and notification
- Wallet vs Ledger and bank-related internal state vs FakeBank statements must reconcile.
- Mismatches are never silently fixed by rewriting balances.
- Notification failure does not roll back a completed money transaction.
- Transactional Outbox is planned but not yet complete.

### Logging
- Never log passwords, OTPs, JWTs, refresh tokens, Authorization headers, service keys or secrets.
- Phone/email/IBAN/account identifiers must be masked.
- CorrelationId, TransactionId and provider references are distinct.
- Centralized structured logging/telemetry remains roadmap work and must not be assumed implemented.

### Code documentation
- Applicable manually written declarations require XML docs.
- Summary/param/returns/exception documentation contains both TR: and EN:.
- `CS1591` is treated as an error.
- Root README and `docs/**/*.md` are maintained in Turkish + English.
- When code behavior changes, both languages are updated in the same PR.

### Testing expectations
Financial features require unit plus appropriate integration/concurrency/E2E tests. Critical scenarios include overspend concurrency, duplicate idempotency, Redis outage, provider timeout, refresh-token reuse, ledger imbalance, Gateway bypass/rate limits and reconciliation mismatch.

Moq does not replace real SQL/Redis/YARP concurrency guarantees.

### Review priority
1. Financial correctness
2. Security
3. Data consistency / idempotency
4. Recoverability / reconciliation
5. Observability
6. Maintainability
7. Performance
8. Convenience
