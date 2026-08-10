# FinWallet Error Code Kataloğu ve Açıklamaları / FinWallet Error Code Catalog and Descriptions

## Türkçe

Bu doküman FinWallet, YARP Gateway, ortak Web Platform ve Fake provider API'lerinde kullanılan önemli machine-readable hata kodlarını toplar. Public HTTP response kodları ile yalnız durable/background state içinde kullanılan failure code'lar ayrı bölümlerde gösterilir.

Tüm public hata response'ları genel olarak şu envelope biçimindedir:

```json
{
  "isSuccess": false,
  "code": "ERROR_CODE",
  "message": "Safe client message.",
  "data": null,
  "errors": []
}
```

### 1. Gateway ve ortak Web Platform

| Code | HTTP | Kaynak | Türkçe açıklama |
|---|---:|---|---|
| GATEWAY_UNAUTHORIZED | 401 | YARP Gateway | Gateway korumalı route için geçerli JWT yok veya token doğrulanamadı. |
| GATEWAY_FORBIDDEN | 403 | YARP Gateway | Kimlik doğrulanmış olsa da Gateway authorization policy isteği reddetti. |
| RATE_LIMITED | 429 | Shared.Web | IP partition fixed-window rate limit aşıldı. `Retry-After` header dönebilir. |
| METHOD_NOT_ALLOWED | 405 | Shared.Web | TRACE veya CONNECT gibi yasaklanmış HTTP method kullanıldı. |
| INTERNAL_SERVICE_UNAUTHORIZED | 401 | Shared.Web | Backend/fake provider business endpoint'i için geçerli downstream internal service key yok. |
| UNSUPPORTED_MEDIA_TYPE | 415 | Shared.Web | Body içeren POST/PUT/PATCH isteği `application/json` değil. |
| UNAUTHORIZED | 401 | FinWallet.Api JWT | Backend katmanında geçerli access token gerekli. Gateway bypass/defense-in-depth kontrolüdür. |
| FORBIDDEN | 403 | FinWallet.Api JWT | Authenticated customer ilgili backend operasyonu için yetkili değil. |
| INVALID_ACCESS_TOKEN | 401 | Public controllers | JWT doğrulanmış olsa bile `sub`/`sid` claim formatı FinWallet GUID identity sözleşmesine uymuyor. |
| INVALID_REQUEST | 400 | ApiExceptionHandler | Bir veya daha fazla request değeri application/domain doğrulamasından geçmedi. |
| DEPENDENCY_UNAVAILABLE | 503 | ApiExceptionHandler | Gerekli dış HTTP servisi genel network seviyesinde ulaşılamaz durumda. |
| UNEXPECTED_ERROR | 500 | ApiExceptionHandler | Beklenmeyen server-side exception oluştu; iç exception detayı client'a sızdırılmaz. |

### 2. Registration ve Authentication

| Code | HTTP | Türkçe açıklama |
|---|---:|---|
| REGISTRATION_NOT_ALLOWED | 400 | Country ve telefon prefix/format kombinasyonu registration policy'ye uygun değil. |
| REGISTRATION_CONFLICT | 409 | Aynı kimlik bilgileriyle mevcut/pending registration bulunuyor. |
| OTP_RESEND_RATE_LIMIT | 429 | Yeni OTP göndermek için resend cooldown henüz dolmadı. |
| INVALID_REGISTRATION_OTP | 400 | OTP yanlış, expired, deneme limiti nedeniyle geçersiz veya daha önce consume edilmiş. |
| AUTH_TEMPORARILY_LOCKED | 429 | Failed-login güvenlik kuralı nedeniyle credential geçici lock durumunda. |
| INVALID_CREDENTIALS | 401 | Telefon/parola kombinasyonu geçersiz. |
| REFRESH_TOKEN_REUSE_DETECTED | 401 | Daha önce kullanılmış refresh token tekrar kullanıldı; session revoke edilir. |
| INVALID_REFRESH_TOKEN | 401 | Refresh token geçersiz, expired, revoked veya artık kullanılamaz. |

