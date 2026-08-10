# FinWallet Sistem Topolojisi / FinWallet System Topology

## Türkçe

### 1. Amaç
Bu belge FinWallet v1'in çalışma zamanındaki servis, güvenlik, veri, finansal işlem ve Docker topolojisini tek yerde gösterir. Amaç projeyi ilk kez gören bir geliştiricinin hangi bileşenin nerede çalıştığını, trafiğin hangi güven sınırlarından geçtiğini, hangi verinin nerede tutulduğunu ve hangi parçaların birlikte ölçeklendiğini hızlıca anlayabilmesidir.

### 2. Yüksek seviye runtime topolojisi

```text
                               PUBLIC / CLIENT ZONE

                          +-----------------------+
                          | Web / Mobile / Postman |
                          +-----------+-----------+
                                      |
                                      | HTTPS / JWT
                                      v
                          +-----------------------+
                          |   FinWallet.Gateway   |
                          |        YARP           |
                          | :8080                 |
                          | JWT / Rate Limit      |
                          | Load Balancing        |
                          +----+-------------+----+
                               |             |
               public /api/*   |             | internal /providers/*
                               |             |
                               v             v
                     +----------------+   +-------------------------+
                     | FinWallet.Api  |   | Fake Provider APIs      |
                     | :8081 internal |   |                         |
                     +-------+--------+   | FakeBank          :8082 |
                             |            | FakeFraud         :8083 |
                             |            | FakeCutoff        :8084 |
                             |            | FakeCampaign      :8085 |
                             |            | FakeCommunication :8086 |
                             |            +-------------------------+
                             |
                  +----------+----------+
                  |                     |
                  v                     v
          +---------------+      +---------------+
          | MSSQL         |      | Redis         |
          | financial     |      | transient     |
          | source truth  |      | support state |
          +---------------+      +---------------+
```

Normal client yalnız Gateway'i çağırır. `FinWallet.Api` ve Fake provider servisleri public giriş noktası değildir.

### 3. Uygulama katman topolojisi

```text
FinWallet.Gateway
        |
        v
FinWallet.Api
        |
        +--> FinWallet.Application
        |         |
        |         v
        |    FinWallet.Domain
        |
        +--> FinWallet.Infrastructure
                  |
                  +--> MSSQL
                  +--> Redis
                  +--> JWT/token infrastructure
                  +--> Gateway üzerinden provider HttpClient adapter'ları

Shared:
  FinWallet.Shared.Contracts -> ServiceResult ve ortak transport sözleşmeleri
  FinWallet.Shared.Web       -> Swagger, rate limit, CORS, Kestrel limitleri,
                                security header'ları, internal service-key kontrolü
```

Dependency yönü:

```text
Api -> Application -> Domain
Api -> Infrastructure -> Application / Domain
Gateway -> Shared.Web / Shared.Contracts
Fake APIs -> Shared.Web / Shared.Contracts
```

`Domain` SQL, HTTP, Redis veya provider DTO bilmez. `Application` concrete Infrastructure implementasyonuna bağımlı değildir.

### 4. Gateway ve trust boundary topolojisi

```text
[Client]
   |
   | JWT
   v
[Gateway]
   |
   | JWT + DownstreamServiceKey
   v
[FinWallet.Api]

[FinWallet.Api]
   |
   | InternalServiceKey
   v
[Gateway /providers/*]
   |
   | DownstreamServiceKey
   v
[Fake Provider]
```

Bu model dört ayrı kontrol noktası oluşturur:

1. Client -> Gateway: public edge authentication ve rate-limit.
2. Gateway -> FinWallet.Api: Gateway ayrıca downstream service credential ekler; API JWT ve ownership kontrolünü tekrar yapar.
3. FinWallet.Api -> Gateway provider route: `InternalServiceKey` ile yalnız trusted internal caller kabul edilir.
4. Gateway -> Fake provider: ayrı `DownstreamServiceKey` destination service tarafından doğrulanır.

