# Tasarım Desenleri / Design Patterns

## Türkçe

FinWallet tasarım desenlerini isim kullanmak için değil, belirli finansal problemlere çözüm oldukları yerde kullanır. Gereksiz abstraction veya interface katmanı eklenmemelidir.

### DDD-lite
Entity, Value Object, Aggregate ve invariant kavramları finansal modelin sınırlarını açıklamak için kullanılır. Tam event-sourcing veya ağır DDD altyapısı yoktur.

### Adapter + Anti-Corruption Layer
`IBankProvider`, `IExternalFraudProvider` ve `ICommunicationGateway` gibi Application portları provider-specific DTO/enumları Domain'den ayırır. FakeBank/FakeFraud detayları Infrastructure içinde kalır.

### Application Orchestrator
Registration, bank-account opening ve transfer gibi use-case'ler handler'lar tarafından sıralı şekilde koordine edilir. Controller business workflow taşımaz.

### Chain of Responsibility / Rule Engine
Internal fraud değerlendirmesi bağımsız `IInternalFraudRule` kurallarından oluşur. Yeni bir fraud kuralı mevcut handler'ı büyük ölçüde değiştirmeden eklenebilir.

### Policy Pattern
Internal ve external fraud kararlarının birleşimi `FraudDecisionPolicy` gibi policy nesneleriyle açıkça tanımlanır.

### Explicit Unit of Work / SQL Transaction Boundary
Wallet transfer için generic UnitOfWork abstraction yerine `SqlWalletTransferPostingStore` transaction sınırını açıkça sahiplenir. Locking ve commit sırası business correctness'in parçasıdır.

### Idempotent Command
Para değiştiren request client tarafından `Idempotency-Key` taşır. MSSQL'deki durable record aynı key/same payload replay'ini güvenli yapar; same key/different payload conflict olur.

### Optimistic / Compare-and-Set Concurrency
BankAccount gibi lifecycle state update'lerinde expected status + timestamp ile CAS kullanılır. Wallet transfer gibi yoğun para hareketinde gerekli yerde explicit locking/Serializable uygulanır.

### Double-Entry Ledger
Her finansal etki Debit/Credit toplamı eşit bir journal ile temsil edilir. Correction geçmiş kaydı değiştirmek yerine reversal/compensation ile yapılır.

### State Machine
Authentication/session, BankAccount ve FinancialTransaction lifecycle'ları yalnız izin verilen state geçişleri üzerinden ilerler.

### Cache-Aside / TTL
Redis yalnız transient state için TTL tabanlı kullanılır. Financial source of truth değildir.

### Timeout / Safe Retry
Provider çağrılarında timeout vardır. Retry yalnız operasyon idempotency açısından güvenliyse uygulanmalıdır; arbitrary financial POST otomatik retry edilmez.

### Transactional Outbox / Inbox
Mimari olarak planlanmıştır fakat henüz production flow içinde uygulanmış değildir. Post-commit notification ve duplicate callback işleme için sıradaki desenlerden biridir.

### Saga / Compensation
Uzun external-bank workflow'larında gerektiğinde kullanılacaktır. Tek DB içindeki wallet transfer için saga kullanılmaz; tek MSSQL transaction daha doğru ve daha basittir.

### Reconciliation
Ledger/balance/provider statement uyuşmazlıkları sessizce düzeltilmez; issue olarak tespit edilip izlenmelidir. Reconciliation altyapısı planlanmıştır.

### CQRS-lite
Command/query kod organizasyonu ayrıştırılabilir; ayrı read database veya event-sourcing zorunlu değildir.

---

## English

FinWallet uses design patterns only where they solve a concrete financial problem, not to accumulate pattern names. Unnecessary abstraction or interface layers should be avoided.

### DDD-lite
Entities, Value Objects, Aggregates and invariants describe financial-model boundaries. The project does not use full event sourcing or heavyweight DDD infrastructure.

### Adapter + Anti-Corruption Layer
Application ports such as `IBankProvider`, `IExternalFraudProvider` and `ICommunicationGateway` keep provider-specific DTOs/enums out of Domain. FakeBank/FakeFraud details remain in Infrastructure.

### Application Orchestrator
Use cases such as registration, bank-account opening and transfer are coordinated by handlers. Controllers do not own business workflows.

### Chain of Responsibility / Rule Engine
Internal fraud evaluation is composed from independent `IInternalFraudRule` rules. New rules can be added without substantially rewriting the transfer handler.

### Policy Pattern
The combination of internal and external fraud decisions is explicitly represented through policy objects such as `FraudDecisionPolicy`.

### Explicit Unit of Work / SQL Transaction Boundary
For wallet transfer, `SqlWalletTransferPostingStore` explicitly owns the transaction boundary instead of hiding it behind a generic UnitOfWork abstraction. Locking and commit order are part of business correctness.

### Idempotent Command
Money-changing requests carry a client-generated `Idempotency-Key`. A durable MSSQL record safely replays the same key/same payload and rejects the same key/different payload.

### Optimistic / Compare-and-Set Concurrency
Lifecycle updates such as BankAccount use expected status + timestamp CAS. High-contention money movement such as wallet transfer uses explicit locking/Serializable where required.

### Double-Entry Ledger
Every financial effect is represented by a journal whose total Debit equals total Credit. Corrections use reversal/compensation rather than rewriting history.

### State Machine
Authentication/session, BankAccount and FinancialTransaction lifecycles progress only through allowed state transitions.

### Cache-Aside / TTL
Redis is used with TTL for transient state only. It is not a financial source of truth.

### Timeout / Safe Retry
Provider calls have timeouts. Retries are allowed only when operation-level idempotency makes them safe; arbitrary financial POSTs are not automatically retried.

### Transactional Outbox / Inbox
These are architectural plans but are not yet implemented in the production flow. They are intended for post-commit notifications and duplicate callback processing.

### Saga / Compensation
This will be used only where long-running external-bank workflows justify it. A single-database wallet transfer does not use a saga because one MSSQL transaction is simpler and more correct.

### Reconciliation
Ledger/balance/provider-statement mismatches are detected and investigated rather than silently corrected. Reconciliation infrastructure is planned.

### CQRS-lite
Command/query code organization may be separated, but a separate read database or event-sourcing infrastructure is not required.