### 3. Wallet ve BankAccount

| Code | HTTP | Türkçe açıklama |
|---|---:|---|
| UNSUPPORTED_CURRENCY | 400 | Public wallet create yalnız desteklenen TRY/USD/EUR currency değerlerini kabul eder. |
| WALLET_CONFLICT | 409 | Eşzamanlı wallet state değişimi veya create winner reload problemi oluştu; operasyon tekrar denenebilir. |
| WALLET_NOT_FOUND | 404 | BankAccount açılmak istenen wallet bulunamadı veya authenticated customer'a ait değil. Ownership leak önlemek için aynı 404 kullanılır. |
| BANK_ACCOUNT_CONFLICT | 409 | BankAccount state CAS/concurrency kontrolünde stale state tespit edildi. |
| BANK_ACCOUNT_UNAVAILABLE | 404 | Bank money movement için Active ve provider-linked BankAccount bulunamadı. |
| IDEMPOTENCY_KEY_REQUIRED | 400 | Money-changing endpoint zorunlu `Idempotency-Key` header olmadan çağrıldı. |
| IDEMPOTENCY_CONFLICT | 409 | Aynı idempotency key farklı financial payload ile tekrar kullanıldı. Transfer, Purchase, BankMovement ve Correction akışlarında görülebilir. |
| CURRENCY_MISMATCH | 400 | İşlemdeki wallet/account currency değerleri birbiriyle uyuşmuyor. |
| INSUFFICIENT_BALANCE | 409 | Kaynak wallet available balance istenen finansal hareket için yetersiz. |

### 4. WalletTransfer ve Fraud

| Code | HTTP | Türkçe açıklama |
|---|---:|---|
| TRANSFER_SESSION_INVALID | 401 | JWT `sid` ile bulunan durable customer session aktif değil, expired veya revoke edilmiş. |
| TRANSFER_FRAUD_DENIED | 403 | Internal/external fraud sonucu transfer Deny oldu; para hareketi yapılmadı. |
| TRANSFER_REVIEW_REQUIRED | 202 | Fraud sonucu Review oldu; durable FraudEvent Pending bırakıldı, para hareketi yapılmadı. |
| SOURCE_WALLET_NOT_FOUND | 404 | Source wallet bulunamadı veya authenticated customer'a ait değil. |
| DESTINATION_WALLET_NOT_FOUND | 404 | Destination wallet bulunamadı. |
| TRANSFER_IN_PROGRESS | 409 | Aynı transfer idempotency isteği başka bir caller tarafından halen işleniyor. |
| TRANSFER_UNAVAILABLE | 409 | Wallet lifecycle/state transfer işlemine uygun değil. |
| FRAUD_DEPENDENCY_UNAVAILABLE | 503 | Gerekli external fraud servisi timeout/network/invalid-response nedeniyle güvenilir karar üretemedi; işlem fail-closed durduruldu. |
| FRAUD_IDEMPOTENCY_CONFLICT | 409 | Aynı fraud-evaluated operation idempotency key farklı request hash ile kullanıldı. |
| FRAUD_REVIEW_NOT_FOUND | 404 | Internal manual-review için FraudEvent bulunamadı. |
| FRAUD_REVIEW_CONFLICT | 409 | FraudEvent artık Pending review state'inde değil; ikinci/uyumsuz review kararı uygulanamaz. |
| INVALID_PAGE_SIZE | 400 | Internal fraud-review `take` değeri 1-100 aralığında değil. |
| REVIEWER_REQUIRED | 400 | Internal review decision çağrısında `X-Reviewer-Id` header yok. |

### 5. Purchase ve Campaign