Sonuç: Gateway bypass edilip doğrudan backend portuna erişilse bile business endpoint otomatik olarak güvenilir hale gelmez.

### 5. YARP route ve load-balancing topolojisi

```text
Client /api/*
    |
    v
route: finwallet-api
    |
    v
cluster: finwallet-api-cluster
    |
    +--> FinWallet.Api replica 1
    +--> FinWallet.Api replica 2   (production eklenebilir)
    +--> FinWallet.Api replica N

FinWallet.Api /providers/bank/*
    |
    v
route: fake-bank
    |
    v
cluster: fake-bank-cluster
    |
    +--> FakeBank replica 1
    +--> FakeBank replica N
```

YARP cluster'larında load-balancing policy `PowerOfTwoChoices` olarak yapılandırılabilir. Destination adresleri, health-check, max connection ve timeout değerleri appsettings üzerinden yönetilir. Development ortamında tek destination yeterlidir; production ortamında aynı cluster'a replica eklenebilir.

### 6. Public API trafik topolojisi

```text
Client
  |
  | POST /api/v1/auth/register
  | POST /api/v1/auth/login
  | POST /api/v1/wallets
  | POST /api/v1/bank-accounts
  | POST /api/v1/bank-movements/deposits
  | POST /api/v1/transfers
  | POST /api/v1/purchases
  v
Gateway
  |
  v
FinWallet.Api Controllers
  |
  v
Application Handler
  |
  +--> Domain rules
  +--> MSSQL / Redis
  +--> gerektiğinde Gateway /providers/*
```

Anonymous route'lar register, registration verify, login ve refresh ile sınırlıdır. Diğer müşteri endpoint'lerinde Gateway JWT ister.

### 7. Provider entegrasyon topolojisi

```text
FinWallet.Api
   |
   +--> /providers/bank/* ----------> FakeBank.Api
   |
   +--> /providers/fraud/* ---------> FakeFraud.Api
   |
   +--> /providers/cutoff/* --------> FakeCutoff.Api
   |
   +--> /providers/campaign/* ------> FakeCampaign.Api
   |
   +--> /providers/communication/* -> FakeCommunication.Api
```

Provider sorumlulukları:

| Servis | Sorumluluk |
|---|---|
| FakeBank.Api | Dış banka hesabı, provider transaction, statement, timeout/failure simulatorı |
| FakeFraud.Api | External Allow/Review/Deny, score ve reason code |
| FakeCutoff.Api | Business day, cutoff, processing ve settlement tarihi |
| FakeCampaign.Api | Merchant campaign eligibility, discount ve sponsor tipi |
| FakeCommunication.Api | SMS/e-posta kabul, delay, timeout ve failure simulatorı |

Provider DTO'ları Application/Domain'e sızmaz; Infrastructure adapter/anti-corruption layer içinde map edilir.

### 8. Veri sahipliği topolojisi

```text
                      +-----------------------------+
                      | MSSQL - AUTHORITATIVE       |
                      +-----------------------------+
                      | Customer                    |
                      | Credential / Session        |
                      | Wallet / BankAccount        |
                      | FinancialTransaction        |
                      | LedgerJournal / LedgerEntry |
                      | Idempotency                 |
                      | FraudEvent / Review         |
                      | Outbox / Inbox              |
                      | Reconciliation              |
                      | Audit                       |
                      +-----------------------------+

                      +-----------------------------+
                      | Redis - TRANSIENT SUPPORT   |
                      +-----------------------------+
                      | OTP TTL                     |
                      | temporary counters          |
                      | fraud velocity              |
                      | hot/session cache support   |
                      | temporary coordination      |
                      +-----------------------------+
```

Finansal doğruluk için Redis tek otorite değildir. Wallet balance, ledger, completed transaction ve durable idempotency MSSQL'de tutulur.

### 9. Bankadan Wallet'a para giriş topolojisi

