# Docker Çalıştırma Rehberi / Docker Runbook

## Türkçe

### Amaç
Bu belge FinWallet çözümündeki tüm servisleri Docker Compose ile ilk kez ayağa kaldırmak, durumlarını kontrol etmek, logları incelemek, veritabanı/Redis volume'larını yönetmek, tek servis rebuild etmek ve güvenli şekilde durdurmak için operasyon rehberidir.

Normal Docker topolojisi:

```text
Host
  |
  | http://localhost:8080
  v
FinWallet.Gateway
  |
  +--> FinWallet.Api
  |
  +--> FakeBank.Api
  +--> FakeFraud.Api
  +--> FakeCutoff.Api
  +--> FakeCampaign.Api
  +--> FakeCommunication.Api

FinWallet.Api
  |
  +--> MSSQL
  +--> Redis
  +--> Gateway /providers/*
```

Normal kullanımda host'a yalnız Gateway portu publish edilir. MSSQL, Redis, FinWallet.Api ve fake provider portları Docker network içinde kalır. `compose.debug.yml` yalnız local debugging amacıyla bu servisleri `127.0.0.1` üzerinden host'a açar.

### Docker dosyaları

| Dosya | Amaç |
|---|---|
| `compose.yml` | Tüm uygulama ve infrastructure servislerinin ana Compose tanımı. |
| `compose.debug.yml` | MSSQL, Redis, FinWallet.Api ve fake provider portlarını yalnız localhost'a açan debug overlay. |
| `compose.production.yml` | Production-like environment için Swagger kapatma ve restart policy gibi ek hardening override'ları. |
| `docker/Dockerfile.webapi` | Gateway, FinWallet.Api ve tüm fake Web API projeleri için ortak multi-stage .NET 8 image build dosyası. |
| `docker/mssql/init-db.sh` | MSSQL ayağa kalktıktan sonra `001`, `002`, `003` schema scriptlerini sıralı ve version kontrollü çalıştırır. |
| `.env.example` | Local Compose environment değişkenleri için şablon. Gerçek secret içermez. |
| `.dockerignore` | Docker build context'inden bin/obj/log/test/docs/local-secret gibi gereksiz dosyaları çıkarır. |
| `.gitignore` | bin/obj/Debug/Release/log/secret/local Docker state gibi Git'e girmemesi gereken dosyaları engeller. |

### Docker servisleri

| Service | Görev | Normal host portu |
|---|---|---|
| `gateway` | YARP edge gateway, JWT/routing/rate limit/load balancing | `8080` |
| `finwallet-api` | Ana FinWallet Web API | publish edilmez |
| `fake-bank` | Bank simulator | publish edilmez |
| `fake-fraud` | Fraud simulator | publish edilmez |
| `fake-cutoff` | Cutoff/business-calendar simulator | publish edilmez |
| `fake-campaign` | Campaign simulator | publish edilmez |
| `fake-communication` | SMS/Email simulator | publish edilmez |
| `mssql` | Financial source of truth | publish edilmez |
| `mssql-init` | One-shot schema initialization container | port yok |
| `redis` | OTP/transient support state | publish edilmez |

### Persistent volume'lar

| Volume | İçerik | `docker compose down` sonrası kalır mı? |
|---|---|---|
| `finwallet_mssql_data` | SQL Server data/log dosyaları | Evet |
| `finwallet_mssql_backup` | SQL backup alanı | Evet |
| `finwallet_redis_data` | Redis AOF/RDB persistence | Evet |

Uygulama container'ları stateless'tir. Uygulama logları named volume'a yazılmaz; container stdout/stderr üzerinden Docker log driver'a gider ve log rotation uygulanır.

### 1. Ön koşulları kontrol et

```bash
docker --version
docker compose version
```

Ne yapar:
- Docker Engine/Desktop CLI'nin kullanılabilir olduğunu doğrular.
- Compose v2 plugin'in kurulu olduğunu doğrular.
- Komut hata veriyorsa projeyi çalıştırmadan önce Docker Desktop/Engine problemi çözülmelidir.

Windows Docker Desktop kullanıyorsan Linux containers modunda çalıştır.

### 2. Repository root dizinine geç

```bash
cd finwallet
```

Komutların tamamı `FinWallet.sln`, `compose.yml` ve `.env.example` dosyalarının bulunduğu repository root dizininden çalıştırılmalıdır.

### 3. Local environment dosyasını oluştur