| Code | HTTP | Türkçe açıklama |
|---|---:|---|
| PURCHASE_SESSION_INVALID | 401 | Purchase için durable customer session geçersiz/revoked/expired. |
| PURCHASE_FRAUD_DENIED | 403 | Purchase fraud kontrolleri tarafından Deny edildi; para hareketi ve campaign posting yapılmadı. |
| PURCHASE_REVIEW_REQUIRED | 202 | Purchase ek/manual review gerektiriyor; para hareketi yapılmadı. |
| PURCHASE_UNAVAILABLE | 409 | Wallet veya merchant purchase için uygun/aktif değil. |
| FRAUD_DEPENDENCY_UNAVAILABLE | 503 | Purchase external fraud dependency güvenilir cevap üretemedi; fail-closed. |
| IDEMPOTENCY_CONFLICT | 409 | Aynı purchase key farklı merchant/wallet/amount request ile kullanıldı. |
| CAMPAIGN_PROVIDER_ERROR | 503 | Campaign adapter provider rejection için özel code alamadığında kullandığı fallback code. |
| CAMPAIGN_PROVIDER_TIMEOUT | 503 | Campaign provider timeout oldu. |
| CAMPAIGN_PROVIDER_NETWORK_ERROR | 503 | Campaign provider network seviyesinde ulaşılamaz. |
| CAMPAIGN_PROVIDER_INVALID_RESPONSE | 503 | Campaign provider JSON/currency/sponsor contract'ı geçersiz. |

### 6. Bank movement ve Bank callback

| Code | HTTP | Türkçe açıklama |
|---|---:|---|
| BANK_CALLBACK_CONFLICT | 409 | Aynı callback MessageId farklı payload ile tekrar kullanıldı. |
| BANK_CALLBACK_TRANSACTION_NOT_FOUND | 404 | External transaction id FinWallet durable bank movement state'inde bulunamadı. |
| INVALID_BANK_CALLBACK | 400 | Callback status değeri `Pending`, `Completed` veya `Failed` sözleşmesine uymuyor. |
| BANK_PROVIDER_ERROR | 502/503 | Bank adapter provider hata code'u okuyamazsa fallback olarak kullanır; retryability HTTP/provider durumuna bağlıdır. |
| BANK_PROVIDER_TIMEOUT | 503 | External bank provider izin verilen süre içinde cevap vermedi. |
| BANK_PROVIDER_NETWORK_ERROR | 503 | External bank provider network seviyesinde geçici olarak ulaşılamaz. |
| BANK_PROVIDER_INVALID_RESPONSE | 502 | Provider response JSON/enum/currency contract'ı desteklenmiyor veya bozuk. |
| BANK_PROVIDER_INVALID_ACCOUNT_STATE | 502 | Account opening sırasında provider identity/currency/final state internal BankAccount ile tutarsız. |

### 7. Refund ve Reversal

| Code | HTTP | Türkçe açıklama |
|---|---:|---|
| TRANSACTION_NOT_FOUND | 404 | Refund/Reversal için original FinancialTransaction bulunamadı. |
| CORRECTION_NOT_ALLOWED | 409 | Original transaction type/state requested correction için uygun değil; örneğin external bank operation public reversal ile düzeltilmez. |
| IDEMPOTENCY_CONFLICT | 409 | Correction idempotency key farklı original transaction veya correction payload ile kullanıldı. |

### 8. Reconciliation internal API

| Code | HTTP | Türkçe açıklama |
|---|---:|---|
| INVALID_RECONCILIATION_SCOPE | 400 | Scope `WalletLedger`, `BankSettlementLedger` veya `ExternalBankStatement` değil. |
| RECONCILIATION_RUN_NOT_FOUND | 404 | İstenen reconciliation run bulunamadı. |

### 9. FakeCommunication provider

| Code | HTTP | Türkçe açıklama |
|---|---:|---|
| INVALID_MESSAGE_REQUEST | 400 | Recipient, messageType, body veya correlation bilgisi geçersiz/boş. |
| FAKE_PROVIDER_UNAVAILABLE | 503 | `X-Fake-Mode=fail` ile communication provider failure simülasyonu aktif. |
| MESSAGE_NOT_FOUND | 404 | Development/test message inspection endpoint'inde message id bulunamadı. |