```text
Customer Bank Account (FakeBank)
        |
        | provider withdrawal
        v
FinWallet Bank Settlement Asset
        |
        | same DB transaction
        +--> Customer Wallet Liability +1000
        +--> FinancialTransaction = Completed
        +--> LedgerJournal
        +--> LedgerEntries (Debit = Credit)
        +--> Idempotency final result
        +--> Outbox notification
```

Önemli ayrım: `BankAccount` ile `Wallet` aynı şey değildir. BankAccount dış banka hesabını; Wallet FinWallet içindeki müşteri bakiyesini temsil eder.

### 10. Wallet-to-Wallet transfer topolojisi

```text
Client
  |
  v
Gateway JWT
  |
  v
FinWallet.Api
  |
  +--> completed idempotency replay check
  +--> server-side risk signals
  +--> Internal Fraud
  +--> Gateway -> FakeFraud
  +--> Allow / Review / Deny
  |
  +--> ALLOW ise tek MSSQL transaction:
          source Wallet debit
          destination Wallet credit
          FinancialTransaction
          LedgerJournal
          LedgerEntries
          durable Idempotency result
```

External fraud HTTP çağrısı açık SQL transaction içinde yapılmaz.

### 11. Purchase topolojisi

```text
Client
  |
  v
Gateway -> FinWallet.Api
  |
  +--> internal fraud
  +--> external FakeFraud
  +--> FakeCampaign
  |
  v
Atomic MSSQL posting
  |
  +--> customer wallet liability debit
  +--> merchant payable credit
  +--> campaign expense debit (platform sponsor ise)
  +--> FinancialTransaction
  +--> balanced Ledger
  +--> Outbox
```

Campaign provider yalnız indirim/uygunluk bilgisini hesaplar; finansal muhasebe FinWallet tarafından yapılır.

### 12. Background worker topolojisi

```text
FinWallet.Api process
   |
   +--> BankMoneyMovementBackgroundService
   |       |
   |       +--> due Scheduled/Pending transactions
   |       +--> Gateway -> FakeBank status / processing
   |       +--> durable finalization / compensation
   |
   +--> OutboxDispatchBackgroundService
           |
           +--> claim Outbox row
           +--> SQL transaction closes
           +--> Gateway -> FakeCommunication
           +--> success/failure/backoff state
```

Notification başarısızlığı tamamlanmış para hareketini rollback etmez. Outbox yeniden dener.

### 13. Inbox callback topolojisi

```text
FakeBank / test provider
   |
   | X-Internal-Service-Key
   v
Gateway InternalService route
   |
   v
/api/v1/internal/bank/callbacks
   |
   v
Inbox dedupe (Source + MessageId)
   |
   v
Bank movement finalization
```

Aynı callback tekrar gelirse Inbox ve terminal financial state duplicate money movement oluşmasını engeller.

### 14. Reconciliation topolojisi

```text
Internal Reconciliation API
   |
   +--> Wallet <-> Ledger-derived balance
   |
   +--> Bank transaction <-> Settlement ledger
   |
   +--> FinWallet <-> FakeBank statement
   |
   v
ReconciliationRun + ReconciliationIssue
```

Reconciliation fark bulduğunda bakiyeyi otomatik düzeltmez; issue üretir.

### 15. Swagger topolojisi

Swagger ortak `FinWallet.Shared.Web` üzerinden aşağıdaki HTTP projelerinde vardır:

```text
FinWallet.Gateway
FinWallet.Api
FakeBank.Api
FakeFraud.Api
FakeCutoff.Api
FakeCampaign.Api
FakeCommunication.Api
```

Development'da açılabilir; production'da varsayılan kapalıdır. Swagger açık olması endpoint authorization'ını bypass etmez.

### 16. Docker runtime topolojisi