PowerShell:

```powershell
Copy-Item .env.example .env
```

Bash:

```bash
cp .env.example .env
```

Ne yapar:
- Version control altında bulunan güvenli şablondan local `.env` üretir.
- `.env` `.gitignore` içindedir; commit edilmez.

`.env` içinde en az şu değerleri güçlü local değerlerle değiştir:

```text
MSSQL_SA_PASSWORD
REDIS_PASSWORD
JWT_SIGNING_KEY
REGISTRATION_OTP_PEPPER
INTERNAL_SERVICE_KEY
DOWNSTREAM_SERVICE_KEY
```

Production secret'larını `.env` ile yönetme. Production'da orchestrator/secret-store injection kullan.

### 4. Compose dosyasını çalıştırmadan önce validate et

```bash
docker compose --env-file .env config --quiet
```

Ne yapar:
- Environment substitution yapar.
- YAML/Compose syntax'ını doğrular.
- Eksik required environment variable varsa burada fail eder.
- Container başlatmaz ve veri değiştirmez.

Resolved configuration görmek için:

```bash
docker compose --env-file .env config
```

Dikkat: resolved output secret değerleri gösterebilir. CI loguna veya paylaşılabilir dosyaya yönlendirme.

Servis listesini görmek için:

```bash
docker compose --env-file .env config --services
```

### 5. Image'ları build et

```bash
docker compose --env-file .env build --pull
```

Ne yapar:
- Gateway, FinWallet.Api ve fake API image'larını multi-stage Dockerfile ile build eder.
- `--pull`, base .NET image'larının mevcut tag için güncel halini kontrol eder.
- NuGet restore Docker BuildKit cache kullanır.
- Container başlatmaz.
- MSSQL ve Redis runtime image'ları `up` sırasında ayrıca pull edilebilir.

Cache kullanmadan tamamen temiz build gerekirse:

```bash
docker compose --env-file .env build --pull --no-cache
```

Bu daha yavaştır; normal geliştirmede sürekli kullanılmamalıdır.

### 6. Tüm sistemi ayağa kaldır

```bash
docker compose --env-file .env up -d --build
```

Ne yapar:
1. Gerekli image'ları build/pull eder.
2. `finwallet-data` ve `finwallet-backend` networklerini oluşturur.
3. MSSQL ve Redis named volume'larını oluşturur veya mevcut volume'ları tekrar bağlar.
4. MSSQL healthcheck başarılı olana kadar bekler.
5. `mssql-init` container'ı FinWallet DB'sini ve uygulanmamış schema versionlarını oluşturur.
6. Redis healthcheck başarılı olduktan sonra FinWallet.Api başlar.
7. Fake provider'lar ve Gateway başlar.
8. `-d` nedeniyle terminali bloklamadan detached modda çalışır.

İlk build/pull sonraki başlatmalardan daha uzun sürer.

### 7. Container durumlarını kontrol et

```bash
docker compose --env-file .env ps
```

Beklenen durum:
- `mssql`: Up / healthy
- `redis`: Up / healthy
- `mssql-init`: Exited (0)
- API ve Gateway servisleri: Up

`mssql-init` servisinin `Exited (0)` olması hata değildir. One-shot migration işi tamamlandığı için container kapanır.

Tüm container'ları Docker seviyesinde görmek için:

```bash
docker ps --filter name=finwallet
```

### 8. Gateway health endpoint'ini kontrol et

```bash
curl http://localhost:8080/health/live
```

Windows PowerShell:

```powershell
Invoke-RestMethod http://localhost:8080/health/live
```

Gateway cevap veriyorsa host -> Gateway network path'i çalışıyor demektir.

Swagger Development/Docker environment'ta açıksa:

```text
http://localhost:8080/swagger
```

Normal client API çağrıları Gateway `http://localhost:8080` adresine yapılmalıdır.

### 9. Logları izle

Tüm stack:

```bash
docker compose --env-file .env logs -f
```

Gateway:

```bash
docker compose --env-file .env logs -f gateway
```

FinWallet API:

```bash
docker compose --env-file .env logs -f finwallet-api
```

MSSQL:

```bash
docker compose --env-file .env logs -f mssql
```

Redis:

```bash
docker compose --env-file .env logs -f redis
```

Son 200 satır:

```bash
docker compose --env-file .env logs --tail 200 gateway finwallet-api
```

