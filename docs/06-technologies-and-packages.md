# Teknolojiler ve NuGet Paketleri / Technologies and NuGet Packages

## Türkçe

### Teknoloji baseline
| Teknoloji | Versiyon/Politika | Amaç |
|---|---|---|
| .NET | 8 | Runtime ve application platform |
| ASP.NET Core | 8 | Controller-based Web API |
| C# | 12 | Uygulama dili |
| YARP | 2.3.0 | Reverse proxy, routing, health, load balancing |
| Swashbuckle | 10.2.3 | Swagger/OpenAPI üretimi ve UI |
| Microsoft.Data.SqlClient | 7.0.2 | MSSQL durable persistence |
| StackExchange.Redis | 3.0.17 | Transient Redis state |
| JwtBearer | 8.0.29 | Gateway/API JWT validation |
| xUnit v3 | 3.2.2 | Unit test framework |
| Moq | 4.20.72 | Application boundary mockları |

### Paket politikası
Paid/freemium NuGet dependency kabul edilmez. Seçim sırası:
1. .NET/ASP.NET Core built-in capability;
2. uygun Microsoft-maintained package;
3. yalnız açık kaynak, ücretsiz ve lisansı uyumlu third-party package.

Tüm paket versiyonları `Directory.Packages.props` ile merkezi pinlenir.

### JwtBearer
JWT validation Gateway ve FinWallet.Api'de kullanılır. Hand-written JWT parsing/signing güvenlik riski nedeniyle tercih edilmez. Signing algorithm code-level invariant olarak sabittir.

### Microsoft.Data.SqlClient
Explicit async SQL, transaction, locking ve financial persistence için kullanılır. MSSQL durable financial authority'dir. Financial path'te SQL parameterized olmalıdır. Generic ORM/repository abstraction locking görünürlüğünü gizlememelidir.

### StackExchange.Redis
OTP/transient distributed state için kullanılır. `ConnectionMultiplexer` singleton'dır. Redis Wallet/Ledger/idempotency financial source of truth olmaz.

### Yarp.ReverseProxy
Gateway route matching, authorization policy, transform, cluster load balancing, health check ve transport tuning için kullanılır. Financial business rule içermez.

### Swashbuckle.AspNetCore
Tüm Web API'lerde ortak Swagger generation/UI sağlar. `FinWallet.Shared.Web` üzerinden register edilir. Production'da varsayılan kapalıdır.

### xUnit v3 + Moq
Application orchestration unit testleri için kullanılır. Moq, external provider/persistence boundary call'larını izole etmek için uygundur; gerçek MSSQL locking, Redis Lua veya YARP routing'i kanıtlamaz.

### Framework-only kullanılan yetenekler
Ek paket olmadan:
- Kestrel limits;
- ASP.NET Core rate limiting;
- CORS;
- security headers middleware;
- HttpClientFactory/SocketsHttpHandler;
- PBKDF2/HMAC/SHA cryptography;
- CancellationToken/TimeProvider.

### PBKDF2 notu
PBKDF2 V1 work factor loose appsettings tuning değildir. Mevcut persisted credential row'ları per-password iteration metadata saklamadığı için work factor değişimi versioned hash migration/rehash gerektirir.

### Yeni paket onay kaydı
Her yeni package için şu bilgiler dokümante edilmelidir: exact ID, version, license, owner project, purpose, neden built-in capability yeterli değil, alternatifler ve financial/security impact.

---

## English

### Technology baseline
| Technology | Version/Policy | Purpose |
|---|---|---|
| .NET | 8 | Runtime and application platform |
| ASP.NET Core | 8 | Controller-based Web API |
| C# | 12 | Application language |
| YARP | 2.3.0 | Reverse proxy, routing, health, load balancing |
| Swashbuckle | 10.2.3 | Swagger/OpenAPI generation and UI |
| Microsoft.Data.SqlClient | 7.0.2 | MSSQL durable persistence |
| StackExchange.Redis | 3.0.17 | Transient Redis state |
| JwtBearer | 8.0.29 | Gateway/API JWT validation |
| xUnit v3 | 3.2.2 | Unit-test framework |
| Moq | 4.20.72 | Application-boundary mocks |

### Package policy
Paid/freemium NuGet dependencies are not allowed. Selection order:
1. built-in .NET/ASP.NET Core capability;
2. an appropriate Microsoft-maintained package;
3. only fully free/open-source third-party packages with compatible licensing.

All versions are centrally pinned in `Directory.Packages.props`.

### JwtBearer
Used for JWT validation at Gateway and FinWallet.Api. Hand-written JWT parsing/signing is avoided because it introduces unnecessary security risk. The signing algorithm remains a code-level invariant.

### Microsoft.Data.SqlClient
Used for explicit async SQL, transactions, locking and financial persistence. MSSQL is the durable financial authority. Financial SQL values must be parameterized. Generic ORM/repository abstractions must not hide important locking behavior.

### StackExchange.Redis
Used for OTP and other transient distributed state. `ConnectionMultiplexer` is singleton. Redis never becomes the Wallet/Ledger/idempotency financial source of truth.

### Yarp.ReverseProxy
Used for Gateway route matching, authorization policies, transforms, cluster load balancing, health checks and transport tuning. It does not contain financial business rules.

### Swashbuckle.AspNetCore
Provides common Swagger generation/UI across all Web APIs through `FinWallet.Shared.Web`. It is disabled by default in production.

### xUnit v3 + Moq
Used for Application orchestration unit tests. Moq is appropriate for isolating external-provider/persistence boundary calls, but does not prove real MSSQL locking, Redis Lua or YARP routing behavior.

### Framework-only capabilities
No separate package is added for:
- Kestrel limits;
- ASP.NET Core rate limiting;
- CORS;
- security-header middleware;
- HttpClientFactory/SocketsHttpHandler;
- PBKDF2/HMAC/SHA cryptography;
- CancellationToken/TimeProvider.

### PBKDF2 note
PBKDF2 V1 work factor is not a loose appsettings tuning value. Existing credential rows do not store per-password iteration metadata, so changing it requires a versioned hash migration/rehash design.

### New-package approval record
Every future package must document exact ID, version, license, owner project, purpose, why built-in functionality is insufficient, considered alternatives and financial/security impact.
