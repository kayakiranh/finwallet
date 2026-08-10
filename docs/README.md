# FinWallet Dokümantasyon İndeksi / Documentation Index

> Tüm proje dokümanları Türkçe ve İngilizce tutulur. / All project documentation is maintained in Turkish and English.

## Türkçe

### Dokümanlar
1. [00 - Master Specification / Ana Spesifikasyon](00-master-specification.md)
2. [01 - Technical Analysis / Teknik Analiz](01-technical-analysis.md)
3. [02 - Architecture / Mimari](02-architecture.md)
4. [03 - Design Patterns / Tasarım Desenleri](03-design-patterns.md)
5. [04 - API Guide / API Rehberi](04-api-guide.md)
6. [05 - External Integrations / Harici Entegrasyonlar](05-external-integrations.md)
7. [06 - Technologies and Packages / Teknolojiler ve Paketler](06-technologies-and-packages.md)
8. [07 - Database / Veritabanı](07-database.md)
9. [08 - Financial Flows / Finansal Akışlar](08-financial-flows.md)
10. [09 - Security / Güvenlik](09-security.md)
11. [10 - Testing / Test Stratejisi](10-testing.md)
12. [11 - Code Documentation Standard / Kod Dokümantasyon Standardı](11-code-documentation-standard.md)
13. [12 - Agent and Codex Workflow / Agent ve Codex Akışı](12-agent-codex-workflow.md)
14. [13 - Project Management / Proje Yönetimi](13-project-management.md)
15. [14 - Wallet Transfer](14-wallet-transfer.md)
16. [15 - Gateway, Swagger and Platform Security](15-gateway-platform-security.md)
17. [16 - First Run Happy Path / İlk Çalıştırma Happy Path](16-happy-path-onboarding.md)
18. [17 - AI-Assisted Architecture Decisions / AI Destekli Mimari Kararlar](17-ai-architecture-decisions.md)
19. [18 - Performance Review / Performans İncelemesi](18-performance-review.md)
20. [19 - Final Technical Review / Son Teknik İnceleme](19-final-technical-review.md)
21. [20 - Docker Runbook / Docker Çalıştırma Rehberi](20-docker-runbook.md)
22. [21 - End-to-End Happy Path / Uçtan Uca Başarılı Akış](21-happy-path.md)
23. [22 - Fraud Path / Fraud Akışı](22-fraud-path.md)
24. [23 - Fail Path / Hata ve Recovery Akışı](23-fail-path.md)
25. [24 - Error Codes / Hata Kodları](24-error-codes.md)
26. [25 - System Topology / Sistem Topolojisi](25-topology.md)
27. [ADR Index / ADR İndeksi](adr/README.md)

### Yeni geliştirici için okuma sırası
1. `00-master-specification.md` — proje scope ve invariant'lar.
2. `01-technical-analysis.md` — functional/non-functional analiz.
3. `02-architecture.md` — katmanlar, runtime topology ve trust boundary'ler.
4. `25-topology.md` — servis, veri, güvenlik, provider, worker ve Docker topolojisinin tek görünümü.
5. `17-ai-architecture-decisions.md` — neden bu mimarinin seçildiği.
6. `04-api-guide.md` — endpoint ve HTTP sözleşmeleri.
7. `21-happy-path.md` — register, OTP, login, wallet, bank account ve BankDeposit'in gerçek sıralı akışı.
8. `22-fraud-path.md` — Allow/Review/Deny/fail-closed fraud dalları.
9. `23-fail-path.md` — auth, bank, fraud, idempotency ve notification failure/recovery davranışları.
10. `24-error-codes.md` — Gateway, FinWallet, provider ve background machine code kataloğu.
11. `07-database.md` ve `08-financial-flows.md` — persistence ve accounting.
12. `09-security.md` ve `15-gateway-platform-security.md` — güvenlik modeli.
13. `10-testing.md` — mock ve gerçek altyapı test ayrımı.
14. `20-docker-runbook.md` — tüm servisleri Docker ile build/start/stop/debug etme, volume ve backup yönetimi.
15. `18-performance-review.md` ve `19-final-technical-review.md` — tuning ve final teknik durum.