`-f` yeni logları terminalde takip eder. `Ctrl+C` yalnız log takibini bitirir; detached container'ları durdurmaz.

Docker json-file log driver için rotation tanımlıdır. Uygulama loglarını container filesystem'inde kalıcı dosya olarak tutmak yerine stdout/stderr tercih edilmiştir.

### 10. MSSQL bağlantısını doğrula

```bash
docker compose --env-file .env exec mssql bash -lc '
SQLCMD=$(command -v /opt/mssql-tools18/bin/sqlcmd || command -v /opt/mssql-tools/bin/sqlcmd) &&
$SQLCMD -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -d FinWallet -Q "SELECT DB_NAME() AS DatabaseName; SELECT Version, AppliedAt FROM dbo.SchemaVersions ORDER BY Version;"
'
```

Ne yapar:
- SQL Server container'ının içinden DB'ye bağlanır.
- `FinWallet` DB'sinin açıldığını doğrular.
- `001`, `002`, `003` migration versionlarını gösterir.

Migration'ları tekrar kontrol etmek/eksik olanı uygulamak için:

```bash
docker compose --env-file .env run --rm mssql-init
```

SchemaVersions kaydı bulunan migration tekrar çalıştırılmaz.

### 11. Redis bağlantısını doğrula

```bash
docker compose --env-file .env exec redis sh -lc 'redis-cli -a "$REDIS_PASSWORD" --no-auth-warning ping'
```

Beklenen cevap:

```text
PONG
```

Redis persistence bilgisi:

```bash
docker compose --env-file .env exec redis sh -lc 'redis-cli -a "$REDIS_PASSWORD" --no-auth-warning INFO persistence'
```

### 12. Debug portlarını aç

Normal compose security gereği backend servislerini host'a publish etmez. Local debugging gerektiğinde overlay kullan:

```bash
docker compose --env-file .env -f compose.yml -f compose.debug.yml up -d --build
```

Bu durumda localhost üzerinde:

```text
Gateway              8080
FinWallet.Api         8081
FakeBank.Api          8082
FakeFraud.Api         8083
FakeCutoff.Api        8084
FakeCampaign.Api      8085
FakeCommunication.Api 8086
MSSQL                 1433
Redis                 6379
```

Portlar `127.0.0.1` bind edilir; LAN'a açılmaz.

Normal integration testinde yine Gateway kullan. Backend debug portları yalnız inspection/debugging içindir ve business endpointleri downstream service-key korumasını sürdürür.

### 13. Tek servisi rebuild/restart et

Örneğin FinWallet.Api kodu değişti:

```bash
docker compose --env-file .env build finwallet-api
docker compose --env-file .env up -d --no-deps finwallet-api
```

Ne yapar:
- Yalnız `finwallet-api` image'ını yeniden build eder.
- `--no-deps` MSSQL/Redis/Gateway gibi bağımlı servisleri yeniden yaratmaz.

Gateway için:

```bash
docker compose --env-file .env build gateway
docker compose --env-file .env up -d --no-deps gateway
```

Container'ı image rebuild etmeden restart etmek için:

```bash
docker compose --env-file .env restart gateway
```

### 14. Bir servisin shell'ine gir

```bash
docker compose --env-file .env exec finwallet-api /bin/sh
```

Runtime image non-root `app` user ile çalıştığı için root yetkisi bekleme.

Container environment'ını incelemek için secret'ları ekrana dökmemeye dikkat et.

### 15. Resource kullanımını izle

```bash
docker stats
```

Ne gösterir:
- CPU
- memory
- network I/O
- block I/O
- PID sayısı

Compose içinde application container'ları için CPU, memory ve PID limitleri tanımlıdır. Bu değerler local safety baseline'dır; production capacity planning ölçüm ile yapılmalıdır.

### 16. Volume'ları kontrol et

```bash
docker volume ls --filter name=finwallet
```

Detay:

```bash
docker volume inspect finwallet_mssql_data
docker volume inspect finwallet_mssql_backup
docker volume inspect finwallet_redis_data
```

Normal uygulama restart/recreate işlemlerinde volume'lar korunur.

### 17. SQL backup al

Önce backup oluştur:

```bash
docker compose --env-file .env exec mssql bash -lc '
SQLCMD=$(command -v /opt/mssql-tools18/bin/sqlcmd || command -v /opt/mssql-tools/bin/sqlcmd) &&
$SQLCMD -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -Q "BACKUP DATABASE [FinWallet] TO DISK = N'"'"'/var/opt/mssql/backup/FinWallet.bak'"'"' WITH INIT, CHECKSUM"
'
```