```text
HOST
  |
  | localhost:8080
  v
+----------------------- finwallet-backend network -----------------------+
|                                                                         |
| Gateway                                                                 |
|   |                                                                     |
|   +--> FinWallet.Api ------------------------------------------------+   |
|   |                                                                |   |
|   +--> FakeBank                                                    |   |
|   +--> FakeFraud                                                   |   |
|   +--> FakeCutoff                                                  |   |
|   +--> FakeCampaign                                                |   |
|   +--> FakeCommunication                                           |   |
+--------------------------------------------------------------------|---+
                                                                     |
                         +----------- finwallet-data network ----------+
                         |                                           |
                         |   MSSQL                 Redis              |
                         |     |                     |                |
                         |     v                     v                |
                         | finwallet_mssql_data  finwallet_redis_data|
                         +-------------------------------------------+
```

Normal Compose kullanımında host'a yalnız Gateway `8080` publish edilir. Backend API'ler, MSSQL ve Redis Docker network içinde kalır. Debug overlay gerektiğinde backend portlarını yalnız `127.0.0.1` üzerinden açar.

### 17. Docker persistent volume topolojisi

```text
mssql container
  +--> finwallet_mssql_data   : database data/log
  +--> finwallet_mssql_backup : backup files

redis container
  +--> finwallet_redis_data   : AOF/RDB persistence

application containers
  +--> named persistent application volume YOK
       stdout/stderr -> Docker log driver
```

`docker compose down` volume'ları korur. `docker compose down -v` MSSQL ve Redis local persistence'ını siler.

### 18. Ölçekleme sınırları
Gateway, FinWallet.Api ve stateless provider HTTP servisleri replica olarak çoğaltılabilir. Finansal correctness replica sayısına bağlı değildir; MSSQL constraint/transaction/idempotency kuralları multi-instance correctness sağlamalıdır.

Redis distributed financial lock olarak kullanılmaz. Bir instance kaybolduğunda committed financial state MSSQL'de kalır.

### 19. Finansal consistency özeti

```text
External HTTP
   |
   | SQL transaction açık DEĞİL
   v
Provider result
   |
   v
Short MSSQL transaction
   |
   +--> Wallet / BlockedBalance
   +--> FinancialTransaction
   +--> Ledger
   +--> Idempotency
   +--> Outbox
   v
COMMIT
```

Bu sınır distributed transaction ihtiyacını azaltır ve para hareketinin yarım commit edilmesini engeller.

### 20. Bir isteği izlemek için referanslar
Uçtan uca troubleshooting sırasında aşağıdaki referanslar birlikte kullanılır:

```text
X-Correlation-Id
CustomerId
SessionId
TransactionId
BankReference / ExternalTransactionId
FraudReference
CampaignReference
CutoffReference
Outbox / Inbox message id
```

Token, OTP, password, connection string ve diğer secret'lar loglanmaz.

---

## English

### 1. Purpose
This document presents the runtime service, security, data, financial-flow and Docker topology of FinWallet v1 in one place. It is intended to let an engineer who is new to the repository quickly understand where components run, which trust boundaries traffic crosses, where data is stored and which components can scale independently.

### 2. High-level runtime topology

```text
                               PUBLIC / CLIENT ZONE

                          +-----------------------+
                          | Web / Mobile / Postman |
                          +-----------+-----------+
                                      |
                                      | HTTPS / JWT
                                      v
                          +-----------------------+
                          |   FinWallet.Gateway   |
                          |        YARP           |
                          | :8080                 |
                          | JWT / Rate Limit      |
                          | Load Balancing        |
                          +----+-------------+----+
                               |             |
               public /api/*   |             | internal /providers/*
                               |             |
                               v             v
                     +----------------+   +-------------------------+
                     | FinWallet.Api  |   | Fake Provider APIs      |
                     | :8081 internal |   |                         |
                     +-------+--------+   | FakeBank          :8082 |
                             |            | FakeFraud         :8083 |
                             |            | FakeCutoff        :8084 |
                             |            | FakeCampaign      :8085 |
                             |            | FakeCommunication :8086 |
                             |            +-------------------------+
                             |
                  +----------+----------+
                  |                     |
                  v                     v
          +---------------+      +---------------+
          | MSSQL         |      | Redis         |
          | financial     |      | transient     |
          | source truth  |      | support state |
          +---------------+      +---------------+
```

