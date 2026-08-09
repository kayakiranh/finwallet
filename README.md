# FinWallet

> Bu README Türkçe ve İngilizce hazırlanmıştır. / This README is maintained in Turkish and English.

## Türkçe

### Proje nedir?
FinWallet; .NET 8, MSSQL, Redis, JWT ve YARP kullanılarak geliştirilen finansal backend side projectidir. Amaç yalnız CRUD yazmak değil; gerçek finansal sistemlerde önemli olan para bütünlüğü, çift taraflı muhasebe, idempotency, concurrency, fraud kontrolü, harici banka entegrasyonu, gateway güvenliği ve reconciliation problemlerini uygulamalı olarak göstermektir.

### Güncel mimari

```text
Client
  -> FinWallet.Gateway (YARP :8080)
      -> FinWallet.Api (:8081)
          -> Gateway /providers/*
              -> FakeBank.Api (:8082)
              -> FakeFraud.Api (:8083)
              -> FakeCutoff.Api (:8084)
              -> FakeCampaign.Api (:8085)
              -> FakeCommunication.Api (:8086)
```

Normal client ve servisler arası HTTP trafiği Gateway üzerinden geçer. Public `/api/*` rotalarında Gateway JWT kontrolü yapar; FinWallet.Api kendi JWT/ownership kontrollerini tekrar uygular. Provider rotaları internal-service credential ile korunur; destination servisler ayrıca downstream credential ister.

### Uygulanmış ana özellikler
- Kayıt, OTP doğrulama, login, JWT, refresh-token rotation ve server-side session.
- TRY/USD/EUR wallet oluşturma ve listeleme.
- FakeBank üzerinden external bank account açma.
- Double-entry ledger ve atomik wallet-to-wallet transfer.
- Durable MSSQL idempotency ve concurrency koruması.
- Internal + FakeFraud değerlendirmesi.
- YARP Gateway, load balancing/health/rate-limit/request limitleri.
- Tüm Web API projelerinde Swagger; production'da varsayılan kapalı.
- MSSQL/Redis/HttpClient/YARP tuning ayarlarının appsettings tabanlı yönetimi.
- xUnit v3 + Moq unit test altyapısı ve CI'da Release build + test.

### Bilinen eksikler / sonraki işler
Public BankDeposit ve BankWithdrawal akışları henüz tamamlanmadığı için yeni oluşturulan wallet public API üzerinden fonlanamıyor. Outbox/Inbox, durable FraudEvents/manual review, transaction-history read model, reconciliation, merkezi maskeli structured logging/telemetry ve gerçek MSSQL/Redis/YARP integration-concurrency testleri de sonraki fazlardır.

### Dokümantasyon
Başlangıç noktası: [`docs/README.md`](docs/README.md).

Önerilen sıra:
1. `docs/00-master-specification.md`
2. `docs/01-technical-analysis.md`
3. `docs/02-architecture.md`
4. `docs/16-happy-path-onboarding.md`
5. `docs/19-final-technical-review.md`

### Build ve test

```bash
dotnet restore FinWallet.sln
dotnet build FinWallet.sln --configuration Release --no-restore --warnaserror
dotnet test FinWallet.sln --configuration Release --no-build
```

---

## English

### What is the project?
FinWallet is a financial-backend side project built with .NET 8, MSSQL, Redis, JWT and YARP. Its purpose is not merely to demonstrate CRUD; it is intended to exercise real financial-system concerns such as money correctness, double-entry accounting, idempotency, concurrency, fraud controls, external-bank integration, gateway security and reconciliation.

### Current architecture

```text
Client
  -> FinWallet.Gateway (YARP :8080)
      -> FinWallet.Api (:8081)
          -> Gateway /providers/*
              -> FakeBank.Api (:8082)
              -> FakeFraud.Api (:8083)
              -> FakeCutoff.Api (:8084)
              -> FakeCampaign.Api (:8085)
              -> FakeCommunication.Api (:8086)
```

Normal client and service-to-service HTTP traffic passes through the Gateway. The Gateway validates JWTs for protected public `/api/*` routes; FinWallet.Api independently repeats JWT and ownership checks. Provider routes require an internal-service credential, while destination services require a separate downstream credential.

### Implemented major capabilities
- Registration, OTP verification, login, JWT, refresh-token rotation and server-side sessions.
- TRY/USD/EUR wallet creation and listing.
- External bank-account opening through FakeBank.
- Double-entry ledger and atomic wallet-to-wallet transfer.
- Durable MSSQL idempotency and concurrency protection.
- Internal plus FakeFraud evaluation.
- YARP Gateway with load balancing, health checks, rate limiting and request limits.
- Swagger on all Web APIs, disabled by default in production.
- Appsettings-driven MSSQL/Redis/HttpClient/YARP tuning.
- xUnit v3 + Moq unit-test infrastructure and Release build + test in CI.

### Known gaps / next work
Public BankDeposit and BankWithdrawal flows are not complete, so a newly created wallet cannot yet be funded entirely through the public API. Outbox/Inbox, durable FraudEvents/manual review, a transaction-history read model, reconciliation, centralized masked structured logging/telemetry and real MSSQL/Redis/YARP integration-concurrency tests are also future phases.

### Documentation
Start at [`docs/README.md`](docs/README.md).

Recommended order:
1. `docs/00-master-specification.md`
2. `docs/01-technical-analysis.md`
3. `docs/02-architecture.md`
4. `docs/16-happy-path-onboarding.md`
5. `docs/19-final-technical-review.md`

### Build and test

```bash
dotnet restore FinWallet.sln
dotnet build FinWallet.sln --configuration Release --no-restore --warnaserror
dotnet test FinWallet.sln --configuration Release --no-build
```