### 10. FakeFraud provider

| Code | HTTP | Türkçe açıklama |
|---|---:|---|
| INVALID_FRAUD_REQUEST | 422 | Fraud provider request identity/amount/velocity zorunlu alanları geçersiz. |
| FAKE_FRAUD_UNAVAILABLE | 503 | `X-Fake-Mode=fail` ile FakeFraud unavailable simülasyonu aktif. |

### 11. FakeBank provider

| Code | HTTP | Türkçe açıklama |
|---|---:|---|
| INVALID_BANK_ACCOUNT_REQUEST | 422 | External account opening request alanları geçersiz. |
| BANK_REQUEST_KEY_CONFLICT | 409 | Aynı provider request key farklı account-opening payload ile kullanıldı. |
| FAKE_BANK_UNAVAILABLE | 503 | `X-Fake-Mode=fail` ile FakeBank unavailable simülasyonu aktif. |
| BANK_ACCOUNT_NOT_FOUND | 404 | Provider account id bulunamadı. |
| INVALID_BANK_TRANSACTION_REQUEST | 422 | Provider money-movement request amount/currency/type/key bilgisi geçersiz. |
| BANK_TRANSACTION_CONFLICT | 409 | Provider account state, currency, balance veya request-key state işlemle çakışıyor. |
| BANK_TRANSACTION_FINALIZATION_CONFLICT | 409 | Pending provider transaction mevcut state'inde finalize edilemiyor. |
| BANK_TRANSACTION_NOT_FOUND | 404 | Provider transaction id bulunamadı. |

### 12. FakeCutoff ve FinWallet Cutoff adapter

| Code | HTTP | Türkçe açıklama |
|---|---:|---|
| CUTOFF_RULE_NOT_AVAILABLE | 422 provider / 503 FinWallet adapter | FakeCutoff supplied country/currency/transaction kombinasyonu için rule bulamadı. FinWallet adapter provider failure'ı dependency failure olarak yüzeye çıkarır. |
| FAKE_CUTOFF_UNAVAILABLE | 503 | FakeCutoff failure simülasyonu. |
| CUTOFF_PROVIDER_ERROR | 503 | Provider error code alınamadığında FinWallet adapter fallback code'u. |
| CUTOFF_PROVIDER_TIMEOUT | 503 | Cutoff provider timeout. |
| CUTOFF_PROVIDER_NETWORK_ERROR | 503 | Cutoff provider network seviyesinde ulaşılamaz. |
| CUTOFF_PROVIDER_INVALID_RESPONSE | 503 | Cutoff provider response JSON/contract geçersiz. |

### 13. FakeCampaign provider

| Code | HTTP | Türkçe açıklama |
|---|---:|---|
| INVALID_CAMPAIGN_REQUEST | 422 | Campaign evaluate request alanları provider sözleşmesine uygun değil. |
| FAKE_CAMPAIGN_UNAVAILABLE | 503 | FakeCampaign failure simülasyonu. |
| CAMPAIGN_PROVIDER_ERROR | 503 | Provider error code alınamadığında FinWallet adapter fallback code'u. |
| CAMPAIGN_PROVIDER_TIMEOUT | 503 | Campaign provider timeout. |
| CAMPAIGN_PROVIDER_NETWORK_ERROR | 503 | Campaign provider geçici network failure. |
| CAMPAIGN_PROVIDER_INVALID_RESPONSE | 503 | Campaign response currency/sponsor/JSON contract'ı geçersiz. |

### 14. Durable/background failure code'ları - HTTP error değildir

Aşağıdaki değerler client HTTP error response olmak zorunda değildir. MSSQL transaction detail, Outbox veya background retry state'inde operasyonel/audit amaçlı tutulabilir.

