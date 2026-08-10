# FinWallet Teknik ve Finansal Sözlük / FinWallet Technical and Financial Glossary

## Türkçe

## Amaç

Bu belge FinWallet projesinin mimari tasarımında, teknik tasarımında, kaynak kodunda ve 00-25 numaralı proje dokümanlarında kullanılan ana teknik ve finansal terimlerin kapsamlı sözlüğüdür.

Her terim dört soruya cevap verir:
- Tanım: Terim genel olarak nedir?
- FinWallet'ta kullanım: Projede nerede ve nasıl kullanıldı?
- Neden: Hangi problemi çözmek için seçildi?
- Not / trade-off: Yanlış anlaşılabilecek nokta, maliyet veya alternatif nedir?

Bu sözlük 314 terim içerir. "Kullanılmayan" bölümündeki terimler de dokümanlarda mimari karar gerekçesi olarak geçtiği için açıklanmıştır.

## Hızlı Mimari ve Teknik Karar Haritası

| Seçim / Teknik | FinWallet'ta ne için kullanıldı? | Neden seçildi? |
|---|---|---|
| Modular Monolith | Finansal çekirdeği tek deploy/DB transaction sınırında tutmak | Wallet/Ledger/Transaction atomikliği; distributed transaction maliyetinden kaçınma |
| Layered + pragmatic Clean Architecture | HTTP, use-case, domain ve infrastructure sorumluluklarını ayırmak | Kodun okunabilirliği, test edilebilirlik, teknoloji bağımsız domain |
| DDD-lite | Finansal dili doğrudan modele taşımak | Money, Wallet, Ledger, FraudDecision gibi iş kavramlarını doğru ifade etmek |
| Ports + Adapters / ACL | Fake/gerçek provider detaylarını Application/Domain'den izole etmek | Provider değişse bile core iş kurallarını korumak |
| YARP API Gateway | Tek public giriş, JWT, routing, rate limit, load balancing | Backend topology'yi gizlemek ve edge policy'leri merkezileştirmek |
| Defense in Depth | Gateway + API + internal service credential kontrollerini katmanlamak | Gateway bypass ve tek kontrol failure riskini azaltmak |
| MSSQL | Finansal durable source of truth olmak | ACID transaction, locking, constraints, audit/history |
| Redis | Sadece transient/TTL/counter state tutmak | OTP/fraud velocity gibi düşük-latency state'i hızlandırmak |
| Double-Entry Ledger | Her para hareketini debit/credit olarak audit etmek | Paranın kaynağını/hedefini açıklamak ve Debit=Credit invariant'ı |
| Wallet Current Balance | Hızlı current projection sağlamak | Her request'te tüm ledger'ı sum etmemek |
| Durable Idempotency | Retry/timeout'ta duplicate financial effect'i engellemek | Aynı request'in ikinci kez para hareketi üretmemesi |
| Outbox | DB commit ile notification/message kaydını atomik tutmak | Crash sonrası mesaj kaybını engellemek |
| Inbox | Provider callback duplicate'larını dedupe etmek | Aynı callback'in ikinci kez para hareketi üretmemesi |
| Internal + External Fraud | Server-side risk + provider risk kararını birleştirmek | Transfer/Purchase öncesi risk kontrolü |
| Manual Fraud Review | Şüpheli işlemi para hareketi olmadan bekletmek | Human-in-the-loop Allow/Deny |
| Cutoff / Business Calendar | Bank işlem tarihini iş gününe göre planlamak | After-hours/holiday bank withdrawal'larını doğru schedule etmek |
| Blocked Balance | Pending wallet->bank hareketinde parayı rezerve etmek | Aynı paranın iki kez harcanmasını engellemek |
| Compensation / Reversal / Refund | Committed hareketi silmeden karşıt işlem yapmak | Immutable audit trail ile düzeltme |
| Reconciliation | Local wallet/ledger/bank/provider kayıtlarını karşılaştırmak | Silent drift ve missing callback/mismatch tespiti |
| Keyset Pagination | Transaction history'yi büyük OFFSET olmadan okumak | Büyük tabloda daha stabil ve hızlı pagination |
| HttpClientFactory + connection pooling | Provider bağlantılarını sağlıklı reuse etmek | Socket exhaustion ve latency azaltmak |
| Structured Logging + Correlation ID | Dağıtık flow'u loglarda izlemek | Incident/troubleshooting ve latency analizi |
| Docker Compose | Tüm stack'i reproducible local/integration environment'ta çalıştırmak | Gateway + APIs + MSSQL + Redis + fake providers tek komutla |
| Named Volumes | MSSQL/Redis state'ini container recreate'den bağımsız tutmak | Local persistence ve backup kolaylığı |
| GitHub Actions CI | Build/test/Docker/PDF doğrulamasını otomatik yapmak | Regression'ı merge öncesi yakalamak |
| xUnit + Moq | Application davranışını hızlı unit test etmek | Business branch/failure interaction doğrulaması |
| Real Docker Smoke/Integration Tests | DI, config, SQL schema ve network'ü gerçek stack'te doğrulamak | 'Derleniyor ama çalışmıyor' problemini yakalamak |

