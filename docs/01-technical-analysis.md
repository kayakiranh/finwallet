# Teknik Analiz / Technical Analysis

## Türkçe

### 1. Amaç
FinWallet'ın teknik hedefi; para hareketlerinin doğru, tekrar çalıştırılabilir, izlenebilir ve dış servis hatalarına dayanıklı olduğu bir finansal backend örneği oluşturmaktır. Sistem yalnız başarılı senaryoya değil; duplicate request, concurrency, provider timeout, fraud failure, session revoke ve veri tutarlılığı problemlerine göre tasarlanır.

### 2. Fonksiyonel kapsam
Güncel uygulanmış kapsam:
- customer registration + OTP verification;
- login, JWT access token, refresh-token rotation, server-side session;
- TRY/USD/EUR wallet create/list;
- external bank-account opening through FakeBank;
- internal/external fraud evaluation;
- durable idempotent wallet-to-wallet transfer;
- double-entry ledger;
- YARP Gateway üzerinden client ve provider trafiği;
- Swagger, rate limit ve ortak HTTP güvenlik kontrolleri.

Planlanan fakat henüz tamamlanmayan finansal kapsam:
- BankDeposit;
- BankWithdrawal;
- merchant purchase/campaign accounting;
- refund/reversal public flows;
- durable manual fraud review;
- outbox/inbox;
- reconciliation;
- transaction-history read model.

### 3. Kritik kalite gereksinimleri
**Financial correctness:** Wallet balance, FinancialTransaction, IdempotencyRecord ve Ledger aynı finansal transaction sınırında tutarlı commit edilmelidir.

**Idempotency:** Aynı para hareketi retry edildiğinde ikinci kez para hareketi oluşmamalıdır. Aynı key farklı payload ile kullanılırsa conflict oluşmalıdır.

**Concurrency:** Aynı wallet'tan eş zamanlı harcamalar overspend yaratmamalıdır. MSSQL final authority'dir.

**Security:** Public trafik Gateway JWT kontrolünden geçer; service-level JWT/ownership tekrar doğrulanır. Provider rotaları ayrı internal/downstream service credential kullanır.

**Availability:** Provider timeout veya 5xx durumları finansal doğruluğu bozmamalıdır. Fraud gibi zorunlu kararlar fail-closed davranır.

**Auditability:** Finansal hareketler ledger ve immutable business transaction kayıtlarıyla açıklanabilir olmalıdır.

### 4. Veri kaynakları
- **MSSQL:** müşteri, authentication/session, wallet, bank account, transaction, idempotency ve ledger için durable source of truth.
- **Redis:** OTP gibi transient state. Para doğruluğunun kaynağı değildir.
- **Fake providers:** Harici sistem davranışlarını simüle eder; FinWallet tablolarını doğrudan değiştiremez.

### 5. Runtime sınırları
```text
Client -> YARP Gateway -> FinWallet.Api
FinWallet.Api -> YARP Gateway -> Fake providers
```

Gateway routing/security/traffic yönetir. Business authorization ve financial rules Gateway'e taşınmaz.

### 6. Performans yaklaşımı
- SqlClient connection pooling kullanılır.
- Redis `ConnectionMultiplexer` singleton tutulur.
- HttpClientFactory + `SocketsHttpHandler` connection pooling kullanılır.
- YARP destination connection/timeouts ve load-balancing config'ten yönetilir.
- Finansal doğruluk için gerekli SQL isolation/locking yalnız benchmark uğruna zayıflatılmaz.

### 7. Test yaklaşımı
Moq yalnız Application orchestration sınırları için kullanılır. Gerçek MSSQL locking, Redis Lua ve YARP routing davranışları mock ile doğrulanmış sayılmaz; integration/concurrency testleri gerektirir.

### 8. Mevcut en önemli gap
Yeni wallet sıfır bakiye ile açılır ve public BankDeposit endpoint'i henüz yoktur. Bu nedenle public API kullanarak tamamen uçtan uca `register -> fund -> transfer` akışı henüz tamamlanamaz. Bu, projenin sıradaki en önemli fonksiyonel eksikliğidir.

---

## English

### 1. Purpose
FinWallet's technical goal is to provide a financial-backend example in which money movements are correct, safely retryable, traceable and resilient to external-service failures. The system is designed not only for happy paths but also for duplicate requests, concurrency, provider timeouts, fraud failures, session revocation and data-consistency problems.

### 2. Functional scope
Currently implemented:
- customer registration + OTP verification;
- login, JWT access token, refresh-token rotation and server-side sessions;
- TRY/USD/EUR wallet create/list;
- external bank-account opening through FakeBank;
- internal/external fraud evaluation;
- durable idempotent wallet-to-wallet transfer;
- double-entry ledger;
- client and provider traffic through YARP Gateway;
- Swagger, rate limiting and shared HTTP security controls.

Planned but not yet complete:
- BankDeposit;
- BankWithdrawal;
- merchant purchase/campaign accounting;
- public refund/reversal flows;
- durable manual fraud review;
- outbox/inbox;
- reconciliation;
- transaction-history read model.

### 3. Critical quality requirements
**Financial correctness:** Wallet balance, FinancialTransaction, IdempotencyRecord and Ledger changes must commit consistently inside the same financial transaction boundary.

**Idempotency:** Retrying the same money-changing operation must not move money twice. Reusing the same key with a different payload must produce a conflict.

**Concurrency:** Concurrent spending from one wallet must not cause overspending. MSSQL is the final authority.

**Security:** Public traffic passes through Gateway JWT validation, while service-level JWT/ownership is validated again. Provider routes use separate internal and downstream service credentials.

**Availability:** Provider timeout or 5xx responses must not corrupt financial correctness. Mandatory decisions such as fraud evaluation fail closed.

**Auditability:** Financial movements must be explainable through ledger entries and immutable business-transaction records.

### 4. Data authorities
- **MSSQL:** durable source of truth for customer, authentication/session, wallet, bank account, transaction, idempotency and ledger state.
- **Redis:** transient state such as OTP challenges; never the authority for money correctness.
- **Fake providers:** simulate external systems and cannot directly mutate FinWallet tables.

### 5. Runtime boundaries
```text
Client -> YARP Gateway -> FinWallet.Api
FinWallet.Api -> YARP Gateway -> Fake providers
```

The Gateway owns routing, edge security and traffic controls. Business authorization and financial rules remain in the application/domain layers.

### 6. Performance approach
- SqlClient connection pooling is used.
- Redis `ConnectionMultiplexer` is singleton.
- HttpClientFactory + `SocketsHttpHandler` pooling is used.
- YARP destination connections, timeouts and load balancing are configuration-driven.
- SQL isolation/locking required for financial correctness is not weakened merely for benchmark throughput.

### 7. Testing approach
Moq is used only for Application orchestration boundaries. Real MSSQL locking, Redis Lua and YARP routing are not considered proven by mocks; they require integration/concurrency tests.

### 8. Most important current gap
A new wallet starts with zero balance and no public BankDeposit endpoint currently exists. Therefore a fully public-API-only `register -> fund -> transfer` path is not yet possible. This is the most important next functional gap in the project.
