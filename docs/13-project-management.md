# Proje Yönetimi ve Teslimat Yol Haritası / Project Management and Delivery Roadmap

## Türkçe

### Amaç
GitHub issue'ları authoritative work item'dır. Uygulama `agent/*` branch'lerinde geliştirilir ve PR ile `main`'e alınır.

### Board modeli
Önerilen kolonlar:
1. Backlog
2. Ready
3. In Progress
4. In Review
5. Blocked
6. Done

Project board yoksa eşdeğer `status:*` label kullanılabilir.

### Milestone'lar
| Milestone | Kapsam | Güncel durum |
|---|---|---|
| M0 Architecture Baseline | scope, architecture, security, docs | Büyük ölçüde tamamlandı |
| M1 Foundation | solution/domain foundations | Tamamlandı |
| M2 Identity & Registration | OTP, password, JWT, refresh/session | Ana akış tamamlandı; logout açık |
| M3 Persistence & Concurrency | MSSQL, Redis, idempotency | Transfer için ana temel tamamlandı; outbox/inbox açık |
| M4 Financial Core | ledger, transaction, reversal model | Ledger + transfer tamamlandı; public reversal/refund açık |
| M5 External Providers | FakeBank/Fraud/Cutoff/Campaign/Communication | Simulator baseline tamamlandı |
| M6 Financial Flows | deposit, withdrawal, transfer, purchase, refund | Wallet transfer tamamlandı; diğerleri açık |
| M7 Reconciliation & Hardening | reconciliation, observability, chaos/integration tests | Açık |
| M8 Gateway/Platform | YARP, Swagger, rate-limit, security baseline | Tamamlandı |

### Güncel tamamlanan ana çalışmalar
- registration/auth/session baseline;
- Wallet/BankAccount persistence;
- Fake provider APIs;
- double-entry ledger schema/domain;
- durable idempotent wallet transfer;
- fraud orchestration;
- YARP Gateway + downstream trust boundary;
- Swagger tüm Web API'lerde;
- appsettings-based platform/performance tuning;
- xUnit v3 + Moq başlangıç test projesi;
- Release build + test CI;
- çift dilli dokümantasyon standardı.

### Açık öncelikler
1. BankDeposit.
2. BankWithdrawal + cutoff.
3. Real MSSQL/Redis/YARP integration/concurrency tests.
4. Outbox/Inbox + reliable notification.
5. Durable FraudEvents/manual review.
6. Transaction history/read model.
7. Refund/Reversal public flows.
8. Reconciliation.
9. Centralized masked logging/OpenTelemetry/alerting.
10. Production deployment hardening.

### Roller
- Solution Architect: boundaries/ADR/dependency direction.
- Security/Auth: auth/session/OTP/token controls.
- Financial Domain: wallet/ledger/transaction/accounting.
- Persistence/Concurrency: SQL/Redis/idempotency/locking.
- Integration: provider adapters/resilience/contracts.
- QA/Chaos: concurrency/duplication/failure scenarios.
- Code Review: architecture/security/financial correctness.
- Documentation: TR+EN docs ve code-doc sync.

### PR workflow
1. Issue/acceptance criteria belirlenir.
2. Current `main`'den bounded branch açılır.
3. Kod + test + docs aynı feature scope içinde güncellenir.
4. Release build/test yapılır.
5. Draft PR açılır.
6. Review/CI bulguları kapatılır.
7. DoD sağlanınca merge edilir.

### DoD
Code, test, TR/EN XML docs, affected TR/EN Markdown docs, security impact, failure behavior, concurrency/idempotency ve package inventory tamamlanmadan issue Done olmaz.

---

## English

### Purpose
GitHub issues are authoritative work items. Implementation occurs on `agent/*` branches and is integrated into `main` through pull requests.

### Board model
Recommended columns:
1. Backlog
2. Ready
3. In Progress
4. In Review
5. Blocked
6. Done

Equivalent `status:*` labels may be used until a Project board is attached.

### Milestones
| Milestone | Scope | Current status |
|---|---|---|
| M0 Architecture Baseline | scope, architecture, security, docs | Largely complete |
| M1 Foundation | solution/domain foundations | Complete |
| M2 Identity & Registration | OTP, password, JWT, refresh/session | Main flow complete; logout open |
| M3 Persistence & Concurrency | MSSQL, Redis, idempotency | Core transfer baseline complete; outbox/inbox open |
| M4 Financial Core | ledger, transaction, reversal model | Ledger + transfer complete; public reversal/refund open |
| M5 External Providers | FakeBank/Fraud/Cutoff/Campaign/Communication | Simulator baseline complete |
| M6 Financial Flows | deposit, withdrawal, transfer, purchase, refund | Wallet transfer complete; others open |
| M7 Reconciliation & Hardening | reconciliation, observability, chaos/integration tests | Open |
| M8 Gateway/Platform | YARP, Swagger, rate limit, security baseline | Complete |

### Major completed work
- registration/auth/session baseline;
- Wallet/BankAccount persistence;
- Fake provider APIs;
- double-entry ledger schema/domain;
- durable idempotent wallet transfer;
- fraud orchestration;
- YARP Gateway + downstream trust boundary;
- Swagger across all Web APIs;
- appsettings-based platform/performance tuning;
- initial xUnit v3 + Moq test project;
- Release build + test CI;
- bilingual documentation standard.

### Open priorities
1. BankDeposit.
2. BankWithdrawal + cutoff.
3. Real MSSQL/Redis/YARP integration/concurrency tests.
4. Outbox/Inbox + reliable notification.
5. Durable FraudEvents/manual review.
6. Transaction history/read model.
7. Public Refund/Reversal flows.
8. Reconciliation.
9. Centralized masked logging/OpenTelemetry/alerting.
10. Production deployment hardening.

### Roles
- Solution Architect: boundaries/ADR/dependency direction.
- Security/Auth: auth/session/OTP/token controls.
- Financial Domain: wallet/ledger/transaction/accounting.
- Persistence/Concurrency: SQL/Redis/idempotency/locking.
- Integration: provider adapters/resilience/contracts.
- QA/Chaos: concurrency/duplication/failure scenarios.
- Code Review: architecture/security/financial correctness.
- Documentation: TR+EN docs and code-doc synchronization.

### Pull-request workflow
1. Define issue/acceptance criteria.
2. Create a bounded branch from current `main`.
3. Update code + tests + docs within the same feature scope.
4. Run Release build/tests.
5. Open a draft PR.
6. Resolve review/CI findings.
7. Merge after DoD is satisfied.

### DoD
An issue is not Done until code, tests, TR/EN XML docs, affected TR/EN Markdown docs, security impact, failure behavior, concurrency/idempotency considerations and package inventory are complete.