| Code | Katman | Türkçe açıklama |
|---|---|---|
| BANK_PROVIDER_FAILED | Bank movement persistence | Provider transaction terminal Failed olduğunda durable financial failure nedeni. |
| BANK_MOVEMENT_FAILED | Idempotency/result state | Bank movement terminal failure sonucu. |
| COMMUNICATION_UNAVAILABLE | Outbox worker | FakeCommunication HTTP çağrısı geçici olarak başarısız; mesaj reschedule edilir. |
| OUTBOX_DISPATCH_ERROR | Outbox worker | Beklenmeyen Outbox dispatch hatası; message bounded backoff ile retry edilir. |
| OUTBOX_RECIPIENT_UNAVAILABLE | Outbox worker | Customer için gönderilebilir telefon bulunamadı. |
| OUTBOX_INVALID_PAYLOAD | Outbox worker | Durable Outbox JSON beklenen CustomerId contract'ını taşımıyor. |

### 15. Aynı code'un farklı context'lerde kullanılması

`IDEMPOTENCY_CONFLICT`, `INVALID_ACCESS_TOKEN` ve `FRAUD_DEPENDENCY_UNAVAILABLE` birden fazla endpoint tarafından kullanılabilir. HTTP client yalnız code'a değil endpoint/operation context'ine de bakmalıdır. Güvenli `message` human-readable açıklamadır; programatik karar öncelikle `code` ve HTTP status üzerinden verilmelidir.

---

## English

This document catalogs the important machine-readable error codes used by FinWallet, YARP Gateway, the shared Web Platform and fake-provider APIs. Public HTTP response codes and durable/background failure codes are listed separately.

Public failures generally use this envelope:

```json
{
  "isSuccess": false,
  "code": "ERROR_CODE",
  "message": "Safe client message.",
  "data": null,
  "errors": []
}
```

### 1. Gateway and shared Web Platform

| Code | HTTP | Source | English description |
|---|---:|---|---|
| GATEWAY_UNAUTHORIZED | 401 | YARP Gateway | A protected Gateway route was called without a valid JWT or token validation failed. |
| GATEWAY_FORBIDDEN | 403 | YARP Gateway | Authentication succeeded but the Gateway authorization policy denied the request. |
| RATE_LIMITED | 429 | Shared.Web | The IP-partitioned fixed-window rate limit was exceeded. A `Retry-After` header may be returned. |
| METHOD_NOT_ALLOWED | 405 | Shared.Web | A blocked HTTP method such as TRACE or CONNECT was used. |
| INTERNAL_SERVICE_UNAUTHORIZED | 401 | Shared.Web | A backend/fake-provider business endpoint was called without a valid downstream internal-service key. |
| UNSUPPORTED_MEDIA_TYPE | 415 | Shared.Web | A POST/PUT/PATCH request with a body did not use `application/json`. |
| UNAUTHORIZED | 401 | FinWallet.Api JWT | Backend defense-in-depth authentication requires a valid access token. |
| FORBIDDEN | 403 | FinWallet.Api JWT | The authenticated customer is not allowed to perform the backend operation. |
| INVALID_ACCESS_TOKEN | 401 | Public controllers | JWT passed signature validation but `sub`/`sid` do not match the FinWallet GUID identity contract. |
| INVALID_REQUEST | 400 | ApiExceptionHandler | One or more request values failed application/domain validation. |
| DEPENDENCY_UNAVAILABLE | 503 | ApiExceptionHandler | A required external HTTP dependency is generally unreachable. |
| UNEXPECTED_ERROR | 500 | ApiExceptionHandler | An unexpected server exception occurred; internal exception details are not exposed. |

### 2. Registration and Authentication

