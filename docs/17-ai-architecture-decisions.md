# AI Destekli Mimari Karar Anlatımı / AI-Assisted Architecture Decision Narrative

> Bu belge gizli model düşünce zinciri değildir. Review edilebilir mühendislik gerekçelerini, trade-off'ları ve uygulama sırasını kaydeder. / This document is not a transcript of private model reasoning. It records reviewable engineering rationale, trade-offs and implementation order.

## Türkçe

### 1. Başlangıç problemi
Side project generic CRUD yerine şu birleşimi seçti:
```text
Digital Wallet + Double-entry Ledger + Fraud Detection + External Banking Integration
```
Amaç concurrency, idempotency, provider failure, accounting, session security, auditability ve reconciliation gibi gerçek financial-backend problemlerini göstermekti.

### 2. Neden modular monolith?
Wallet/Ledger/Transaction aynı atomik para hareketine katıldığı için başlangıçta microservice ayrıştırması distributed consistency maliyetini gereksiz erken getirirdi. Domain modülleri logical olarak ayrıdır; deployment sınırı gerektiğinde sonra değerlendirilebilir.

### 3. Neden MSSQL financial source of truth?
ACID transaction, constraint, FK, unique key, explicit locking ve deterministic reconciliation query ihtiyacı vardır. Redis hızlı transient state için uygundur ama para var/yok kararının authority'si değildir.

### 4. Neden auth önce?
Wallet, BankAccount ve Transfer ownership kontrolü stabil customer/session identity ister. Bu yüzden registration -> OTP -> password -> login -> JWT -> refresh/session sırası money endpointlerinden önce kuruldu.

### 5. Neden JWT + server-side session?
JWT request authentication sağlar fakat tek başına immediate revoke sağlamaz. Minimal `sid` claim ve durable CustomerSession high-risk flow'da revoke/device/session kontrolü sağlar.

### 6. Neden Wallet ve BankAccount ayrı?
Wallet internal customer liability/balance state'tir. BankAccount external provider relationship'idir. Lifecycle ve source of truth farklı olduğu için aynı aggregate yapılmadı.

### 7. Neden FakeBank ayrı API?
External integration problemlerini gerçekten simüle etmek için HTTP boundary zorunlu tutuldu: timeout, 5xx, pending, provider-generated ID, idempotency ve polling. FinWallet provider DB'sine erişmez.

### 8. Neden durable state bank HTTP'den önce?
Bank-account opening önce internal `BankAccount(Opening)` kaydeder, SQL'i kapatır, sonra provider çağırır. Durable internal ID deterministic provider request key üretir; lost response duplicate external account'a dönüşmez.

### 9. Neden ledger transfer endpointinden önce?
Balance yalnız “şu an ne kadar var?” sorusunu cevaplar. Ledger “bu balance neden var?” sorusunun geçmişini sağlar. Bu nedenle money endpoint ledger invariant oluşmadan açılmadı.

### 10. FinancialTransaction neden LedgerJournal'dan ayrı?
FinancialTransaction business operation/lifecycle'dır; LedgerJournal accounting effect'tir. Failure/reversal/idempotency/resource identity ve reconciliation için ayrı kavramlar daha temizdir.

### 11. Neden explicit transfer store?
`SqlWalletTransferPostingStore` idempotency, iki wallet update'i, FinancialTransaction ve Ledger'ı tek MSSQL transaction'da sahiplenir. Bu bölgede locking/commit sırası correctness'in parçası olduğu için generic repository abstraction yerine explicit SQL tercih edildi.

### 12. Neden durable idempotency MSSQL'de?
Mobile timeout, gateway timeout, duplicate click veya retry aynı financial request'i tekrar gönderebilir. Final duplicate guarantee process cache/Redis'e bırakılmaz. `Scope + CustomerId + Key` + canonical payload hash kullanılır.

### 13. Neden fraud SQL money transaction'dan önce?
External provider HTTP uzun ve belirsizdir. Fraud önce değerlendirilir; yalnız final Allow sonrası kısa atomic financial SQL transaction açılır. Required fraud unavailable ise fail-closed.

### 14. Neden completed replay fraud'dan önce?
Dün completed olmuş aynı request bugün değişen fraud rule'ları yüzünden retry'da Deny olmamalıdır. Completed immutable result önce replay edilir.

### 15. Neden controller-based API?
Enterprise Web API convention, attributes, Swagger ve tutarlı action contractları için tüm HTTP servisleri controller standardında tutuldu; Minimal API ile iki convention karıştırılmadı.