Under normal operation the client calls only the Gateway. `FinWallet.Api` and Fake provider services are not public ingress points.

### 3. Application-layer topology

```text
FinWallet.Gateway
        |
        v
FinWallet.Api
        |
        +--> FinWallet.Application
        |         |
        |         v
        |    FinWallet.Domain
        |
        +--> FinWallet.Infrastructure
                  |
                  +--> MSSQL
                  +--> Redis
                  +--> JWT/token infrastructure
                  +--> provider HttpClient adapters through Gateway

Shared:
  FinWallet.Shared.Contracts -> ServiceResult and common transport contracts
  FinWallet.Shared.Web       -> Swagger, rate limiting, CORS, Kestrel limits,
                                security headers and internal-service-key checks
```

Dependency direction:

```text
Api -> Application -> Domain
Api -> Infrastructure -> Application / Domain
Gateway -> Shared.Web / Shared.Contracts
Fake APIs -> Shared.Web / Shared.Contracts
```

`Domain` knows nothing about SQL, HTTP, Redis or provider DTOs. `Application` does not depend on concrete Infrastructure implementations.

### 4. Gateway and trust-boundary topology

```text
[Client]
   |
   | JWT
   v
[Gateway]
   |
   | JWT + DownstreamServiceKey
   v
[FinWallet.Api]

[FinWallet.Api]
   |
   | InternalServiceKey
   v
[Gateway /providers/*]
   |
   | DownstreamServiceKey
   v
[Fake Provider]
```

This creates four distinct control points:

1. Client -> Gateway: public edge authentication and rate limiting.
2. Gateway -> FinWallet.Api: Gateway adds a downstream service credential; the API independently validates JWT and ownership.
3. FinWallet.Api -> Gateway provider route: `InternalServiceKey` restricts provider routes to trusted internal callers.
4. Gateway -> Fake provider: the destination validates a separate `DownstreamServiceKey`.

Result: direct access to a backend port does not automatically make that request trusted.

### 5. YARP route and load-balancing topology

```text
Client /api/*
    |
    v
route: finwallet-api
    |
    v
cluster: finwallet-api-cluster
    |
    +--> FinWallet.Api replica 1
    +--> FinWallet.Api replica 2   (can be added in production)
    +--> FinWallet.Api replica N

FinWallet.Api /providers/bank/*
    |
    v
route: fake-bank
    |
    v
cluster: fake-bank-cluster
    |
    +--> FakeBank replica 1
    +--> FakeBank replica N
```

YARP clusters can use `PowerOfTwoChoices`. Destination addresses, health checks, maximum connections and timeouts are managed through appsettings. Development can use a single destination while production can add replicas to the same cluster.

### 6. Public API traffic topology

```text
Client
  |
  | POST /api/v1/auth/register
  | POST /api/v1/auth/login
  | POST /api/v1/wallets
  | POST /api/v1/bank-accounts
  | POST /api/v1/bank-movements/deposits
  | POST /api/v1/transfers
  | POST /api/v1/purchases
  v
Gateway
  |
  v
FinWallet.Api Controllers
  |
  v
Application Handler
  |
  +--> Domain rules
  +--> MSSQL / Redis
  +--> Gateway /providers/* when required
```

Anonymous routes are limited to register, registration verification, login and refresh. Other customer routes require JWT at the Gateway.

### 7. Provider integration topology

```text
FinWallet.Api
   |
   +--> /providers/bank/* ----------> FakeBank.Api
   |
   +--> /providers/fraud/* ---------> FakeFraud.Api
   |
   +--> /providers/cutoff/* --------> FakeCutoff.Api
   |
   +--> /providers/campaign/* ------> FakeCampaign.Api
   |
   +--> /providers/communication/* -> FakeCommunication.Api
```

