# Son Teknik İnceleme / Final Technical Review

## Türkçe

### İnceleme sonucu
FinWallet v1 için planlanan uygulama seviyesindeki ana finansal ve operasyonel boşluklar kapatılmıştır. Proje artık yalnız wallet-transfer demonstrasyonu değildir; public funding/withdrawal, purchase/campaign, refund/reversal, durable fraud review, Outbox/Inbox, transaction history ve reconciliation akışları vardır.

### Tamamlanan kritik alanlar
- MSSQL finansal source of truth; Redis transient support state.
- Customer registration, OTP, JWT, refresh rotation, logout ve durable session.
- Multi-currency wallet ve external BankAccount.
- Bank -> Wallet deposit.
- Wallet -> Bank withdrawal + cutoff scheduling + blocked-fund lifecycle.
- Wallet-to-wallet transfer.
- Merchant purchase + campaign sponsor accounting.
- Purchase refund ve internal wallet-transfer reversal.
- Internal + external fraud ve durable FraudEvents/manual review.
- Append-only double-entry ledger.
- Durable idempotency ve MSSQL concurrency/locking.
- Transactional Outbox dispatcher ve Inbox callback dedupe.
- Customer transaction-history keyset read model.
- Wallet/Ledger, BankSettlement/Ledger ve FinWallet/FakeBank reconciliation.
- YARP Gateway auth, internal-service routing, rate limit, health, load balancing ve request limits.
- Tüm Web API'lerde Swagger; production overlay'de kapalı.
- Dockerized MSSQL/Redis/provider/gateway stack.
- Unit tests + Docker stack smoke/schema/Redis kontrolleri.
- Direct/transitive NuGet vulnerability audit ve SBOM artifact CI.
- Production Compose exposure policy: yalnız Gateway host port publish edebilir; privileged/host-network/cap-add yasaktır.

### Finansal correctness değerlendirmesi
Aşağıdaki kurallar implementation'ın merkezindedir:
1. External HTTP açık financial SQL transaction içinde yapılmaz.
2. Completed money movement balanced ledger journal üretir.
3. Wallet balance projection'dır; ledger/history ayrı tutulur.
4. Withdrawal provider tamamlanana kadar fon bloklanabilir; terminal failure'da blok serbest bırakılır.
5. Duplicate command durable idempotency ile tek financial effect üretir.
6. Callback duplicate'ları Inbox ile dedupe edilir; terminal movement finalization replay-safe'dir.
7. Correction geçmiş kaydı overwrite etmez; refund/reversal opposite journal üretir.
8. Reconciliation fark gördüğünde otomatik balance overwrite yapmaz; issue oluşturur.

### Fraud değerlendirmesi
Transfer ve Purchase fraud sinyalleri client trust flag'lerinden alınmaz. Session, country, device reference, velocity, 24h amount ve beneficiary/merchant familiarity server-side state'ten türetilir. Internal ve external kararlar birleşir. `Review` kararı durable FraudEvent olarak kalır; internal approve sonrası aynı idempotency key ile işlem devam edebilir, deny sonrası para hareketi yapılmaz.

### Reliability değerlendirmesi
- Outbox: financial commit ile aynı MSSQL transaction içinde notification intent yazılır; provider outage finansal commit'i geri almaz.
- Inbox: callback Source+MessageId+payload hash ile durable dedupe edilir.
- Bank background processor: scheduled/pending banka hareketlerini tekrar işler.
- Retryable provider failure pending kalabilir; non-retryable failure terminal Failed olup blocked funds release eder.
- Redis outage finansal source-of-truth'u bozmaz; OTP gibi güvenlik akışları fail-closed olabilir.

### Security değerlendirmesi
Application seviyesinde OWASP/API abuse karşı önlemler vardır: JWT, durable sessions, BOLA/ownership, parameterized SQL, rate/body/header limits, JSON write policy, CORS allow-list, security headers, service-to-service keys, secret fail-fast, sensitive logging yasakları ve provider URL'lerinin server-owned config olması.

Production deployment sınırında hâlâ dış altyapı sorumlulukları vardır: TLS ingress, WAF/DDoS, NetworkPolicy/firewall, managed secret store, certificate/key rotation, image signing/digest pinning, SIEM/APM, backup restore tatbikatı ve operasyonel approval/audit süreçleri. Bunlar repo içi application code ile tamamen çözülemez.

### Test değerlendirmesi
Unit-test katmanı xUnit v3 + Moq kullanır. Mock testleri orchestration boundary'lerini doğrular; MSSQL locking, Redis ve YARP davranışının yerine geçmez. CI ayrıca Docker Compose modelini doğrular, tüm service image'larını build eder, stack'i ayağa kaldırır, Gateway health, MSSQL schema ve Redis auth/persistence kontrollerini çalıştırır. Security CI direct+transitive NuGet vulnerability audit ve production exposure policy uygular.

### Dependency ve supply-chain
- NuGet sürümleri merkezi yönetilir.
- Release build warnings-as-errors çalışır.
- CI direct+transitive vulnerability kontrolü yapar.
- Dependency graph'tan CycloneDX-compatible SBOM artifact üretilir.
- Runtime'a yalnız SBOM için yeni package eklenmez.

### Bilinçli olarak kapsam dışında kalanlar
- gerçek banka/SMS/fraud provider credential ve SLA'ları;
- kart/acquiring;
- kredi/loan/credit scoring;
- crypto/stocks;
- FX engine;
- Kafka/RabbitMQ;
- core microservice split;
- Event Sourcing;
- production Kubernetes/OpenShift manifest/NetworkPolicy/Ingress/WAF platform yönetimi.