| Code | HTTP | English description |
|---|---:|---|
| REGISTRATION_NOT_ALLOWED | 400 | Country and phone prefix/format combination is not eligible under registration policy. |
| REGISTRATION_CONFLICT | 409 | A current/pending registration already exists for the supplied identity. |
| OTP_RESEND_RATE_LIMIT | 429 | OTP resend cooldown has not expired yet. |
| INVALID_REGISTRATION_OTP | 400 | OTP is wrong, expired, exhausted or already consumed. |
| AUTH_TEMPORARILY_LOCKED | 429 | Credential is temporarily locked by the failed-login policy. |
| INVALID_CREDENTIALS | 401 | Phone/password credentials are invalid. |
| REFRESH_TOKEN_REUSE_DETECTED | 401 | A previously consumed refresh token was reused; the session is revoked. |
| INVALID_REFRESH_TOKEN | 401 | Refresh token is invalid, expired, revoked or no longer usable. |

### 3. Wallet and BankAccount

| Code | HTTP | English description |
|---|---:|---|
| UNSUPPORTED_CURRENCY | 400 | Public wallet creation only accepts supported TRY/USD/EUR currencies. |
| WALLET_CONFLICT | 409 | Concurrent wallet state/create-winner conflict occurred; the operation may be retried. |
| WALLET_NOT_FOUND | 404 | Wallet for BankAccount opening does not exist or is not owned by the authenticated customer. The same 404 prevents ownership leakage. |
| BANK_ACCOUNT_CONFLICT | 409 | BankAccount CAS/concurrency check detected stale state. |
| BANK_ACCOUNT_UNAVAILABLE | 404 | No Active provider-linked BankAccount is available for the requested bank movement. |
| IDEMPOTENCY_KEY_REQUIRED | 400 | A money-changing endpoint was called without the mandatory `Idempotency-Key` header. |
| IDEMPOTENCY_CONFLICT | 409 | The same idempotency key was reused with a different financial payload. It can occur in Transfer, Purchase, BankMovement and Correction flows. |
| CURRENCY_MISMATCH | 400 | Wallet/account currencies involved in the operation do not match. |
| INSUFFICIENT_BALANCE | 409 | Source wallet available balance is insufficient for the requested financial movement. |

### 4. WalletTransfer and Fraud

| Code | HTTP | English description |
|---|---:|---|
| TRANSFER_SESSION_INVALID | 401 | Durable session referenced by JWT `sid` is inactive, expired or revoked. |
| TRANSFER_FRAUD_DENIED | 403 | Internal/external fraud resulted in Deny; no money moved. |
| TRANSFER_REVIEW_REQUIRED | 202 | Fraud resulted in Review; a durable Pending FraudEvent exists and no money moved. |
| SOURCE_WALLET_NOT_FOUND | 404 | Source wallet does not exist or is not owned by the authenticated customer. |
| DESTINATION_WALLET_NOT_FOUND | 404 | Destination wallet does not exist. |
| TRANSFER_IN_PROGRESS | 409 | The same transfer idempotency request is still being processed by another caller. |
| TRANSFER_UNAVAILABLE | 409 | One or more wallet lifecycle states cannot process the transfer. |
| FRAUD_DEPENDENCY_UNAVAILABLE | 503 | Required external fraud service failed by timeout/network/invalid response; the operation stopped fail-closed. |
| FRAUD_IDEMPOTENCY_CONFLICT | 409 | Same fraud-evaluated operation idempotency key was used with a different request hash. |
| FRAUD_REVIEW_NOT_FOUND | 404 | FraudEvent requested for manual review was not found. |
| FRAUD_REVIEW_CONFLICT | 409 | FraudEvent is no longer Pending and cannot receive another/incompatible review decision. |
| INVALID_PAGE_SIZE | 400 | Internal fraud-review `take` is outside 1-100. |
| REVIEWER_REQUIRED | 400 | Internal review decision is missing `X-Reviewer-Id`. |

### 5. Purchase and Campaign

