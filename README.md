# FinWallet

> Bu README Türkçe ve İngilizce hazırlanmıştır. / This README is maintained in Turkish and English.

## Türkçe

### Proje nedir?
FinWallet; .NET 8, MSSQL, Redis, JWT ve YARP ile geliştirilmiş finansal backend side projectidir. Amaç yalnız CRUD değil; gerçek finansal sistemlerde kritik olan para bütünlüğü, double-entry ledger, idempotency, concurrency, internal/external fraud, bank integration, campaign accounting, cutoff scheduling, Outbox/Inbox, reconciliation ve gateway security problemlerini uygulamalı olarak göstermektir.

### Güncel mimari
```text
Client
  -> FinWallet.Gateway (YARP :8080)
      -> FinWallet.Api
          -> Gateway /providers/*
              -> FakeBank.Api
              -> FakeFraud.Api
              -> FakeCutoff.Api
              -> FakeCampaign.Api
              -> FakeCommunication.Api

FinWallet.Api -> MSSQL (financial/durable truth)
FinWallet.Api -> Redis (OTP/transient support state)
```

Normal client ve servisler arası HTTP trafiği Gateway üzerinden geçer. Public `/api/*` rotalarında Gateway JWT kontrolü yapar; FinWallet.Api JWT, session ve ownership kontrollerini tekrar uygular. Internal callback/review/reconciliation rotaları `InternalService` policy ile ayrılır. Provider rotaları internal-service credential ister; destination servisler ayrıca downstream credential doğrular.

### Tamamlanan ana özellikler
- Customer registration, Redis OTP, login, JWT, refresh-token rotation, logout ve durable session.
- TRY/USD/EUR wallet oluşturma/listeleme.
- FakeBank external bank account açma.
- Public Bank -> Wallet deposit ve Wallet -> Bank withdrawal.
- FakeCutoff ile scheduled withdrawal/business date/settlement date.
- Wallet-to-wallet transfer.
- Merchant purchase + FakeCampaign discount/sponsor accounting.
- Purchase refund ve güvenli internal wallet-transfer reversal.
- Internal + FakeFraud değerlendirmesi ve durable manual fraud review.
- Double-entry append-only ledger; corrections ters journal ile yapılır.
- Durable MSSQL idempotency, concurrency/locking ve blocked-fund lifecycle.
- Transactional Outbox worker ve idempotent Inbox bank callback dedupe.
- Customer transaction-history read model ve keyset pagination.
- Wallet/Ledger, Bank-Settlement/Ledger ve FinWallet/FakeBank reconciliation.
- YARP Gateway, rate limits, health checks, request limits ve internal trust boundaries.
- Tüm Web API projelerinde Swagger; production overlay'de kapalı.
- MSSQL/Redis/HttpClient/YARP tuning ayarları configuration üzerinden yönetilir.
- xUnit v3 + Moq unit test altyapısı.
- Docker stack smoke validation, schema/Redis checks ve security/supply-chain CI.
- NuGet direct+transitive vulnerability audit ve CycloneDX-compatible SBOM artifact.

### Finansal invariant'lar
- MSSQL financial source of truth'tür; Redis para doğruluğu için yeterli değildir.
- External HTTP açık financial SQL transaction içinde çalışmaz.
- Completed para hareketleri balanced journal üretir: `SUM(Debit) = SUM(Credit)`.
- Aynı idempotency key + aynı payload aynı sonucu replay eder.
- Aynı key + farklı payload conflict üretir.
- Ledger geçmişi silinmez/değiştirilmez; refund/reversal/compensation yeni kayıt üretir.
- Reconciliation mismatch gördüğünde bakiyeyi sessizce düzeltmez; issue üretir.

### Production sınırı
Bu repo production-benzeri hardening gösterir fakat gerçek banka production deployment'ı değildir. Gerçek ortamda ayrıca TLS ingress, WAF/DDoS, secret manager, managed database/Redis, backup/restore, network policy, SIEM/APM, key rotation, image signing/digest pinning ve operasyonel onay süreçleri gerekir.

### Dokümantasyon
Başlangıç: [`docs/README.md`](docs/README.md).

Önerilen sıra:
1. `docs/00-master-specification.md`
2. `docs/01-technical-analysis.md`
3. `docs/02-architecture.md`
4. `docs/04-api-guide.md`
5. `docs/16-happy-path-onboarding.md`
6. `docs/19-final-technical-review.md`
7. `docs/20-docker-runbook.md`