Backup `mssql_backup` named volume içinde kalır.

Host'a kopyalamak istersen önce container ID al:

```bash
docker compose ps -q mssql
```

Bash örneği:

```bash
docker cp "$(docker compose ps -q mssql):/var/opt/mssql/backup/FinWallet.bak" ./FinWallet.bak
```

Backup dosyalarını Git'e commit etme.

### 18. Redis persistence snapshot iste

```bash
docker compose --env-file .env exec redis sh -lc 'redis-cli -a "$REDIS_PASSWORD" --no-auth-warning BGSAVE'
```

Bu komut Redis'e arka planda snapshot üretme talebi verir. Redis yine financial source of truth değildir; MSSQL backup'ın alternatifi değildir.

### 19. Servisleri geçici durdur ve tekrar başlat

Durdur:

```bash
docker compose --env-file .env stop
```

Başlat:

```bash
docker compose --env-file .env start
```

`stop` container'ları silmez. Volume ve container metadata korunur.

### 20. Stack'i kapat ama veriyi koru

```bash
docker compose --env-file .env down
```

Ne yapar:
- Compose container'larını siler.
- Compose networklerini siler.
- Named volume'ları silmez.
- Bir sonraki `up` MSSQL ve Redis verisini mevcut volume'lardan kullanır.

### 21. Tamamen sıfırla - DİKKAT veri siler

```bash
docker compose --env-file .env down -v --remove-orphans
```

Bu komut:
- container'ları siler;
- networkleri siler;
- `mssql_data`, `mssql_backup`, `redis_data` named volume'larını siler.

Sonuç: local DB, ledger, customer/auth data, Redis OTP state ve SQL backup volume'u kaybolur.

Yalnız gerçekten temiz local environment istediğinde kullan.

### 22. Yalnız Redis'i sıfırla

Önce stack'i durdur:

```bash
docker compose --env-file .env down
```

Sonra:

```bash
docker volume rm finwallet_redis_data
```

Tekrar:

```bash
docker compose --env-file .env up -d
```

MSSQL volume'u korunur.

### 23. Yalnız MSSQL'i sıfırla

```bash
docker compose --env-file .env down
docker volume rm finwallet_mssql_data finwallet_mssql_backup
docker compose --env-file .env up -d
```

Bu işlem financial data'yı tamamen siler. Local development dışında kullanılmamalıdır.

### 24. Kullanılmayan Docker disk alanını incele

```bash
docker system df
```

Build cache temizliği:

```bash
docker builder prune
```

Kullanılmayan image temizliği:

```bash
docker image prune
```

`docker system prune -a --volumes` çok daha agresiftir ve başka projelerin image/cache/volume'larını da etkileyebilir; rutin komut olarak kullanılmamalıdır.

### 25. Production-like overlay ile çalıştır

```bash
docker compose \
  --env-file .env \
  -f compose.yml \
  -f compose.production.yml \
  up -d --build
```

Bu overlay:
- `ASPNETCORE_ENVIRONMENT=Production` yapar;
- Swagger'ı kapatır;
- restart policy'yi sıkılaştırır;
- HSTS configuration'ını açar.

Bu dosya gerçek production platformunun yerine geçmez. Gerçek production için en az TLS ingress, external secret store, WAF/DDoS, image digest pinning, vulnerability scanning, backup/restore planı, monitoring/alerting ve network policy gerekir.

### 26. Docker Compose stack'i güncelle

Kod değişikliğinden sonra genel güncelleme:

```bash
git pull
docker compose --env-file .env build --pull
docker compose --env-file .env up -d
```

Schema scripti eklendiyse mevcut `mssql-init` scriptinin yeni migration'ı tanıdığı da güncellenmelidir. Migration'ı yalnız SQL dosyası ekleyip init listesine eklememek yeterli değildir.

### 27. Sık karşılaşılan problemler

#### Port 8080 kullanımda

```bash
docker compose --env-file .env ps
```

`.env` içinde:

```text
GATEWAY_PORT=8090
```

yapıp yeniden `up -d` çalıştırabilirsin. Yeni Gateway adresi `http://localhost:8090` olur.

#### MSSQL unhealthy

```bash
docker compose --env-file .env logs --tail 300 mssql
```