Provider responsibilities:

| Service | Responsibility |
|---|---|
| FakeBank.Api | External bank account, provider transaction, statement and timeout/failure simulation |
| FakeFraud.Api | External Allow/Review/Deny, score and reason codes |
| FakeCutoff.Api | Business day, cutoff, processing and settlement dates |
| FakeCampaign.Api | Merchant campaign eligibility, discount and sponsor type |
| FakeCommunication.Api | SMS/email acceptance, delay, timeout and failure simulation |

Provider DTOs do not leak into Application/Domain; Infrastructure adapters/anti-corruption layers map them.

### 8. Data-ownership topology

```text
                      +-----------------------------+
                      | MSSQL - AUTHORITATIVE       |
                      +-----------------------------+
                      | Customer                    |
                      | Credential / Session        |
                      | Wallet / BankAccount        |
                      | FinancialTransaction        |
                      | LedgerJournal / LedgerEntry |
                      | Idempotency                 |
                      | FraudEvent / Review         |
                      | Outbox / Inbox              |
                      | Reconciliation              |
                      | Audit                       |
                      +-----------------------------+

                      +-----------------------------+
                      | Redis - TRANSIENT SUPPORT   |
                      +-----------------------------+
                      | OTP TTL                     |
                      | temporary counters          |
                      | fraud velocity              |
                      | hot/session cache support   |
                      | temporary coordination      |
                      +-----------------------------+
```

Redis is never the sole authority for financial correctness. Wallet balances, ledger state, completed transactions and durable idempotency remain in MSSQL.

### 9. Bank-to-Wallet funding topology

```text
Customer Bank Account (FakeBank)
        |
        | provider withdrawal
        v
FinWallet Bank Settlement Asset
        |
        | same DB transaction
        +--> Customer Wallet Liability +1000
        +--> FinancialTransaction = Completed
        +--> LedgerJournal
        +--> LedgerEntries (Debit = Credit)
        +--> Idempotency final result
        +--> Outbox notification
```

Important distinction: `BankAccount` and `Wallet` are not the same thing. BankAccount models the external bank account; Wallet models the customer's balance inside FinWallet.

### 10. Wallet-to-Wallet transfer topology

```text
Client
  |
  v
Gateway JWT
  |
  v
FinWallet.Api
  |
  +--> completed idempotency replay check
  +--> server-side risk signals
  +--> Internal Fraud
  +--> Gateway -> FakeFraud
  +--> Allow / Review / Deny
  |
  +--> on ALLOW, one MSSQL transaction:
          source Wallet debit
          destination Wallet credit
          FinancialTransaction
          LedgerJournal
          LedgerEntries
          durable Idempotency result
```

The external fraud HTTP call is never executed while the SQL transaction is open.

### 11. Purchase topology

```text
Client
  |
  v
Gateway -> FinWallet.Api
  |
  +--> internal fraud
  +--> external FakeFraud
  +--> FakeCampaign
  |
  v
Atomic MSSQL posting
  |
  +--> customer wallet liability debit
  +--> merchant payable credit
  +--> campaign expense debit (platform-sponsored campaign)
  +--> FinancialTransaction
  +--> balanced Ledger
  +--> Outbox
```

The campaign provider only calculates eligibility/discount information; financial accounting remains inside FinWallet.

### 12. Background-worker topology

```text
FinWallet.Api process
   |
   +--> BankMoneyMovementBackgroundService
   |       |
   |       +--> due Scheduled/Pending transactions
   |       +--> Gateway -> FakeBank status / processing
   |       +--> durable finalization / compensation
   |
   +--> OutboxDispatchBackgroundService
           |
           +--> claim Outbox row
           +--> SQL transaction closes
           +--> Gateway -> FakeCommunication
           +--> success/failure/backoff state
```