| Code | HTTP | English description |
|---|---:|---|
| PURCHASE_SESSION_INVALID | 401 | Durable customer session for Purchase is invalid/revoked/expired. |
| PURCHASE_FRAUD_DENIED | 403 | Purchase was denied by fraud; no money/campaign posting occurred. |
| PURCHASE_REVIEW_REQUIRED | 202 | Purchase requires additional/manual review; no money moved. |
| PURCHASE_UNAVAILABLE | 409 | Wallet or merchant is not available/active for purchase. |
| FRAUD_DEPENDENCY_UNAVAILABLE | 503 | Purchase external-fraud dependency could not produce a trusted decision; fail-closed. |
| IDEMPOTENCY_CONFLICT | 409 | Same purchase key was reused with a different merchant/wallet/amount request. |
| CAMPAIGN_PROVIDER_ERROR | 503 | Fallback code when the campaign adapter cannot obtain a specific provider failure code. |
| CAMPAIGN_PROVIDER_TIMEOUT | 503 | Campaign provider timed out. |
| CAMPAIGN_PROVIDER_NETWORK_ERROR | 503 | Campaign provider is unreachable at the network level. |
| CAMPAIGN_PROVIDER_INVALID_RESPONSE | 503 | Campaign provider returned invalid JSON/currency/sponsor contract data. |

### 6. Bank movement and Bank callback

| Code | HTTP | English description |
|---|---:|---|
| BANK_CALLBACK_CONFLICT | 409 | Same callback MessageId was reused with a different payload. |
| BANK_CALLBACK_TRANSACTION_NOT_FOUND | 404 | External transaction id is not known in durable FinWallet bank-movement state. |
| INVALID_BANK_CALLBACK | 400 | Callback status does not match `Pending`, `Completed` or `Failed`. |
| BANK_PROVIDER_ERROR | 502/503 | Bank adapter fallback when no provider-specific code is available; retryability depends on HTTP/provider state. |
| BANK_PROVIDER_TIMEOUT | 503 | External bank provider did not respond within the configured timeout. |
| BANK_PROVIDER_NETWORK_ERROR | 503 | External bank provider is temporarily unreachable at network level. |
| BANK_PROVIDER_INVALID_RESPONSE | 502 | Provider response JSON/enum/currency contract is invalid or unsupported. |
| BANK_PROVIDER_INVALID_ACCOUNT_STATE | 502 | Account-opening provider identity/currency/final state conflicts with internal BankAccount state. |

### 7. Refund and Reversal

| Code | HTTP | English description |
|---|---:|---|
| TRANSACTION_NOT_FOUND | 404 | Original FinancialTransaction for Refund/Reversal was not found. |
| CORRECTION_NOT_ALLOWED | 409 | Original type/state does not permit the requested correction; for example external-bank operations are not reversed by the public internal-wallet reversal endpoint. |
| IDEMPOTENCY_CONFLICT | 409 | Correction key was reused with a different original transaction or correction payload. |

### 8. Reconciliation internal API

| Code | HTTP | English description |
|---|---:|---|
| INVALID_RECONCILIATION_SCOPE | 400 | Scope is not `WalletLedger`, `BankSettlementLedger` or `ExternalBankStatement`. |
| RECONCILIATION_RUN_NOT_FOUND | 404 | Requested reconciliation run does not exist. |

### 9. FakeCommunication provider

| Code | HTTP | English description |
|---|---:|---|
| INVALID_MESSAGE_REQUEST | 400 | Recipient, messageType, body or correlation data is missing/invalid. |
| FAKE_PROVIDER_UNAVAILABLE | 503 | FakeCommunication failure simulation is active through `X-Fake-Mode=fail`. |
| MESSAGE_NOT_FOUND | 404 | Development/test message-inspection endpoint cannot find the message id. |

### 10. FakeFraud provider

| Code | HTTP | English description |
|---|---:|---|
| INVALID_FRAUD_REQUEST | 422 | Required fraud identity/amount/velocity fields are invalid. |
| FAKE_FRAUD_UNAVAILABLE | 503 | FakeFraud unavailable simulation is active through `X-Fake-Mode=fail`. |

### 11. FakeBank provider