### 16. ServiceResult neden yalnız HTTP contract?
`ServiceResult<T>` client response standardıdır. Domain/Application'ın HTTP envelope'a dependency alması business model ile transport katmanını gereksiz bağlardı.

### 17. YARP neden financial core'dan sonra?
Routing infrastructure yanlış para modelini düzeltmez. Auth, persistence, ledger ve transfer correctness kurulduktan sonra Gateway edge/platform boundary olarak eklendi.

### 18. Neden service-to-service de Gateway?
FinWallet provider adapterları `/providers/*` kullanır. İki credential vardır:
```text
FinWallet -> Gateway   InternalServiceKey
Gateway -> Backend     DownstreamServiceKey
```
Bu Gateway'i yalnız client convention değil enforceable trust boundary yapar.

### 19. Neden Shared.Web?
Yedi Web API'ye ayrı ayrı Swagger/rate/CORS/header kodu kopyalamak configuration drift yaratırdı. `FinWallet.Shared.Web` yalnız host cross-cutting concern'leri merkezileştirir; business framework olmaz.

### 20. Neden bazı değerler config, bazıları değil?
Adres, timeout, pool, rate limit, body/header/connection limit, CORS, Swagger ve YARP destination operasyonel olduğu için config'tir. HS256 algorithm, PBKDF2 V1 migration semantics, `Debit=Credit`, financial decimal ve durable idempotency semantics correctness/security invariant olduğu için arbitrary runtime toggle değildir.

### 21. Neden Redis general cache olmadı?
Redis current use-case'te OTP/transient state için yeterlidir. Wallet balances veya final idempotency truth Redis'e taşınmadı; gereksiz cache invalidation ve correctness riski yaratılmadı.

### 22. Neden mock var ama yeterli sayılmıyor?
xUnit + Moq Application orchestration call davranışını test eder. SQL Serializable/range lock veya YARP route correctness gerçek infrastructure test ister.

### 23. Uygulama sırası
```text
scope/invariants
-> architecture boundaries
-> registration/auth/session
-> communication/fraud providers
-> wallet/ledger domain
-> FakeBank + BankAccount
-> persistence
-> wallet APIs
-> financial transaction/idempotency schema
-> atomic transfer store
-> fraud/session transfer orchestration
-> YARP Gateway
-> Shared.Web + Swagger/security
-> config/performance tuning
-> xUnit/Moq + CI tests
-> bilingual/current documentation
```

### 24. Sıradaki doğal sıra
1. BankDeposit;
2. BankWithdrawal + cutoff;
3. integration/concurrency tests;
4. Outbox/Inbox + notification;
5. FraudEvents/manual review;
6. transaction history;
7. refund/reversal;
8. reconciliation;
9. OpenTelemetry/masked logging/operations.

### 25. Temel kural
```text
Kolay endpoint'i, altındaki zor invariant hazır olmadan açma.
```
Transfer ledger/idempotency olmadan; bank flow durable state olmadan; provider retry idempotency olmadan expose edilmez.

---

## English

### 1. Starting problem
The side project selected a financial combination rather than generic CRUD:
```text
Digital Wallet + Double-entry Ledger + Fraud Detection + External Banking Integration
```
The goal was to exercise real backend concerns such as concurrency, idempotency, provider failures, accounting, session security, auditability and reconciliation.

### 2. Why a modular monolith?
Wallet, Ledger and Transaction participate in the same atomic money movement. Splitting them into microservices immediately would introduce distributed-consistency costs too early. Domain modules remain logically separated and deployment boundaries can be reconsidered later.

### 3. Why MSSQL as the financial source of truth?
The system needs ACID transactions, constraints, FKs, unique keys, explicit locking and deterministic reconciliation queries. Redis is useful for fast transient state but is not allowed to decide whether money exists.

### 4. Why authentication first?
Wallet, BankAccount and Transfer ownership require stable customer/session identity. Registration -> OTP -> password -> login -> JWT -> refresh/session therefore came before money endpoints.

### 5. Why JWT plus server-side sessions?
JWT authenticates requests but does not independently provide immediate revoke. A minimal `sid` claim plus durable CustomerSession provides revoke/device/session checks for high-risk flows.

### 6. Why separate Wallet and BankAccount?
Wallet is internal customer liability/balance state. BankAccount is an external-provider relationship. Their lifecycle and source of truth differ, so they are not one aggregate.

### 7. Why FakeBank as a separate API?
An HTTP boundary forces the project to handle actual integration concerns: timeout, 5xx, pending state, provider-generated identity, idempotency and polling. FinWallet never accesses provider storage directly.