## Temel Finansal Kavramlar - Kısa Örneklerle

### Wallet ile BankAccount neden ayrı?

```text
Başlangıç:
Müşterinin FakeBank hesabı : 5.000 TRY
FinWallet Wallet            :     0 TRY

Bankadan Wallet'a 1.000 TRY funding sonrası:
Müşterinin FakeBank hesabı : 4.000 TRY
FinWallet Wallet            : 1.000 TRY
```

Wallet, bankadaki aynı satırın kopyası değildir. BankAccount dış bankadaki hesabı; Wallet ise FinWallet içindeki müşteriye ait kullanılabilir bakiyeyi ve FinWallet'ın müşteriye karşı yükümlülüğünü temsil eder.

### Ledger nedir?

Ledger, para hareketlerinin "neden ve hangi hesaplar arasında" oluştuğunu debit/credit kayıtlarıyla açıklayan immutable muhasebe geçmişidir. Wallet tablosu bugün kullanılabilir bakiye kaç sorusuna hızlı cevap verir; Ledger bu bakiyenin nasıl oluştuğunu açıklar.

Bankadan Wallet'a 1.000 TRY örneği:

```text
Debit   BANK-SETTLEMENT:TRY               1.000
Credit  WALLET-LIABILITY:<walletId>       1.000
------------------------------------------------
Toplam Debit                              1.000
Toplam Credit                             1.000
```

Ekonomik anlam:
- Settlement Asset +1.000: FinWallet'ın bankacılık tarafındaki karşılık varlığı arttı.
- Customer Wallet Liability +1.000: FinWallet artık müşteriye 1.000 TRY wallet bakiyesi borçlu.

### Wallet-to-Wallet Transfer örneği

Ali'den Ayşe'ye 300 TRY:

```text
Debit   WALLET-LIABILITY:ALI              300
Credit  WALLET-LIABILITY:AYSE             300
------------------------------------------------
Debit = Credit
```

Source wallet liability debit edilerek azalır; destination liability credit edilerek artar. Banka çağrısı gerekmez, çünkü bu FinWallet içi book transfer'dır.

### Purchase + Platform Kampanyası örneği

Ürün 200 TRY, platform 20 TRY indirim sponsor ediyor, müşteri 180 TRY ödüyor:

```text
Debit   WALLET-LIABILITY:CUSTOMER         180
Debit   CAMPAIGN-EXPENSE                   20
Credit  MERCHANT-PAYABLE                  200
------------------------------------------------
Toplam Debit                              200
Toplam Credit                             200
```

Bu kayıt "20 TRY nereden geldi?" sorusunu açıkça cevaplar: platform expense olarak üstlendi.

### Refund / Reversal neden UPDATE değildir?

