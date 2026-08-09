# FinWallet API Rehberi / API Guide

## Türkçe

### Public giriş noktası
Normal client yalnız Gateway'i çağırır:
```text
http://localhost:8080
```
Ana business prefix: `/api/v1`.

Anonymous Gateway rotaları:
- `POST /api/v1/auth/register`
- `POST /api/v1/auth/registration/verify`
- `POST /api/v1/auth/login`
- `POST /api/v1/auth/refresh`

Diğer `/api/*` rotaları Gateway'de JWT ister. FinWallet.Api JWT/authorization/ownership kontrolünü ayrıca tekrar yapar.

### Ortak HTTP contract
Tüm success/failure body'leri `ServiceResult<T>` kullanır. Client logic sırası:
1. HTTP status;
2. stabil `code`.

Human-readable `message` business logic için parse edilmemelidir.

### Correlation
Client `X-Correlation-Id` gönderebilir. Shared web platform yalnız bounded alphanumeric/`-`/`_` formatını kabul eder; geçersiz veya eksik değer yerine yeni ID üretir. Correlation ID transaction veya idempotency key değildir ve PII içermemelidir.

### Authentication
Protected endpoint:
```http
Authorization: Bearer <access-token>
```
JWT Gateway ve FinWallet.Api'de doğrulanır. Para değiştiren transfer akışı ayrıca JWT `sid` değerini durable CustomerSession ile kontrol eder.

### Swagger
Development:
```text
Gateway             http://localhost:8080/swagger
FinWallet.Api       http://localhost:8081/swagger
FakeBank.Api        http://localhost:8082/swagger
FakeFraud.Api       http://localhost:8083/swagger
FakeCutoff.Api      http://localhost:8084/swagger
FakeCampaign.Api    http://localhost:8085/swagger
FakeCommunication   http://localhost:8086/swagger
```
Production'da Swagger varsayılan kapalıdır.

### Auth endpointleri
**Register** — `POST /api/v1/auth/register`  
Pending customer oluşturur ve OTP'yi FakeCommunication'a gönderir. Başarı: HTTP 202.

**Verify** — `POST /api/v1/auth/registration/verify`  
OTP verify+consume yapar ve customer'ı aktive eder.

**Login** — `POST /api/v1/auth/login`  
Credential doğrular, durable session, access token ve refresh token üretir.

**Refresh** — `POST /api/v1/auth/refresh`  
Opaque refresh token rotation yapar; token single-use'dur ve reuse detection session family revoke eder.

**Logout** — henüz public endpoint olarak uygulanmadı.

### Wallet endpointleri
**POST `/api/v1/wallets`** — JWT gerekir.
```json
{ "currency": "TRY" }
```
Desteklenen currency: `TRY`, `USD`, `EUR`. İlk create 201, aynı customer/currency tekrarında mevcut wallet 200 döner. Yeni wallet sıfır available/blocked balance ile başlar.

**GET `/api/v1/wallets`** — JWT subject'e ait wallet'ları listeler.

### Bank account
**POST `/api/v1/bank-accounts`** — JWT gerekir.
```json
{ "walletId": "<wallet-guid>" }
```
Akış:
```text
Gateway JWT
-> API JWT + ownership
-> durable BankAccount(Opening)
-> SQL tamamlanır
-> Gateway /providers/bank/*
-> FakeBank
-> provider result validation
-> CAS state update
```
External HTTP açık SQL transaction içinde çalışmaz.

### Wallet transfer
**POST `/api/v1/transfers`** gerekir:
- JWT;
- active durable session;
- `Idempotency-Key`;
- owned/active source wallet;
- distinct active destination;
- aynı currency;
- positive/bounded amount;
- final fraud Allow;
- yeterli bakiye.

```http
POST /api/v1/transfers
Authorization: Bearer <JWT>
Idempotency-Key: transfer-000001
Content-Type: application/json
```
```json
{
  "sourceWalletId": "aaaaaaaa-1111-4111-8111-111111111111",
  "destinationWalletId": "bbbbbbbb-2222-4222-8222-222222222222",
  "amount": 125.50
}
```

Sıra:
```text
completed replay
-> session/risk
-> internal fraud
-> FakeFraud via Gateway
-> combined decision
-> atomic MSSQL posting
```

Completed replay aynı immutable transaction'ı döndürür; ikinci fraud evaluation veya ikinci money movement yapılmaz.

### Funding durumu
Public BankDeposit endpoint'i henüz yoktur. Yeni wallet 0 bakiye ile açıldığı için register'dan sonra public endpointlerle kaynak wallet'ı fonlayıp transfer etmek mümkün değildir. Test fixture doğrudan balance UPDATE etmemeli; balanced FinancialTransaction + LedgerJournal/Entries oluşturmalıdır.

### Internal/provider rotaları
```text
/providers/bank/*
/providers/fraud/*
/providers/cutoff/*
/providers/campaign/*
/providers/communication/*
```
Bunlar public client API değildir. Gateway internal-service authorization ister; destination business endpoint ayrıca downstream service key doğrular.