### Son karar
**FinWallet v1 side-project scope'u tamamlanmıştır.** Kod tarafında bilinen ana functional boşluk kalmamıştır. Proje finansal backend mimarisi, correctness, concurrency, fraud, gateway, reliability ve reconciliation prensiplerini gösterecek seviyededir.

Bu ifade “regüle banka production platformudur” anlamına gelmez. Gerçek production için dış deployment/security/operations kontrolleri ve bağımsız penetration/performance/DR testleri ayrıca gerekir.

---

## English

### Review outcome
The major application-level financial and operational gaps planned for FinWallet v1 are now closed. The project is no longer only a wallet-transfer demonstration; it includes public funding/withdrawal, purchase/campaign, refund/reversal, durable fraud review, Outbox/Inbox, transaction history and reconciliation flows.

### Completed critical areas
- MSSQL as financial source of truth; Redis as transient support state.
- Customer registration, OTP, JWT, refresh rotation, logout and durable sessions.
- Multi-currency wallets and external BankAccounts.
- Bank -> Wallet deposit.
- Wallet -> Bank withdrawal with cutoff scheduling and blocked-fund lifecycle.
- Wallet-to-wallet transfer.
- Merchant purchase plus campaign-sponsor accounting.
- Purchase refund and internal wallet-transfer reversal.
- Internal + external fraud with durable FraudEvents/manual review.
- Append-only double-entry ledger.
- Durable idempotency and MSSQL concurrency/locking.
- Transactional Outbox dispatcher and Inbox callback deduplication.
- Customer transaction-history keyset read model.
- Wallet/Ledger, BankSettlement/Ledger and FinWallet/FakeBank reconciliation.
- YARP Gateway authentication, internal-service routing, rate limiting, health, load balancing and request limits.
- Swagger on all Web APIs, disabled by production overlay.
- Dockerized MSSQL/Redis/provider/gateway stack.
- Unit tests plus Docker stack smoke/schema/Redis checks.
- Direct/transitive NuGet vulnerability audit and SBOM artifact CI.
- Production Compose exposure policy: only Gateway may publish a host port; privileged/host-network/cap-add are forbidden.

### Financial-correctness assessment
The implementation centers on these rules:
1. External HTTP never runs inside an open financial SQL transaction.
2. Completed money movements create balanced ledger journals.
3. Wallet balance is a projection; ledger/history remain separate.
4. Withdrawal funds may remain blocked until provider completion; terminal failure releases the reservation.
5. Duplicate commands produce a single financial effect through durable idempotency.
6. Duplicate callbacks are deduplicated by Inbox; terminal movement finalization is replay-safe.
7. Corrections never overwrite history; refund/reversal creates opposite journals.
8. Reconciliation reports mismatches instead of silently overwriting balances.

### Fraud assessment
Transfer and Purchase fraud signals are not accepted as client trust flags. Session, country, device reference, velocity, 24-hour amount and beneficiary/merchant familiarity are derived from server-side state. Internal and external decisions are combined. A `Review` result becomes a durable FraudEvent; after internal approval the same idempotency key may resume processing, while denial moves no money.

### Reliability assessment
- Outbox writes notification intent in the same MSSQL transaction as the financial commit; communication outage never rolls money back.
- Inbox durably deduplicates callbacks by Source+MessageId+payload hash.
- Bank background processor advances scheduled/pending bank movements.
- Retryable provider failures may remain pending; non-retryable failures become terminal Failed and release blocked funds.
- Redis outage does not replace financial truth; security flows such as OTP may fail closed.

### Security assessment
Application-level OWASP/API-abuse controls include JWT, durable sessions, BOLA/ownership checks, parameterized SQL, rate/body/header limits, JSON write policy, CORS allow-list, security headers, service-to-service keys, secret fail-fast behavior, sensitive-log prohibitions and server-owned provider URLs.

Real production still requires external platform controls: TLS ingress, WAF/DDoS protection, NetworkPolicy/firewall, managed secret store, certificate/key rotation, image signing/digest pinning, SIEM/APM, backup/restore exercises and operational approval/audit processes. These cannot be fully solved by repository application code alone.

### Testing assessment
The unit-test layer uses xUnit v3 + Moq. Mock tests validate orchestration boundaries but do not replace MSSQL locking, Redis or YARP behavior. CI also validates Docker Compose models, builds all service images, starts the stack, and checks Gateway health, MSSQL schema, Redis authentication/persistence. Security CI performs direct+transitive NuGet vulnerability auditing and production-exposure policy checks.

### Dependency and supply chain
- NuGet versions are centrally managed.
- Release build runs with warnings-as-errors.
- CI audits direct and transitive vulnerabilities.
- A CycloneDX-compatible SBOM artifact is generated from the dependency graph.
- No runtime package is added solely for SBOM generation.

### Intentionally out of scope
- real bank/SMS/fraud provider credentials and SLAs;
- cards/acquiring;
- lending/credit scoring;
- crypto/stocks;
- FX engine;
- Kafka/RabbitMQ;
- core microservice split;
- Event Sourcing;
- production Kubernetes/OpenShift manifest/NetworkPolicy/Ingress/WAF platform administration.

### Final decision
**FinWallet v1 is complete for its side-project scope.** No known major functional application gap remains. The repository is at a level suitable for demonstrating financial-backend architecture, correctness, concurrency, fraud, gateway, reliability and reconciliation principles.

This does not mean it is a regulated bank production platform. Real production still requires external deployment/security/operations controls plus independent penetration, performance and disaster-recovery testing.