Kontrol et:
- password SQL Server complexity koşullarını sağlıyor mu;
- yeterli Docker memory var mı;
- volume bozuk/uyumsuz mu;
- container sürekli restart ediyor mu.

#### `mssql-init` failed

```bash
docker compose --env-file .env logs mssql-init
```

Schema script error'ını düzelt. `mssql-init` one-shot olduğu için tekrar çalıştır:

```bash
docker compose --env-file .env run --rm mssql-init
```

#### Redis unhealthy

```bash
docker compose --env-file .env logs redis
```

Sonra:

```bash
docker compose --env-file .env exec redis sh -lc 'redis-cli -a "$REDIS_PASSWORD" --no-auth-warning ping'
```

#### Gateway 502/503 dönüyor

```bash
docker compose --env-file .env logs --tail 200 gateway finwallet-api fake-bank fake-fraud fake-cutoff fake-campaign fake-communication
```

Kontrol et:
- downstream servis Up mı;
- service DNS adı doğru mu;
- internal/downstream key eşleşiyor mu;
- Gateway health probe servisi unhealthy işaretlemiş mi.

#### Secret eksik

Compose şu formatta fail eder:

```text
required variable ... is missing a value
```

`.env` dosyasının repository root'ta olduğundan ve required key'lerin boş olmadığından emin ol.

### 28. Minimum günlük geliştirme komut seti

İlk gün:

```bash
cp .env.example .env
docker compose --env-file .env config --quiet
docker compose --env-file .env up -d --build
docker compose --env-file .env ps
docker compose --env-file .env logs -f gateway finwallet-api
```

Gün sonunda veriyi koruyarak kapat:

```bash
docker compose --env-file .env down
```

Ertesi gün:

```bash
docker compose --env-file .env up -d
docker compose --env-file .env ps
```

Tam local reset gerektiğinde:

```bash
docker compose --env-file .env down -v --remove-orphans
docker compose --env-file .env up -d --build
```

Son iki komutun local MSSQL/Redis datasını sildiğini unutma.

---

## English

### Purpose
This document is the operational runbook for starting the entire FinWallet solution with Docker Compose for the first time, validating service health, inspecting logs, managing MSSQL/Redis volumes, rebuilding individual services, and shutting the stack down safely.

Normal Docker topology:

```text
Host
  |
  | http://localhost:8080
  v
FinWallet.Gateway
  |
  +--> FinWallet.Api
  |
  +--> FakeBank.Api
  +--> FakeFraud.Api
  +--> FakeCutoff.Api
  +--> FakeCampaign.Api
  +--> FakeCommunication.Api

FinWallet.Api
  |
  +--> MSSQL
  +--> Redis
  +--> Gateway /providers/*
```

Only the Gateway is published to the host in the normal stack. MSSQL, Redis, FinWallet.Api and fake-provider ports stay inside Docker networks. `compose.debug.yml` publishes them only on `127.0.0.1` for local inspection/debugging.

### Docker files

| File | Purpose |
|---|---|
| `compose.yml` | Main Compose definition for the application and infrastructure services. |
| `compose.debug.yml` | Debug overlay exposing MSSQL, Redis, FinWallet.Api and provider ports on localhost only. |
| `compose.production.yml` | Production-like hardening overrides such as disabling Swagger and stronger restart policies. |
| `docker/Dockerfile.webapi` | Shared multi-stage .NET 8 image build for Gateway, FinWallet.Api and all fake Web APIs. |
| `docker/mssql/init-db.sh` | Waits for MSSQL and applies the `001`, `002`, `003` schema scripts with schema-version tracking. |
| `.env.example` | Template for local Compose environment variables; contains no real production secrets. |
| `.dockerignore` | Removes bin/obj/log/test/docs/local-secret content from the Docker build context. |
| `.gitignore` | Prevents build output, logs, secrets and local Docker state from entering Git. |

### Docker services

| Service | Responsibility | Normal host port |
|---|---|---|
| `gateway` | YARP edge gateway, JWT/routing/rate limit/load balancing | `8080` |
| `finwallet-api` | Main FinWallet Web API | not published |
| `fake-bank` | Bank simulator | not published |
| `fake-fraud` | Fraud simulator | not published |
| `fake-cutoff` | Cutoff/business-calendar simulator | not published |
| `fake-campaign` | Campaign simulator | not published |
| `fake-communication` | SMS/Email simulator | not published |
| `mssql` | Financial source of truth | not published |
| `mssql-init` | One-shot schema initialization container | none |
| `redis` | OTP/transient support state | not published |