| Code | HTTP | English description |
|---|---:|---|
| INVALID_BANK_ACCOUNT_REQUEST | 422 | External account-opening fields are invalid. |
| BANK_REQUEST_KEY_CONFLICT | 409 | Same provider request key was reused with a different account-opening payload. |
| FAKE_BANK_UNAVAILABLE | 503 | FakeBank unavailable simulation is active through `X-Fake-Mode=fail`. |
| BANK_ACCOUNT_NOT_FOUND | 404 | Provider account id does not exist. |
| INVALID_BANK_TRANSACTION_REQUEST | 422 | Provider money-movement amount/currency/type/key data is invalid. |
| BANK_TRANSACTION_CONFLICT | 409 | Provider account state, currency, balance or request-key state conflicts with the transaction. |
| BANK_TRANSACTION_FINALIZATION_CONFLICT | 409 | Pending provider transaction cannot be finalized in its current state. |
| BANK_TRANSACTION_NOT_FOUND | 404 | Provider transaction id does not exist. |

### 12. FakeCutoff and FinWallet Cutoff adapter

| Code | HTTP | English description |
|---|---:|---|
| CUTOFF_RULE_NOT_AVAILABLE | 422 provider / 503 FinWallet adapter | FakeCutoff has no rule for the supplied country/currency/transaction combination. FinWallet surfaces provider failure as a dependency failure. |
| FAKE_CUTOFF_UNAVAILABLE | 503 | FakeCutoff failure simulation. |
| CUTOFF_PROVIDER_ERROR | 503 | FinWallet adapter fallback when no provider error code is available. |
| CUTOFF_PROVIDER_TIMEOUT | 503 | Cutoff provider timed out. |
| CUTOFF_PROVIDER_NETWORK_ERROR | 503 | Cutoff provider is unreachable at network level. |
| CUTOFF_PROVIDER_INVALID_RESPONSE | 503 | Cutoff provider JSON/contract is invalid. |

### 13. FakeCampaign provider

| Code | HTTP | English description |
|---|---:|---|
| INVALID_CAMPAIGN_REQUEST | 422 | Campaign-evaluation request does not satisfy provider contract. |
| FAKE_CAMPAIGN_UNAVAILABLE | 503 | FakeCampaign failure simulation. |
| CAMPAIGN_PROVIDER_ERROR | 503 | FinWallet adapter fallback when no provider error code is available. |
| CAMPAIGN_PROVIDER_TIMEOUT | 503 | Campaign provider timed out. |
| CAMPAIGN_PROVIDER_NETWORK_ERROR | 503 | Campaign provider has a temporary network failure. |
| CAMPAIGN_PROVIDER_INVALID_RESPONSE | 503 | Campaign response currency/sponsor/JSON contract is invalid. |

### 14. Durable/background failure codes - not necessarily HTTP errors

The following values may be stored in MSSQL transaction details, Outbox or background retry state and are not necessarily client HTTP error responses.

| Code | Layer | English description |
|---|---|---|
| BANK_PROVIDER_FAILED | Bank movement persistence | Durable financial failure reason when provider transaction reaches terminal Failed. |
| BANK_MOVEMENT_FAILED | Idempotency/result state | Terminal bank-movement result code. |
| COMMUNICATION_UNAVAILABLE | Outbox worker | FakeCommunication HTTP call failed temporarily; message is rescheduled. |
| OUTBOX_DISPATCH_ERROR | Outbox worker | Unexpected dispatch error; message is retried with bounded backoff. |
| OUTBOX_RECIPIENT_UNAVAILABLE | Outbox worker | No deliverable customer phone could be found. |
| OUTBOX_INVALID_PAYLOAD | Outbox worker | Durable Outbox JSON does not contain the expected CustomerId contract. |

### 15. Codes reused across contexts

`IDEMPOTENCY_CONFLICT`, `INVALID_ACCESS_TOKEN` and `FRAUD_DEPENDENCY_UNAVAILABLE` are intentionally reused by multiple endpoints. An HTTP client should evaluate code together with endpoint/operation context. The safe `message` is human-readable; programmatic behavior should primarily use `code` plus HTTP status.