### Platform hata örnekleri
- JWT yok/geçersiz: 401 `GATEWAY_UNAUTHORIZED`.
- Direct backend without downstream key: 401 `INTERNAL_SERVICE_UNAUTHORIZED`.
- Rate limit: 429 `RATE_LIMITED`.
- Unsupported write content type: 415.
- TRACE/CONNECT: 405.
- Body/header limit aşımı: business processing başlamadan reddedilir.

---

## English

### Public entry point
Normal clients call only the Gateway:
```text
http://localhost:8080
```
Main business prefix: `/api/v1`.

Anonymous Gateway routes:
- `POST /api/v1/auth/register`
- `POST /api/v1/auth/registration/verify`
- `POST /api/v1/auth/login`
- `POST /api/v1/auth/refresh`

Other `/api/*` routes require JWT at the Gateway. FinWallet.Api independently repeats JWT, authorization and ownership checks.

### Common HTTP contract
All success/failure bodies use `ServiceResult<T>`. Client logic should branch on:
1. HTTP status;
2. stable `code`.

Human-readable `message` text must not be parsed for business logic.

### Correlation
Clients may send `X-Correlation-Id`. The shared web platform accepts only bounded alphanumeric/`-`/`_` values; invalid or absent values are replaced. Correlation IDs are not transaction or idempotency keys and must not contain PII.

### Authentication
Protected endpoints use:
```http
Authorization: Bearer <access-token>
```
JWT is validated at both Gateway and FinWallet.Api. Money-changing transfer also validates JWT `sid` against durable CustomerSession state.

### Swagger
Development:
```text
Gateway             http://localhost:8080/swagger
FinWallet.Api       http://localhost:8081/swagger
FakeBank.Api        http://localhost:8082/swagger
FakeFraud.Api       http://localhost:8083/swagger
FakeCutoff.Api      http://localhost:8084/swagger
FakeCampaign.Api    http://localhost:8085/swagger
FakeCommunication   http://localhost:8086/swagger
```
Swagger is disabled by default in production.

### Authentication endpoints
**Register** — `POST /api/v1/auth/register`  
Creates a pending customer and sends OTP through FakeCommunication. Success: HTTP 202.

**Verify** — `POST /api/v1/auth/registration/verify`  
Verifies/consumes OTP and activates the customer.

**Login** — `POST /api/v1/auth/login`  
Validates credentials and creates durable session, access token and refresh token.

**Refresh** — `POST /api/v1/auth/refresh`  
Rotates an opaque single-use refresh token; reuse detection revokes the session family.

**Logout** — not yet implemented as a public endpoint.

### Wallet endpoints
**POST `/api/v1/wallets`** — requires JWT.
```json
{ "currency": "TRY" }
```
Supported currencies: `TRY`, `USD`, `EUR`. First create returns 201; repeating the same customer/currency returns the existing wallet with 200. New wallets start with zero available and blocked balance.

**GET `/api/v1/wallets`** — lists wallets owned by the JWT subject.

### Bank account
**POST `/api/v1/bank-accounts`** — requires JWT.
```json
{ "walletId": "<wallet-guid>" }
```
Flow:
```text
Gateway JWT
-> API JWT + ownership
-> durable BankAccount(Opening)
-> SQL completes
-> Gateway /providers/bank/*
-> FakeBank
-> provider result validation
-> CAS state update
```
External HTTP never runs inside an open SQL transaction.

### Wallet transfer
**POST `/api/v1/transfers`** requires:
- JWT;
- active durable session;
- `Idempotency-Key`;
- owned/active source wallet;
- distinct active destination;
- same currency;
- positive/bounded amount;
- final fraud Allow;
- sufficient balance.

```http
POST /api/v1/transfers
Authorization: Bearer <JWT>
Idempotency-Key: transfer-000001
Content-Type: application/json
```
```json
{
  "sourceWalletId": "aaaaaaaa-1111-4111-8111-111111111111",
  "destinationWalletId": "bbbbbbbb-2222-4222-8222-222222222222",
  "amount": 125.50
}
```

Order:
```text
completed replay
-> session/risk
-> internal fraud
-> FakeFraud via Gateway
-> combined decision
-> atomic MSSQL posting
```

A completed replay returns the same immutable transaction without a second fraud evaluation or money movement.

### Funding status
No public BankDeposit endpoint exists yet. Because a new wallet starts at zero, the source wallet cannot currently be funded after registration using public endpoints alone. Test fixtures must create a balanced FinancialTransaction + LedgerJournal/Entries rather than directly updating the wallet balance.

### Internal/provider routes
```text
/providers/bank/*
/providers/fraud/*
/providers/cutoff/*
/providers/campaign/*
/providers/communication/*
```
These are not public client APIs. Gateway requires internal-service authorization and destination business endpoints additionally validate the downstream service key.

### Platform failure examples
- missing/invalid JWT: 401 `GATEWAY_UNAUTHORIZED`;
- direct backend without downstream key: 401 `INTERNAL_SERVICE_UNAUTHORIZED`;
- rate limit: 429 `RATE_LIMITED`;
- unsupported write content type: 415;
- TRACE/CONNECT: 405;
- body/header limits: rejected before business processing.