### Persistent volumes

| Volume | Content | Survives `docker compose down`? |
|---|---|---|
| `finwallet_mssql_data` | SQL Server database/log files | Yes |
| `finwallet_mssql_backup` | SQL backup area | Yes |
| `finwallet_redis_data` | Redis AOF/RDB persistence | Yes |

Application containers are stateless. Application logs are not persisted to application-specific named volumes; they go to stdout/stderr and Docker log rotation is configured.

### 1. Verify prerequisites

```bash
docker --version
docker compose version
```

What it does:
- Confirms Docker Engine/Desktop CLI is available.
- Confirms the Compose v2 plugin is installed.
- If these fail, fix Docker before starting FinWallet.

On Windows Docker Desktop, use Linux containers.

### 2. Change to the repository root

```bash
cd finwallet
```

Run the following commands from the directory containing `FinWallet.sln`, `compose.yml`, and `.env.example`.

### 3. Create the local environment file

PowerShell:

```powershell
Copy-Item .env.example .env
```

Bash:

```bash
cp .env.example .env
```

What it does:
- Creates a local `.env` from the version-controlled template.
- `.env` is ignored by Git and must not be committed.

Replace at least these values with strong local values:

```text
MSSQL_SA_PASSWORD
REDIS_PASSWORD
JWT_SIGNING_KEY
REGISTRATION_OTP_PEPPER
INTERNAL_SERVICE_KEY
DOWNSTREAM_SERVICE_KEY
```

Do not use `.env` as the production secret-management mechanism. Use orchestrator/secret-store injection in production.

### 4. Validate Compose before starting containers

```bash
docker compose --env-file .env config --quiet
```

What it does:
- Resolves environment substitutions.
- Validates YAML/Compose syntax.
- Fails early when a required environment variable is missing.
- Does not start containers or modify data.

To print the resolved model:

```bash
docker compose --env-file .env config
```

Warning: resolved output can contain secrets. Do not redirect it into shared CI logs or files.

List services:

```bash
docker compose --env-file .env config --services
```

### 5. Build images

```bash
docker compose --env-file .env build --pull
```

What it does:
- Builds Gateway, FinWallet.Api and fake API images using the multi-stage Dockerfile.
- `--pull` checks for a newer base image matching the configured tag.
- NuGet restore uses the BuildKit cache.
- Does not start containers.

For a fully clean build:

```bash
docker compose --env-file .env build --pull --no-cache
```

This is slower and should not be the default development workflow.

### 6. Start the entire system

```bash
docker compose --env-file .env up -d --build
```

What it does:
1. Builds/pulls required images.
2. Creates `finwallet-data` and `finwallet-backend` networks.
3. Creates or reuses MSSQL and Redis named volumes.
4. Waits for MSSQL health checks.
5. Runs `mssql-init`, which creates the FinWallet database and unapplied schema versions.
6. Starts FinWallet.Api after Redis is healthy and DB initialization completes.
7. Starts fake providers and Gateway.
8. `-d` runs the stack in detached mode.

The first build/pull takes longer than subsequent starts.

### 7. Inspect container status

```bash
docker compose --env-file .env ps
```

Expected state:
- `mssql`: Up / healthy
- `redis`: Up / healthy
- `mssql-init`: Exited (0)
- API and Gateway services: Up

`mssql-init` being `Exited (0)` is expected because it is a one-shot migration job.

Docker-level view:

```bash
docker ps --filter name=finwallet
```

### 8. Verify the Gateway health endpoint

```bash
curl http://localhost:8080/health/live
```

Windows PowerShell:

```powershell
Invoke-RestMethod http://localhost:8080/health/live
```

If this responds, the host-to-Gateway path is available.

When Swagger is enabled in Docker/Development:

```text
http://localhost:8080/swagger
```

Normal client API calls should use Gateway at `http://localhost:8080`.

### 9. Follow logs

Entire stack:

```bash
docker compose --env-file .env logs -f
```

Gateway:

```bash
docker compose --env-file .env logs -f gateway
```

FinWallet API:

```bash
docker compose --env-file .env logs -f finwallet-api
```

MSSQL:

```bash
docker compose --env-file .env logs -f mssql
```

Redis:

```bash
docker compose --env-file .env logs -f redis
```

Last 200 lines:

```bash
docker compose --env-file .env logs --tail 200 gateway finwallet-api
```

`-f` follows new log entries. `Ctrl+C` exits log following only; detached containers continue running.

Docker `json-file` log rotation is configured. Application logs are sent to stdout/stderr rather than durable files inside application containers.

### 10. Verify MSSQL

```bash
docker compose --env-file .env exec mssql bash -lc '
SQLCMD=$(command -v /opt/mssql-tools18/bin/sqlcmd || command -v /opt/mssql-tools/bin/sqlcmd) &&
$SQLCMD -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -d FinWallet -Q "SELECT DB_NAME() AS DatabaseName; SELECT Version, AppliedAt FROM dbo.SchemaVersions ORDER BY Version;"
'
```

This:
- connects from inside the SQL Server container;
- confirms the `FinWallet` database exists;
- lists applied `001`, `002`, `003` schema versions.

Re-run the migration check manually with:

```bash
docker compose --env-file .env run --rm mssql-init
```

Already-recorded schema versions are skipped.

### 11. Verify Redis

```bash
docker compose --env-file .env exec redis sh -lc 'redis-cli -a "$REDIS_PASSWORD" --no-auth-warning ping'
```

Expected:

```text
PONG
```

Persistence details:

```bash
docker compose --env-file .env exec redis sh -lc 'redis-cli -a "$REDIS_PASSWORD" --no-auth-warning INFO persistence'
```

### 12. Publish debug ports

The normal stack intentionally does not publish backend services. For local debugging:

```bash
docker compose --env-file .env -f compose.yml -f compose.debug.yml up -d --build
```

Loopback ports become:

```text
Gateway              8080
FinWallet.Api         8081
FakeBank.Api          8082
FakeFraud.Api         8083
FakeCutoff.Api        8084
FakeCampaign.Api      8085
FakeCommunication.Api 8086
MSSQL                 1433
Redis                 6379
```

The overlay binds debug ports to `127.0.0.1`, not the LAN.

Use Gateway for normal integration calls. Direct backend ports are for inspection/debugging and downstream service-key checks remain active.

### 13. Rebuild/restart one service

If FinWallet.Api code changed:

```bash
docker compose --env-file .env build finwallet-api
docker compose --env-file .env up -d --no-deps finwallet-api
```

What it does:
- rebuilds only the FinWallet.Api image;
- recreates only that service;
- `--no-deps` avoids recreating MSSQL/Redis/Gateway.

For Gateway:

```bash
docker compose --env-file .env build gateway
docker compose --env-file .env up -d --no-deps gateway
```

Restart without rebuilding:

```bash
docker compose --env-file .env restart gateway
```

### 14. Open a shell inside a service

```bash
docker compose --env-file .env exec finwallet-api /bin/sh
```

The runtime image executes the service as the non-root `app` user. Do not expect root privileges.

Avoid printing the full environment because it contains secrets.

### 15. Monitor resource usage

```bash
docker stats
```

It shows CPU, memory, network I/O, block I/O and PID counts.

Compose sets CPU, memory and PID limits for application containers. These are local safety baselines, not production capacity numbers.

### 16. Inspect volumes

```bash
docker volume ls --filter name=finwallet
```

Inspect individually:

```bash
docker volume inspect finwallet_mssql_data
docker volume inspect finwallet_mssql_backup
docker volume inspect finwallet_redis_data
```

Normal restart/recreate operations keep these volumes.

### 17. Create an SQL backup

Create a backup:

```bash
docker compose --env-file .env exec mssql bash -lc '
SQLCMD=$(command -v /opt/mssql-tools18/bin/sqlcmd || command -v /opt/mssql-tools/bin/sqlcmd) &&
$SQLCMD -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -Q "BACKUP DATABASE [FinWallet] TO DISK = N'"'"'/var/opt/mssql/backup/FinWallet.bak'"'"' WITH INIT, CHECKSUM"
'
```

The backup remains in the `mssql_backup` named volume.

Get the container ID:

```bash
docker compose ps -q mssql
```

Copy to the host in Bash:

```bash
docker cp "$(docker compose ps -q mssql):/var/opt/mssql/backup/FinWallet.bak" ./FinWallet.bak
```

Do not commit backups to Git.

### 18. Request a Redis snapshot

```bash
docker compose --env-file .env exec redis sh -lc 'redis-cli -a "$REDIS_PASSWORD" --no-auth-warning BGSAVE'
```