### Build ve test
```bash
dotnet restore FinWallet.sln
dotnet build FinWallet.sln --configuration Release --no-restore --warnaserror
dotnet test FinWallet.sln --configuration Release --no-build
```

Docker:
```bash
cp .env.example .env
docker compose --env-file .env -f compose.yml up -d --build
```

---

## English

### What is the project?
FinWallet is a financial-backend side project built with .NET 8, MSSQL, Redis, JWT and YARP. It goes beyond CRUD and exercises real financial-system concerns: money correctness, double-entry ledger, idempotency, concurrency, internal/external fraud, bank integration, campaign accounting, cutoff scheduling, Outbox/Inbox, reconciliation and gateway security.

### Current architecture
```text
Client
  -> FinWallet.Gateway (YARP :8080)
      -> FinWallet.Api
          -> Gateway /providers/*
              -> FakeBank.Api
              -> FakeFraud.Api
              -> FakeCutoff.Api
              -> FakeCampaign.Api
              -> FakeCommunication.Api

FinWallet.Api -> MSSQL (financial/durable truth)
FinWallet.Api -> Redis (OTP/transient support state)
```

Normal client and service-to-service HTTP traffic passes through the Gateway. The Gateway validates JWTs for public `/api/*` routes; FinWallet.Api independently repeats JWT, session and ownership checks. Internal callback/review/reconciliation routes are isolated by the `InternalService` policy. Provider routes require an internal-service credential and destination services additionally validate a downstream credential.

### Completed major capabilities
- Customer registration, Redis OTP, login, JWT, refresh-token rotation, logout and durable sessions.
- TRY/USD/EUR wallet creation/listing.
- External bank-account opening through FakeBank.
- Public Bank -> Wallet deposit and Wallet -> Bank withdrawal.
- Scheduled withdrawal/business/settlement dates through FakeCutoff.
- Wallet-to-wallet transfer.
- Merchant purchase plus FakeCampaign discount/sponsor accounting.
- Purchase refund and safe internal wallet-transfer reversal.
- Internal + FakeFraud evaluation with durable manual fraud review.
- Double-entry append-only ledger; corrections use opposite journals.
- Durable MSSQL idempotency, concurrency/locking and blocked-fund lifecycle.
- Transactional Outbox worker and idempotent Inbox bank-callback deduplication.
- Customer transaction-history read model with keyset pagination.
- Wallet/Ledger, Bank-Settlement/Ledger and FinWallet/FakeBank reconciliation.
- YARP Gateway, rate limits, health checks, request limits and internal trust boundaries.
- Swagger on every Web API, disabled by the production overlay.
- Configuration-driven MSSQL/Redis/HttpClient/YARP tuning.
- xUnit v3 + Moq unit-test infrastructure.
- Docker stack smoke validation, schema/Redis checks and security/supply-chain CI.
- Direct+transitive NuGet vulnerability audit and a CycloneDX-compatible SBOM artifact.

### Financial invariants
- MSSQL is the financial source of truth; Redis is never sufficient for money correctness.
- External HTTP never runs inside an open financial SQL transaction.
- Completed money movements create balanced journals: `SUM(Debit) = SUM(Credit)`.
- Same idempotency key + same payload replays the same result.
- Same key + different payload conflicts.
- Ledger history is never deleted/rewritten; refund/reversal/compensation creates new records.
- Reconciliation reports mismatches and never silently repairs balances.

### Production boundary
The repository demonstrates production-like hardening but is not a real-bank production deployment. A real environment still needs TLS ingress, WAF/DDoS controls, a secret manager, managed database/Redis, backup/restore, network policies, SIEM/APM, key rotation, image signing/digest pinning and operational approval processes.

### Documentation
Start at [`docs/README.md`](docs/README.md).

Recommended order:
1. `docs/00-master-specification.md`
2. `docs/01-technical-analysis.md`
3. `docs/02-architecture.md`
4. `docs/04-api-guide.md`
5. `docs/16-happy-path-onboarding.md`
6. `docs/19-final-technical-review.md`
7. `docs/20-docker-runbook.md`

### Build and test
```bash
dotnet restore FinWallet.sln
dotnet build FinWallet.sln --configuration Release --no-restore --warnaserror
dotnet test FinWallet.sln --configuration Release --no-build
```

Docker:
```bash
cp .env.example .env
docker compose --env-file .env -f compose.yml up -d --build
```