Original transaction veya journal silinmez/değiştirilmez. Yeni bir karşıt FinancialTransaction ve yeni bir journal oluşturulur.

```text
Original Purchase
        |
        +--> immutable history

Refund
        |
        +--> yeni transaction
        +--> original etkisini tersleyen yeni journal
```

Böylece audit trail korunur.

### Idempotency neden finansal sistemde kritiktir?

Client transfer request'ini gönderdi, server parayı hareket ettirdi fakat response network'te kayboldu. Client tekrar denerse aynı para ikinci kez hareket etmemelidir.

```text
Idempotency-Key: transfer-abc-001

1. request -> işlem Completed
2. aynı key + aynı payload -> eski sonuç replay
3. aynı key + farklı payload -> Conflict
```

### Outbox / Inbox neden var?

```text
Outbox:
Financial DB COMMIT
    |
    +--> Outbox message aynı transaction'da kaydedilir
    |
    +--> Worker daha sonra SMS/notification gönderir

Inbox:
Provider callback
    |
    +--> Source + MessageId ile dedupe
    |
    +--> aynı callback 100 kez gelse de finansal effect tekrar uygulanmaz
```

### Reconciliation neden otomatik düzeltme yapmıyor?

Reconciliation bir "fark bulma" mekanizmasıdır, sessiz bir balance overwrite mekanizması değildir.

```text
Wallet current balance
        vs
Ledger-derived balance

FinWallet bank movement
        vs
FakeBank statement
```

Fark varsa ReconciliationIssue oluşturulur. Para geçmişi otomatik değiştirilmez.

## Sözlük

## 1. Mimari ve Tasarım (32 terim)

### Aggregate
- Tanım: Birlikte tutarlı kalması gereken entity/value object grubunun transaction sınırını temsil eden DDD kavramıdır.
- FinWallet'ta kullanım: FinWallet ağır aggregate framework'ü kullanmaz; ancak Wallet/Transaction/Ledger posting kuralları aynı consistency boundary içinde ele alınır.
- Neden / çözdüğü problem: Hangi state'in birlikte atomik değişmesi gerektiğini düşünmeye yardımcı olur.
- Not / trade-off: FinWallet'ta aggregate kavramı hafif uygulanır; her tablo ayrı aggregate değildir.

### Aggregate Root
- Tanım: Bir aggregate'e dışarıdan erişilen ana entity'dir.
- FinWallet'ta kullanım: FinWallet'ta bu terim yoğun framework kuralı olarak uygulanmaz; örneğin Wallet davranışı kendi kimliği ve kuralları etrafında korunur.
- Neden / çözdüğü problem: Aggregate içindeki kurallara kontrollü giriş sağlar.
- Not / trade-off: Projede resmi aggregate-root hierarchy kurulmadığı için 'hafif kullanılan' bir terimdir.

### Anti-Corruption Layer (ACL)
- Tanım: Dış sistemin modelini iç domain modelinden ayıran dönüştürme katmanıdır.
- FinWallet'ta kullanım: Bank, Fraud, Cutoff ve Campaign provider response'ları Infrastructure'da FinWallet tiplerine dönüştürülür.
- Neden / çözdüğü problem: Dış sistemin isimlendirme, enum ve hata davranışının domain'i kirletmesini önler.
- Not / trade-off: Ek mapping kodu üretir ama entegrasyon bağımlılığını ciddi azaltır.

### Append-Only
- Tanım: Yeni history kayıtlarının eklendiği, eski kayıtların overwrite/delete edilmediği veri modeli yaklaşımıdır.
- FinWallet'ta kullanım: Ledger journal/entry ve correction history bu prensibe yakındır.
- Neden / çözdüğü problem: Audit trail ve finansal geçmişin sonradan değiştirilmemesini sağlar.
- Not / trade-off: Current-state tabloları append-only olmak zorunda değildir; history ve projection rolleri ayrıdır.

### Architecture and Design?