This asks Redis to create a background snapshot. Redis is still not the financial source of truth and this is not a substitute for MSSQL backup.

### 19. Stop and start without deleting containers

Stop:

```bash
docker compose --env-file .env stop
```

Start:

```bash
docker compose --env-file .env start
```

`stop` does not remove containers or volumes.

### 20. Shut down while preserving data

```bash
docker compose --env-file .env down
```

It:
- removes Compose containers;
- removes Compose networks;
- preserves named volumes;
- allows the next `up` to reuse MSSQL/Redis data.

### 21. Full reset - DESTRUCTIVE

```bash
docker compose --env-file .env down -v --remove-orphans
```

It removes:
- containers;
- networks;
- `mssql_data`, `mssql_backup`, `redis_data` named volumes.

Result: local database, ledger, customer/auth data, Redis state and SQL backup volume are deleted.

Use this only for a deliberate clean local reset.

### 22. Reset Redis only

```bash
docker compose --env-file .env down
docker volume rm finwallet_redis_data
docker compose --env-file .env up -d
```

MSSQL data remains intact.

### 23. Reset MSSQL only

```bash
docker compose --env-file .env down
docker volume rm finwallet_mssql_data finwallet_mssql_backup
docker compose --env-file .env up -d
```

This permanently removes local financial data. Do not use outside disposable local development.

### 24. Inspect and clean Docker disk usage

```bash
docker system df
```

Build cache cleanup:

```bash
docker builder prune
```

Unused image cleanup:

```bash
docker image prune
```

`docker system prune -a --volumes` is much more destructive and can remove resources used by other projects; it should not be a routine command.

### 25. Use the production-like overlay

```bash
docker compose \
  --env-file .env \
  -f compose.yml \
  -f compose.production.yml \
  up -d --build
```

The overlay:
- sets `ASPNETCORE_ENVIRONMENT=Production`;
- disables Swagger;
- strengthens restart policy;
- enables HSTS configuration.

This is not a replacement for a real production platform. Real production also requires TLS ingress, external secret management, WAF/DDoS controls, image digest pinning, vulnerability scanning, backup/restore procedures, monitoring/alerting and network policy.

### 26. Update the running stack after code changes

```bash
git pull
docker compose --env-file .env build --pull
docker compose --env-file .env up -d
```

When a new schema script is added, `mssql-init` must also be updated to recognize the new migration. Merely adding a SQL file is not sufficient.

### 27. Common problems

#### Port 8080 already in use

```bash
docker compose --env-file .env ps
```

Change `.env`:

```text
GATEWAY_PORT=8090
```

Run `up -d` again. Gateway becomes `http://localhost:8090`.

#### MSSQL unhealthy

```bash
docker compose --env-file .env logs --tail 300 mssql
```

Check:
- SA password complexity;
- Docker memory availability;
- damaged/incompatible volume;
- restart loop.

#### `mssql-init` failed

```bash
docker compose --env-file .env logs mssql-init
```

Fix the schema error, then run:

```bash
docker compose --env-file .env run --rm mssql-init
```

#### Redis unhealthy

```bash
docker compose --env-file .env logs redis
docker compose --env-file .env exec redis sh -lc 'redis-cli -a "$REDIS_PASSWORD" --no-auth-warning ping'
```

#### Gateway returns 502/503

```bash
docker compose --env-file .env logs --tail 200 gateway finwallet-api fake-bank fake-fraud fake-cutoff fake-campaign fake-communication
```

Check:
- downstream service is Up;
- Docker service DNS address is correct;
- internal/downstream keys match;
- Gateway health checking has not marked the destination unhealthy.

#### Required secret is missing

Compose fails with a message similar to:

```text
required variable ... is missing a value
```

Confirm `.env` exists at repository root and required keys are non-empty.

### 28. Minimum daily development command set

First day:

```bash
cp .env.example .env
docker compose --env-file .env config --quiet
docker compose --env-file .env up -d --build
docker compose --env-file .env ps
docker compose --env-file .env logs -f gateway finwallet-api
```

At the end of the day, preserve data:

```bash
docker compose --env-file .env down
```

Next day:

```bash
docker compose --env-file .env up -d
docker compose --env-file .env ps
```

For a full disposable local reset:

```bash
docker compose --env-file .env down -v --remove-orphans
docker compose --env-file .env up -d --build
```

Remember: the reset command deletes local MSSQL and Redis data.