A notification failure never rolls back an already committed money movement. Outbox retries delivery.

### 13. Inbox-callback topology

```text
FakeBank / test provider
   |
   | X-Internal-Service-Key
   v
Gateway InternalService route
   |
   v
/api/v1/internal/bank/callbacks
   |
   v
Inbox dedupe (Source + MessageId)
   |
   v
Bank movement finalization
```

Repeated callbacks cannot create duplicate money movement because Inbox and terminal financial state are replay-safe.

### 14. Reconciliation topology

```text
Internal Reconciliation API
   |
   +--> Wallet <-> Ledger-derived balance
   |
   +--> Bank transaction <-> Settlement ledger
   |
   +--> FinWallet <-> FakeBank statement
   |
   v
ReconciliationRun + ReconciliationIssue
```

Reconciliation reports differences but never silently mutates balances to make them match.

### 15. Swagger topology

Swagger is provided through `FinWallet.Shared.Web` for all HTTP projects:

```text
FinWallet.Gateway
FinWallet.Api
FakeBank.Api
FakeFraud.Api
FakeCutoff.Api
FakeCampaign.Api
FakeCommunication.Api
```

It can be enabled in development and is disabled by default in production. Swagger visibility never bypasses endpoint authorization.

### 16. Docker runtime topology

```text
HOST
  |
  | localhost:8080
  v
+----------------------- finwallet-backend network -----------------------+
|                                                                         |
| Gateway                                                                 |
|   |                                                                     |
|   +--> FinWallet.Api ------------------------------------------------+   |
|   |                                                                |   |
|   +--> FakeBank                                                    |   |
|   +--> FakeFraud                                                   |   |
|   +--> FakeCutoff                                                  |   |
|   +--> FakeCampaign                                                |   |
|   +--> FakeCommunication                                           |   |
+--------------------------------------------------------------------|---+
                                                                     |
                         +----------- finwallet-data network ----------+
                         |                                           |
                         |   MSSQL                 Redis              |
                         |     |                     |                |
                         |     v                     v                |
                         | finwallet_mssql_data  finwallet_redis_data|
                         +-------------------------------------------+
```

Under normal Compose usage only Gateway `8080` is published to the host. Backend APIs, MSSQL and Redis remain inside Docker networks. The debug overlay exposes backend ports only through `127.0.0.1` when needed.

### 17. Docker persistent-volume topology

```text
mssql container
  +--> finwallet_mssql_data   : database data/log
  +--> finwallet_mssql_backup : backup files

redis container
  +--> finwallet_redis_data   : AOF/RDB persistence

application containers
  +--> NO named persistent application volume
       stdout/stderr -> Docker log driver
```

`docker compose down` preserves volumes. `docker compose down -v` deletes local MSSQL and Redis persistence.

### 18. Scaling boundaries
Gateway, FinWallet.Api and stateless provider HTTP services can be replicated. Financial correctness must not depend on the number of replicas; MSSQL constraints, transactions and idempotency rules provide multi-instance correctness.

Redis is not used as a distributed financial lock. If an application instance disappears, committed financial state remains in MSSQL.

### 19. Financial-consistency summary

```text
External HTTP
   |
   | SQL transaction is NOT open
   v
Provider result
   |
   v
Short MSSQL transaction
   |
   +--> Wallet / BlockedBalance
   +--> FinancialTransaction
   +--> Ledger
   +--> Idempotency
   +--> Outbox
   v
COMMIT
```

This boundary minimizes the need for distributed transactions and prevents partial financial commits.

### 20. References for tracing one request
End-to-end troubleshooting correlates the following references:

```text
X-Correlation-Id
CustomerId
SessionId
TransactionId
BankReference / ExternalTransactionId
FraudReference
CampaignReference
CutoffReference
Outbox / Inbox message id
```

Tokens, OTPs, passwords, connection strings and other secrets are never logged.