### Güncel runtime topolojisi
```text
Client -> FinWallet.Gateway -> FinWallet.Api
FinWallet.Api -> FinWallet.Gateway /providers/* -> Fake provider APIs
FinWallet.Api -> MSSQL / Redis
```

Gateway edge auth/routing/traffic control sağlar. FinWallet.Api ve provider servisleri kendi güvenlik kontrollerini korur. MSSQL financial source of truth'tür; Redis transient support state içindir.

### Dokümantasyon Definition of Done
Bir feature tamamlanmış sayılmadan önce etkilenen API, database, security, integration, package, configuration, test ve architecture belgeleri güncellenmelidir. Kod davranışı değiştiğinde dokümanın eski davranışı anlatmaya devam etmesi defect kabul edilir.

---

## English

### Documents
1. [00 - Master Specification](00-master-specification.md)
2. [01 - Technical Analysis](01-technical-analysis.md)
3. [02 - Architecture](02-architecture.md)
4. [03 - Design Patterns](03-design-patterns.md)
5. [04 - API Guide](04-api-guide.md)
6. [05 - External Integrations](05-external-integrations.md)
7. [06 - Technologies and Packages](06-technologies-and-packages.md)
8. [07 - Database](07-database.md)
9. [08 - Financial Flows](08-financial-flows.md)
10. [09 - Security](09-security.md)
11. [10 - Testing](10-testing.md)
12. [11 - Code Documentation Standard](11-code-documentation-standard.md)
13. [12 - Agent and Codex Workflow](12-agent-codex-workflow.md)
14. [13 - Project Management](13-project-management.md)
15. [14 - Wallet Transfer](14-wallet-transfer.md)
16. [15 - Gateway, Swagger and Platform Security](15-gateway-platform-security.md)
17. [16 - First Run Happy Path](16-happy-path-onboarding.md)
18. [17 - AI-Assisted Architecture Decisions](17-ai-architecture-decisions.md)
19. [18 - Performance Review](18-performance-review.md)
20. [19 - Final Technical Review](19-final-technical-review.md)
21. [20 - Docker Runbook](20-docker-runbook.md)
22. [21 - End-to-End Happy Path](21-happy-path.md)
23. [22 - Fraud Path](22-fraud-path.md)
24. [23 - Fail Path](23-fail-path.md)
25. [24 - Error Codes](24-error-codes.md)
26. [25 - System Topology](25-topology.md)
27. [ADR Index](adr/README.md)

### Recommended reading order for a new engineer
1. `00-master-specification.md` — project scope and invariants.
2. `01-technical-analysis.md` — functional and non-functional analysis.
3. `02-architecture.md` — layers, runtime topology and trust boundaries.
4. `25-topology.md` — one combined view of service, data, security, provider, worker and Docker topology.
5. `17-ai-architecture-decisions.md` — why the architecture evolved this way.
6. `04-api-guide.md` — endpoint and HTTP contracts.
7. `21-happy-path.md` — actual ordered register, OTP, login, wallet, bank-account and BankDeposit flow.
8. `22-fraud-path.md` — Allow/Review/Deny/fail-closed fraud branches.
9. `23-fail-path.md` — auth, bank, fraud, idempotency and notification failure/recovery behavior.
10. `24-error-codes.md` — Gateway, FinWallet, provider and background machine-code catalog.
11. `07-database.md` and `08-financial-flows.md` — persistence and accounting.
12. `09-security.md` and `15-gateway-platform-security.md` — security model.
13. `10-testing.md` — mocks versus real-infrastructure tests.
14. `20-docker-runbook.md` — build/start/stop/debug the complete Docker stack and manage volumes/backups.
15. `18-performance-review.md` and `19-final-technical-review.md` — tuning and final technical status.

### Current runtime topology
```text
Client -> FinWallet.Gateway -> FinWallet.Api
FinWallet.Api -> FinWallet.Gateway /providers/* -> Fake provider APIs
FinWallet.Api -> MSSQL / Redis
```

The Gateway owns edge authentication, routing and traffic control. FinWallet.Api and provider services retain their own security checks. MSSQL is the financial source of truth; Redis is transient support state.

### Documentation Definition of Done
Before a feature is considered complete, affected API, database, security, integration, package, configuration, testing and architecture documents must be updated. If code behavior changes while documentation continues to describe the old behavior, that is treated as a defect.