### 8. Why durable state before bank HTTP?
Bank-account opening persists internal `BankAccount(Opening)`, closes SQL work, then calls the provider. The durable internal ID generates a deterministic provider request key so lost responses do not create duplicate external accounts.

### 9. Why Ledger before the transfer endpoint?
Balance answers “how much exists now?” Ledger answers “why does that balance exist?”. Money endpoints were not exposed before accounting invariants existed.

### 10. Why FinancialTransaction separate from LedgerJournal?
FinancialTransaction represents the business operation/lifecycle; LedgerJournal represents the accounting effect. Keeping them separate supports failure, reversal, idempotency resource identity and reconciliation.

### 11. Why an explicit transfer store?
`SqlWalletTransferPostingStore` owns idempotency, both wallet updates, FinancialTransaction and Ledger in one MSSQL transaction. Locking and commit order are correctness rules here, so explicit SQL is preferred over a generic repository abstraction.

### 12. Why durable idempotency in MSSQL?
Mobile timeout, Gateway timeout, duplicate clicks and retries can resend the same financial command. Final duplicate protection is not delegated to process cache or Redis. The identity uses `Scope + CustomerId + Key` plus a canonical payload hash.

### 13. Why fraud before the money SQL transaction?
External HTTP is slow and uncertain. Fraud is evaluated first; a short atomic financial SQL transaction starts only after final Allow. Required fraud unavailable => fail closed.

### 14. Why completed replay before fraud?
A transaction completed yesterday should not be denied today when the client simply retries the same request under changed fraud rules. Completed immutable results are replayed first.

### 15. Why controller-based APIs?
All HTTP services use controllers for consistent enterprise Web API conventions, attributes, Swagger and explicit action contracts. Minimal API conventions are not mixed into the same codebase.

### 16. Why is ServiceResult only an HTTP contract?
`ServiceResult<T>` standardizes client responses. Making Domain/Application depend on it would unnecessarily couple business objects to HTTP transport.

### 17. Why YARP after financial core?
Routing infrastructure cannot fix incorrect money logic. Gateway was added as the edge/platform boundary after auth, persistence, ledger and transfer correctness were established.

### 18. Why service-to-service through Gateway?
Provider adapters use `/providers/*`. Two credentials exist:
```text
FinWallet -> Gateway   InternalServiceKey
Gateway -> Backend     DownstreamServiceKey
```
This makes Gateway an enforceable trust boundary rather than only a client convention.

### 19. Why Shared.Web?
Duplicating Swagger/rate/CORS/header code across seven Web APIs would cause configuration drift. `FinWallet.Shared.Web` centralizes hosting concerns without becoming a business framework.

### 20. Why are some values configurable and others not?
Addresses, timeouts, pools, rate limits, body/header/connection limits, CORS, Swagger and YARP destinations are operational and therefore configurable. HS256 algorithm, PBKDF2 V1 migration semantics, `Debit=Credit`, financial decimals and durable idempotency semantics remain correctness/security invariants rather than arbitrary runtime toggles.

### 21. Why not expand Redis into a general cache?
Current use focuses on OTP/transient state. Wallet balances and final idempotency truth remain in MSSQL to avoid unnecessary invalidation and correctness risks.

### 22. Why add mocks but not treat them as sufficient?
xUnit + Moq can verify Application orchestration calls. SQL Serializable/range-lock behavior and YARP route correctness require real infrastructure tests.

### 23. Implementation sequence
```text
scope/invariants
-> architecture boundaries
-> registration/auth/session
-> communication/fraud providers
-> wallet/ledger domain
-> FakeBank + BankAccount
-> persistence
-> wallet APIs
-> financial transaction/idempotency schema
-> atomic transfer store
-> fraud/session transfer orchestration
-> YARP Gateway
-> Shared.Web + Swagger/security
-> config/performance tuning
-> xUnit/Moq + CI tests
-> bilingual/current documentation
```

### 24. Natural next order
1. BankDeposit;
2. BankWithdrawal + cutoff;
3. integration/concurrency tests;
4. Outbox/Inbox + notification;
5. FraudEvents/manual review;
6. transaction history;
7. refund/reversal;
8. reconciliation;
9. OpenTelemetry/masked logging/operations.

### 25. Core rule
```text
Do not expose the easy endpoint before the hard invariant underneath it exists.
```
Transfer is not exposed before ledger/idempotency; bank flows should not be exposed before durable state; provider retries should not exist without idempotency.
