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

### Adapter
- Tanım: Bir port'u belirli teknoloji/protokole bağlayan concrete implementasyondur.
- FinWallet'ta kullanım: FakeBank HTTP client adapter'ı `IBankProvider`'ı REST çağrılarına map eder.
- Neden / çözdüğü problem: Provider sözleşmesi değişse bile Application'ın değişmemesini sağlar.
- Not / trade-off: Adapter içinde validation ve DTO mapping yapılmalıdır; domain'e provider tipi taşınmamalıdır.

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

### Audit Trail
- Tanım: Bir business/financial state'in hangi olaylar ve kimliklerle oluştuğunu geriye dönük izleyebilme kaydıdır.
- FinWallet'ta kullanım: FinancialTransaction, Ledger, FraudEvent, reconciliation ve review metadata birlikte audit trail oluşturur.
- Neden / çözdüğü problem: Incident, dispute ve finansal investigation sırasında açıklanabilirlik sağlar.
- Not / trade-off: Audit log ile application debug log aynı şey değildir; audit kayıtları daha güçlü retention/integrity gerektirebilir.

### Bounded Context
- Tanım: Aynı kelimelerin aynı anlamı taşıdığı iş alanı sınırıdır.
- FinWallet'ta kullanım: FinWallet finansal çekirdeği ile FakeBank/FakeFraud/FakeCampaign gibi dış provider modelleri ayrı anlam sınırları olarak ele alınır.
- Neden / çözdüğü problem: Provider DTO'larının doğrudan domain modeline sızmasını engeller.
- Not / trade-off: V1'de bunlar ayrı microservice bounded-context organizasyonu değil; kavramsal sınırlar ve adapter'larla korunur.

### Clean Architecture
- Tanım: Bağımlılıkların dış katmanlardan içteki iş kurallarına doğru aktığı mimari prensipler bütünüdür.
- FinWallet'ta kullanım: Domain hiçbir framework/SQL/HTTP detayını bilmez; Application interface'leri tanımlar, Infrastructure bunları uygular.
- Neden / çözdüğü problem: İş kurallarını teknolojiden bağımsız tutar ve test edilebilirliği artırır.
- Not / trade-off: FinWallet tam akademik Clean Architecture değil; sadeleştirilmiş/pragmatik bir uygulamadır.

### Consistency Boundary
- Tanım: Bir işlemde birlikte doğru kalması gereken verilerin sınırıdır.
- FinWallet'ta kullanım: Wallet balance, FinancialTransaction, Ledger, durable idempotency ve Outbox gerektiğinde aynı MSSQL transaction'a alınır.
- Neden / çözdüğü problem: Bir kısmı commit olup diğer kısmı kalırsa oluşacak finansal tutarsızlığı önler.
- Not / trade-off: External provider state bu sınırın dışındadır; o nedenle compensation/reconciliation gerekir.

### DDD-lite
- Tanım: DDD'nin değerli modelleme fikirlerini kullanıp ağır süreç ve framework katmanlarını eklemeyen pragmatik uygulamadır.
- FinWallet'ta kullanım: Value Object, entity, invariant ve domain isimlendirmeleri kullanılır; tam Event Sourcing veya kapsamlı domain event bus kullanılmaz.
- Neden / çözdüğü problem: Finansal domain'i anlaşılır tutarken over-engineering'i sınırlar.
- Not / trade-off: Daha karmaşık domainlerde ileride daha güçlü DDD araçları gerekebilir.

### Defense in Depth
- Tanım: Tek bir güvenlik kontrolüne güvenmek yerine birden fazla katmanda kontrol uygulanmasıdır.
- FinWallet'ta kullanım: Gateway JWT kontrolü yaparken FinWallet.Api de JWT/session/ownership ve service-key kontrolü yapar.
- Neden / çözdüğü problem: Bir katman bypass edilse bile sonraki katmanın isteği durdurmasını sağlar.
- Not / trade-off: Aynı kontrolü körlemesine kopyalamak değil, farklı tehdit seviyelerine uygun katmanlar kurmaktır.

### Dependency Injection (DI)
- Tanım: Bir sınıfın ihtiyaç duyduğu bağımlılıkların dışarıdan verilmesidir.
- FinWallet'ta kullanım: ASP.NET Core built-in DI ile handler, store, provider, TimeProvider ve background service'ler kaydedilir.
- Neden / çözdüğü problem: Bağımlılık oluşturma işini sınıflardan çıkarır; test ve runtime wiring'i yönetilebilir yapar.
- Not / trade-off: Eksik registration compile sırasında değil startup/ilk resolve sırasında hata verebilir; bu yüzden runtime smoke test önemlidir.

### Dependency Inversion Principle (DIP)
- Tanım: Üst seviye iş kurallarının alt seviye teknik detaylara değil soyutlamalara bağlı olması prensibidir.
- FinWallet'ta kullanım: Application `IBankProvider`, store ve fraud provider interface'lerine bağlıdır; Infrastructure concrete implementasyonları sağlar.
- Neden / çözdüğü problem: Fake provider ile gerçek provider değişimini ve unit test mock'larını kolaylaştırır.
- Not / trade-off: Her sınıf için gereksiz interface üretmek değildir; sadece gerçek boundary'lerde kullanılır.

### Dependency Rule
- Tanım: İç katmanların dış katmanları bilmemesi kuralıdır.
- FinWallet'ta kullanım: `Api -> Application -> Domain` ve `Infrastructure -> Application/Domain` yönü korunur.
- Neden / çözdüğü problem: Domain'in SQL, Redis veya provider DTO'larına bağlanmasını önler.
- Not / trade-off: Kural ihlal edilirse teknoloji değişikliği domain'i kırar.

### Domain-Driven Design (DDD)
- Tanım: Yazılım modelini iş alanındaki gerçek kavramlar ve kurallar etrafında kurma yaklaşımıdır.
- FinWallet'ta kullanım: Wallet, Money, Currency, FinancialTransaction, Ledger, FraudDecision gibi terimler doğrudan domain dilidir.
- Neden / çözdüğü problem: Kod ile finans/bankacılık dili arasındaki anlam farkını azaltır.
- Not / trade-off: FinWallet 'DDD-lite' kullanır; ağır aggregate/event-sourcing altyapısı kurulmamıştır.

### Entity
- Tanım: Kimliği olan ve zaman içinde state değiştirebilen domain nesnesidir.
- FinWallet'ta kullanım: Wallet, BankAccount ve FinancialTransaction kimlikleriyle takip edilen entity örnekleridir.
- Neden / çözdüğü problem: Aynı kimliğin yaşam döngüsü ve kurallarını tek modelde tutar.
- Not / trade-off: Entity eşitliği sadece property değerleriyle değil kimlikle ilgilidir.

### Handler
- Tanım: Bir use-case komutunu/query'sini orkestre eden Application sınıfıdır.
- FinWallet'ta kullanım: `ExecuteWalletTransferHandler`, `ExecuteFraudProtectedPurchaseHandler` gibi sınıflar bu rolü taşır.
- Neden / çözdüğü problem: Controller'ı ince tutar ve business flow'u HTTP'den ayırır.
- Not / trade-off: MediatR kullanılmadan doğrudan DI ile çağrılır.

### Horizontal Scaling
- Tanım: Aynı servisin daha fazla instance/replica çalıştırılarak kapasitesinin artırılmasıdır.
- FinWallet'ta kullanım: YARP cluster'larına FinWallet.Api veya provider replica'ları eklenebilir.
- Neden / çözdüğü problem: Tek instance bottleneck'ini azaltır.
- Not / trade-off: Correctness instance sayısına değil DB constraints/idempotency/concurrency kurallarına dayanmalıdır.

### Invariant
- Tanım: Sistemde hangi akış olursa olsun bozulmaması gereken iş kuralıdır.
- FinWallet'ta kullanım: `Debit == Credit`, negatif wallet bakiyesi yok, aynı correction iki kez uygulanamaz gibi kurallar invariant'tır.
- Neden / çözdüğü problem: Finansal doğruluğu sadece endpoint akışına değil veri/model seviyesine bağlar.
- Not / trade-off: Config ile açılıp kapanan seçenek değildir; değişmesi veri/muhasebe migrasyonu gerektirebilir.

### Layered Architecture
- Tanım: Sorumlulukların katmanlara ayrıldığı mimari yaklaşımdır.
- FinWallet'ta kullanım: FinWallet.Api, Application, Domain ve Infrastructure ayrı projelerdir.
- Neden / çözdüğü problem: HTTP, use-case, domain kuralı ve teknik altyapı kodunun birbirine karışmasını engeller.
- Not / trade-off: Katmanlar gereğinden fazla soyutlanırsa over-engineering oluşabilir; projede sade tutulmuştur.

### Modular Monolith
- Tanım: Tek deploy edilebilen uygulama içinde domain alanlarının modüllerle ayrıldığı mimari stildir.
- FinWallet'ta kullanım: FinWallet'ın finansal çekirdeği Wallet, BankAccount, Transaction, Ledger, Fraud, Reconciliation gibi modülleri aynı uygulama ve MSSQL transaction sınırı içinde tutar.
- Neden / çözdüğü problem: Para hareketlerinde dağıtık transaction ihtiyacını azaltır; domain sınırlarını korurken atomik commit'i kolaylaştırır.
- Not / trade-off: Microservice kadar bağımsız deploy sağlamaz; fakat v1 için finansal doğruluk ve sadelik daha değerlidir.

### Orchestration
- Tanım: Bir use-case içindeki adımların doğru sırada çalıştırılmasıdır.
- FinWallet'ta kullanım: Transfer handler replay -> fraud signals -> internal fraud -> external fraud -> durable fraud -> posting sırasını yönetir.
- Neden / çözdüğü problem: External I/O ile DB transaction sınırlarının doğru kurulmasını sağlar.
- Not / trade-off: Orchestrator'ın tüm business kurallarını kendi içine alması yerine kuralları Domain/Store'a dağıtmak gerekir.

### Port
- Tanım: Application katmanının dış dünya ile konuşmak için tanımladığı interface/boundary'dir.
- FinWallet'ta kullanım: `IBankProvider`, `IExternalFraudProvider`, store interface'leri port örnekleridir.
- Neden / çözdüğü problem: Application'ın hangi yeteneğe ihtiyaç duyduğunu belirtir, teknolojiyi belirtmez.
- Not / trade-off: Port'un implementasyonu Infrastructure'da tutulur.

### Projection
- Tanım: Immutable/history verisinden veya business state'ten okuma için türetilmiş current görünümüdür.
- FinWallet'ta kullanım: Wallet AvailableBalance/BlockedBalance current state'i ledger/history ile reconcile edilen bir projection gibi düşünülür.
- Neden / çözdüğü problem: Her request'te tüm ledger'ı sum etmek yerine hızlı current balance sağlar.
- Not / trade-off: Projection source history ile drift edebilir; reconciliation bu nedenle gereklidir.

### Separation of Concerns
- Tanım: Farklı sorumlulukların aynı sınıf veya katmanda birbirine karıştırılmaması prensibidir.
- FinWallet'ta kullanım: HTTP mapping Controller'da, orchestration Application'da, kural Domain'de, SQL/HTTP client Infrastructure'dadır.
- Neden / çözdüğü problem: Değişikliklerin etkilediği alanı küçültür ve okunabilirliği artırır.
- Not / trade-off: Aşırı parçalama da maliyetlidir; proje küçük sınırlar tercih eder.

### State Machine / Lifecycle
- Tanım: Bir entity/transaction'ın izin verilen durumlarını ve geçişlerini tanımlayan modeldir.
- FinWallet'ta kullanım: FinancialTransaction ve bank movement akışlarında Scheduled/Pending/Completed/Failed gibi durumlar vardır.
- Neden / çözdüğü problem: Geçersiz state transition'ları engeller ve retry/callback davranışını deterministik yapar.
- Not / trade-off: State isimleri API response değil domain yaşam döngüsüdür.

### Stateless Service
- Tanım: Process/container memory'sinde kalıcı iş gerçeği tutmayan servis yaklaşımıdır.
- FinWallet'ta kullanım: Gateway, FinWallet.Api ve fake HTTP servisleri replica olarak yeniden oluşturulabilir; durable finansal state MSSQL'dedir.
- Neden / çözdüğü problem: Horizontal scale ve container restart'ını güvenli hale getirir.
- Not / trade-off: Redis/cache geçici state içerebilir ancak finansal truth memory/container'a bağlanmaz.

### Topology
- Tanım: Servislerin, network yollarının, veri kaynaklarının ve güven sınırlarının çalışma zamanındaki yerleşimidir.
- FinWallet'ta kullanım: `25-topology.md` Gateway, API, fake provider, MSSQL, Redis, worker ve Docker network ilişkilerini gösterir.
- Neden / çözdüğü problem: Kod klasör yapısından farklı olarak gerçek trafik ve dependency yönünü anlamayı sağlar.
- Not / trade-off: Development ve production topology replica/edge bileşenleri açısından farklılaşabilir.

### Transaction Boundary
- Tanım: Bir database transaction'ın nerede başlayıp nerede bittiğini belirleyen tasarım kararıdır.
- FinWallet'ta kullanım: External HTTP çağrısı sırasında SQL transaction açık tutulmaz; provider sonucu alındıktan sonra kısa transaction açılır.
- Neden / çözdüğü problem: Lock süresini ve deadlock riskini azaltır, connection pool'u korur.
- Not / trade-off: Distributed atomicity sağlamaz; dış sistem farkları reconciliation ile ele alınır.

### Trust Boundary
- Tanım: Bir taraftan gelen verinin/kimliğin otomatik güvenilir kabul edilmediği güvenlik sınırıdır.
- FinWallet'ta kullanım: Client->Gateway, Gateway->FinWallet.Api, FinWallet.Api->Gateway provider route ve Gateway->provider ayrı trust boundary'lerdir.
- Neden / çözdüğü problem: Gateway bypass veya credential reuse gibi riskleri azaltır.
- Not / trade-off: Her boundary kendi doğrulamasını yapmalıdır.

### Use Case
- Tanım: Kullanıcının veya sistemin tek bir iş amacını gerçekleştiren uygulama davranışıdır.
- FinWallet'ta kullanım: Register, Login, CreateWallet, BankDeposit, Transfer, Purchase, Refund, Reconciliation birer use-case'tir.
- Neden / çözdüğü problem: Controller ile domain/persistence detayları arasındaki orchestration'ı tek yerde toplar.
- Not / trade-off: Use-case business transaction ile HTTP endpoint birebir aynı olmak zorunda değildir.

### Value Object
- Tanım: Kimlikten çok değeri ve geçerlilik kuralları önemli olan immutable domain nesnesidir.
- FinWallet'ta kullanım: `Money` ve currency gibi kavramlar bu amaçla modellenir.
- Neden / çözdüğü problem: Amount/currency birlikteliği ve geçersiz para değerlerinin domain'e girmemesini sağlar.
- Not / trade-off: Value Object'lar küçük ve immutable tutulmalıdır.

## 2. API, Gateway ve Ağ (32 terim)

### Active Health Check
- Tanım: Proxy'nin belirli aralıklarla backend'e probe göndermesidir.
- FinWallet'ta kullanım: YARP bazı FinWallet/FakeBank/FakeFraud destination'larını aktif olarak kontrol edebilir.
- Neden / çözdüğü problem: Trafik gelmese bile arızayı tespit eder.
- Not / trade-off: Çok sık probe gereksiz yük üretir; interval/timeout config'ten yönetilir.

### API (Application Programming Interface)
- Tanım: Bir yazılımın başka yazılımlar tarafından hangi sözleşmeyle çağrılacağını tanımlayan arayüzdür.
- FinWallet'ta kullanım: FinWallet public müşteri API'leri ve internal provider API'leri HTTP üzerinden exposed edilir.
- Neden / çözdüğü problem: Client ile business logic arasında stabil bir sözleşme sağlar.
- Not / trade-off: API sözleşmesi domain modelinin birebir kopyası olmamalıdır; DTO ve ServiceResult ile sınır korunur.

### API Gateway
- Tanım: Birden fazla backend'e ortak giriş noktası sağlayan edge uygulamasıdır.
- FinWallet'ta kullanım: FinWallet.Gateway JWT validation, routing, rate-limit, service-key policy ve load balancing uygular.
- Neden / çözdüğü problem: Client'ın backend topology'sini bilmesini engeller ve ortak edge policy'leri merkezi hale getirir.
- Not / trade-off: Gateway içine domain business logic taşınmaz.

### Cluster
- Tanım: YARP'ta aynı logical backend servisin bir veya daha fazla destination grubudur.
- FinWallet'ta kullanım: FinWallet.Api ve fake provider'ların her biri kendi cluster'ında tanımlanır.
- Neden / çözdüğü problem: Replica eklenmesini ve load-balancing/health policy uygulanmasını kolaylaştırır.
- Not / trade-off: Development'da tek destination olması cluster kavramını gereksiz yapmaz; production scaling için hazırdır.

### Connection Keep-Alive
- Tanım: Aynı TCP/HTTP bağlantısının birden fazla request için yeniden kullanılmasıdır.
- FinWallet'ta kullanım: Kestrel ve outbound HttpClient timeout/lifetime ayarlarıyla kontrollü tutulur.
- Neden / çözdüğü problem: Handshake maliyetini ve latency'yi düşürür.
- Not / trade-off: Aşırı uzun lifetime stale connection/DNS sorunlarına neden olabilir.

### Content-Type
- Tanım: HTTP body'nin veri formatını belirten header'dır.
- FinWallet'ta kullanım: Write request'lerinde `application/json` zorunlu tutulur.
- Neden / çözdüğü problem: Yanlış/ambiguous payload parsing riskini azaltır.
- Not / trade-off: Body olmayan request'lerde JSON zorunluluğu uygulanmaz.

### Controller
- Tanım: ASP.NET Core'da HTTP request'i alan ve Application use-case'ine yönlendiren sınıftır.
- FinWallet'ta kullanım: FinWallet Minimal API yerine controller-based API kullanır.
- Neden / çözdüğü problem: HTTP concerns ile business orchestration'ı ayırır ve Swagger metadata'sını düzenli tutar.
- Not / trade-off: Controller kalınlaşırsa business logic katman sızıntısı oluşur.

### Correlation ID
- Tanım: Bir request'in servisler arası izini takip etmek için taşınan benzersiz referanstır.
- FinWallet'ta kullanım: `X-Correlation-Id` Gateway, API ve provider çağrılarında taşınır; geçersizse yeniden üretilir.
- Neden / çözdüğü problem: Dağıtık loglarda aynı iş akışının parçalarını ilişkilendirir.
- Not / trade-off: Idempotency key veya FinancialTransactionId değildir; finansal tekrar güvenliği sağlamaz.

### CORS
- Tanım: Browser'ın farklı origin'e yaptığı request'leri hangi origin/method/header kombinasyonunda kabul edeceğini belirleyen politikadır.
- FinWallet'ta kullanım: Allowed origins appsettings üzerinden allow-list olarak yönetilir.
- Neden / çözdüğü problem: Browser tabanlı cross-origin erişimi sınırlar.
- Not / trade-off: CORS authentication değildir ve non-browser client'ları engellemez.

### Destination
- Tanım: YARP cluster içindeki tek backend instance adresidir.
- FinWallet'ta kullanım: Örneğin Docker DNS üzerinden `http://finwallet-api:8080` bir destination olabilir.
- Neden / çözdüğü problem: Load balancer'ın seçebileceği concrete servis instance'ını temsil eder.
- Not / trade-off: Health check sonucu unhealthy destination trafik dışına alınabilir.

### DTO (Data Transfer Object)
- Tanım: Katmanlar veya sistemler arasında veri taşımak için kullanılan sözleşme nesnesidir.
- FinWallet'ta kullanım: Request/response modelleri ile FakeBank/FakeFraud provider contract'ları DTO'dur.
- Neden / çözdüğü problem: Domain entity'lerini doğrudan expose etmeyi engeller.
- Not / trade-off: DTO validation ve mapping maliyeti vardır ama boundary netliği sağlar.

### Endpoint
- Tanım: Belirli bir HTTP method + URL kombinasyonuyla erişilen API operasyonudur.
- FinWallet'ta kullanım: `POST /api/v1/transfers` veya `GET /api/v1/transactions` endpoint örnekleridir.
- Neden / çözdüğü problem: Use-case'in dış dünyaya açılan giriş noktasıdır.
- Not / trade-off: Endpoint business logic'i içermez; request mapping ve response üretimi yapar.

### Fixed-Window Rate Limiter
- Tanım: Zamanı sabit pencerelere bölerek her pencere için izin sayısı uygulayan algoritmadır.
- FinWallet'ta kullanım: Shared.Web global rate limiter bu modeli kullanır.
- Neden / çözdüğü problem: Basit, düşük maliyetli ve config-driven'dır.
- Not / trade-off: Pencere sınırında burst oluşabilir; daha ileri ihtiyaçta sliding/token-bucket düşünülebilir.

### Health Check
- Tanım: Bir servisin trafik almaya uygun olup olmadığını kontrol eden mekanizmadır.
- FinWallet'ta kullanım: Gateway/Compose servisleri live/health endpoint'leri ve provider health kontrolleri kullanır.
- Neden / çözdüğü problem: Bozuk instance'a trafik göndermeyi azaltır ve container startup sırasını doğrular.
- Not / trade-off: Health endpoint sadece process ayakta mı değil, gerektiğinde dependency readiness'i de ayırmalıdır.

### HTTP Status Code
- Tanım: HTTP response'un genel sonucunu standart numarayla ifade eder.
- FinWallet'ta kullanım: 200, 202, 400, 401, 403, 404, 409, 422, 429 ve 503 FinWallet akışlarında kullanılır.
- Neden / çözdüğü problem: Transport seviyesinde sonucu standartlaştırır; detay machine code ile tamamlanır.
- Not / trade-off: Aynı HTTP status farklı business code'lar taşıyabilir; client sadece status'a bakmamalıdır.

### HttpClientFactory
- Tanım: .NET'te HttpClient yaşam döngüsünü ve handler pooling'i merkezi yöneten altyapıdır.
- FinWallet'ta kullanım: Bank, Fraud, Cutoff, Campaign ve Communication typed client'ları bununla oluşturulur.
- Neden / çözdüğü problem: Socket exhaustion ve yanlış HttpClient lifetime kullanımını azaltır.
- Not / trade-off: Financial retry policy otomatik eklenmez; retry business semantics'e göre ayrıca tasarlanır.

### JSON
- Tanım: HTTP API'lerde yaygın kullanılan metin tabanlı veri formatıdır.
- FinWallet'ta kullanım: FinWallet write request'lerinde `application/json` zorunlu tutulur.
- Neden / çözdüğü problem: Platformlar arası kolay serialization ve debugging sağlar.
- Not / trade-off: Finansal decimal ve tarih formatları sözleşmede kontrollü kullanılmalıdır.

### Kestrel
- Tanım: ASP.NET Core uygulamalarının HTTP server'ıdır.
- FinWallet'ta kullanım: Shared.Web request body/header, connection, keep-alive ve header timeout sınırlarını Kestrel üzerinden ayarlar.
- Neden / çözdüğü problem: Resource consumption ve header/body abuse risklerini uygulama seviyesinde sınırlar.
- Not / trade-off: Internet edge protection yerine geçmez; reverse proxy/LB arkasında çalışabilir.

### Load Balancing
- Tanım: İsteklerin birden fazla backend instance arasında dağıtılmasıdır.
- FinWallet'ta kullanım: YARP production'da aynı cluster'a eklenen replica'lar arasında trafik dağıtabilir.
- Neden / çözdüğü problem: Kapasite ve availability artırır.
- Not / trade-off: Stateful correctness load balancer sticky-session'a bağlı olmamalıdır; durable state MSSQL'de olmalıdır.

### OpenAPI
- Tanım: REST API sözleşmesini makine tarafından okunabilir formatta tanımlayan spesifikasyondur.
- FinWallet'ta kullanım: Swagger tooling arka planda OpenAPI document üretir; kullanıcıya yönelik terim olarak Swagger daha çok görünür.
- Neden / çözdüğü problem: Tooling/client generation ve schema keşfi sağlar.
- Not / trade-off: FinWallet domain mimarisi OpenAPI'ye bağımlı değildir.

### Passive Health Check
- Tanım: Gerçek trafik sırasında oluşan hatalara göre destination health değerlendirmesidir.
- FinWallet'ta kullanım: FinWallet API cluster'ında transport failure sonrası destination geçici olarak devre dışı bırakılabilir.
- Neden / çözdüğü problem: Ek probe olmadan gerçek failure sinyalini kullanır.
- Not / trade-off: Düşük trafik ortamında arızayı geç fark edebilir; active check ile birlikte daha güçlüdür.

### PowerOfTwoChoices
- Tanım: Load balancing'de rastgele iki candidate seçip daha az yüklü olana yönlendiren algoritmadır.
- FinWallet'ta kullanım: FinWallet YARP cluster'larında tercih edilen policy'dir.
- Neden / çözdüğü problem: Round-robin'e göre çok az ek maliyetle daha iyi yük dağılımı sağlayabilir.
- Not / trade-off: Tek destination olduğunda fiilen seçim yapmaz; replica sayısı arttığında anlam kazanır.

### Rate Limiting
- Tanım: Belirli süre içinde kabul edilen istek sayısını sınırlamaktır.
- FinWallet'ta kullanım: Gateway per-IP fixed-window limit uygular; backend de defense-in-depth limiti tutabilir.
- Neden / çözdüğü problem: Resource exhaustion, brute-force ve basit L7 abuse etkisini azaltır.
- Not / trade-off: Volumetric DDoS çözümü değildir; edge DDoS/WAF yine gerekir.

### REST
- Tanım: HTTP resource/verb semantiğini kullanan web API yaklaşımıdır.
- FinWallet'ta kullanım: Controller tabanlı endpoint'lerde GET/POST gibi HTTP methodları ve JSON payload'lar kullanılır.
- Neden / çözdüğü problem: Basit, yaygın ve tooling desteği güçlü bir entegrasyon modeli sağlar.
- Not / trade-off: FinWallet tam HATEOAS odaklı akademik REST uygulamaz; pragmatik REST API kullanır.

### Retry-After
- Tanım: Client'a ne kadar süre sonra tekrar denemesi gerektiğini bildiren HTTP header'ıdır.
- FinWallet'ta kullanım: Rate limit rejection'ında mümkünse response'a eklenir.
- Neden / çözdüğü problem: Client'ın hemen tekrar tekrar request atmasını azaltır.
- Not / trade-off: Financial POST'ların retry davranışı ayrıca idempotency ve provider semantics'e bağlıdır.

### Reverse Proxy
- Tanım: Client isteğini alıp arka taraftaki başka bir servise ileten ara HTTP bileşenidir.
- FinWallet'ta kullanım: Gateway public `/api/*` isteklerini FinWallet.Api'ye, `/providers/*` isteklerini fake provider'lara iletir.
- Neden / çözdüğü problem: Backend adreslerini client'tan gizler ve merkezi trafik kontrolü sağlar.
- Not / trade-off: Proxy business source of truth olmamalıdır; finansal state Gateway'de tutulmaz.

### Route
- Tanım: Gateway'in gelen URL/path'i hangi backend cluster'a göndereceğini belirleyen kuraldır.
- FinWallet'ta kullanım: Public auth, protected API, internal callback ve `/providers/*` için ayrı YARP route'ları vardır.
- Neden / çözdüğü problem: Authentication policy ve routing önceliğini path seviyesinde kontrol eder.
- Not / trade-off: Route sırası/precedence yanlışsa internal endpoint yanlış auth policy'ye düşebilir.

### ServiceResult<T>
- Tanım: FinWallet'ın success/failure response'larını tek biçimde taşıyan ortak envelope modelidir.
- FinWallet'ta kullanım: `isSuccess`, `code`, `message`, `data`, `errors` alanlarıyla tüm API'lerde tutarlı response üretilir.
- Neden / çözdüğü problem: Client'ın her endpoint için farklı hata şekli parse etmesini engeller.
- Not / trade-off: HTTP status'un yerini almaz; HTTP status + ServiceResult code birlikte kullanılır.

### SocketsHttpHandler
- Tanım: .NET HttpClient'ın connection pooling, DNS/lifetime ve transport davranışını yöneten low-level handler'ıdır.
- FinWallet'ta kullanım: PooledConnectionLifetime, idle timeout ve MaxConnectionsPerServer gibi ayarlar config'ten verilir.
- Neden / çözdüğü problem: Uzun yaşayan servislerde connection reuse ve DNS yenilenmesini dengeler.
- Not / trade-off: Değerler ölçüm olmadan aşırı yükseltilmemelidir.

### Swagger
- Tanım: API endpoint, request/response ve schema'ları interaktif olarak keşfetmeyi sağlayan dokümantasyon arayüzüdür.
- FinWallet'ta kullanım: Gateway, FinWallet.Api ve tüm fake API'lerde Shared.Web/Swashbuckle ile bulunur.
- Neden / çözdüğü problem: Developer onboarding, manual test ve contract görünürlüğünü kolaylaştırır.
- Not / trade-off: Production'da varsayılan kapalıdır; açık olması endpoint authorization'ını bypass etmez.

### Timeout
- Tanım: Bir I/O operasyonunun sonsuza kadar beklemesini engelleyen süre sınırıdır.
- FinWallet'ta kullanım: Provider HttpClient, YARP activity ve request header süreleri config'ten sınırlandırılır.
- Neden / çözdüğü problem: Thread/connection kaynaklarının kilitlenmesini ve cascading stall riskini azaltır.
- Not / trade-off: Çok kısa timeout false failure; çok uzun timeout resource exhaustion üretir.

### YARP
- Tanım: Microsoft'un .NET için reverse-proxy toolkit'idir.
- FinWallet'ta kullanım: `FinWallet.Gateway` route, cluster, load balancing, health ve request transform davranışlarını YARP ile uygular.
- Neden / çözdüğü problem: Gateway'i özel low-level proxy kodu yazmadan config-driven yönetmeyi sağlar.
- Not / trade-off: YARP uygulama gateway'idir; volumetric DDoS için tek başına edge/WAF yerine geçmez.

## 3. Güvenlik ve Kimlik (39 terim)

### `sid` Claim
- Tanım: JWT içindeki session kimliğini taşıyan claim'dir.
- FinWallet'ta kullanım: Login'da oluşturulan durable session ID access token'a yazılır ve logout/fraud signal'larında kullanılır.
- Neden / çözdüğü problem: Token'ı aktif session state'iyle ilişkilendirir ve revoke kontrolü yapılmasını sağlar.
- Not / trade-off: Sadece token signature'a güvenmek yerine session lifecycle doğrulaması ekler.

### `sub` Claim
- Tanım: JWT standardında subject/ana kimlik claim'idir.
- FinWallet'ta kullanım: FinWallet'ta authenticated CustomerId'yi taşır.
- Neden / çözdüğü problem: Controller'ın request body'den müşteri kimliği kabul etmeden owner context üretmesini sağlar.
- Not / trade-off: Geçersiz/eksik GUID ise request `INVALID_ACCESS_TOKEN` ile reddedilir.

### Access Token
- Tanım: Kısa ömürlü, protected API çağrılarında kullanılan credential'dır.
- FinWallet'ta kullanım: Login/refresh sonrası JWT olarak üretilir.
- Neden / çözdüğü problem: Her request'te kullanıcı kimliğini doğrular.
- Not / trade-off: Kısa lifetime çalınmış token riskini azaltır ama refresh mekanizması gerektirir.

### Authentication
- Tanım: Bir isteği yapan kişinin veya servisin kim olduğunu doğrulama işlemidir.
- FinWallet'ta kullanım: Public client için JWT/login; internal servisler için service-key kullanılır.
- Neden / çözdüğü problem: Kimliği doğrulanmamış aktörün korumalı endpoint'e erişmesini engeller.
- Not / trade-off: Authentication kimliğin ne yapabileceğini belirlemez; authorization ayrı kontroldür.

### Authorization
- Tanım: Doğrulanmış kimliğin belirli bir kaynağa/işleme izinli olup olmadığını kontrol etmektir.
- FinWallet'ta kullanım: Gateway route policy'leri, `[Authorize]`, owner-aware SQL ve internal service policy'leri kullanılır.
- Neden / çözdüğü problem: Başka müşterinin wallet/transaction verisine erişim gibi BOLA risklerini azaltır.
- Not / trade-off: Sadece role bakmak yeterli değildir; resource ownership de doğrulanır.

### Bearer Token
- Tanım: Token'a sahip olan tarafın onu HTTP Authorization header ile sunduğu access-token kullanım biçimidir.
- FinWallet'ta kullanım: `Authorization: Bearer <JWT>` protected client endpoint'lerinde kullanılır.
- Neden / çözdüğü problem: Standart HTTP tooling ile kolay entegrasyon sağlar.
- Not / trade-off: Token ele geçirilirse süresi dolana veya session revoke edilene kadar risk oluşturur; bu yüzden TLS ve kısa lifetime gerekir.

### BOLA (Broken Object Level Authorization)
- Tanım: Authenticated kullanıcının başka kullanıcıya ait object'i ID değiştirerek erişebilmesi zafiyetidir.
- FinWallet'ta kullanım: Wallet, transaction ve bank-account sorgularında customer ownership server-side doğrulanır.
- Neden / çözdüğü problem: ID tahmini veya request manipulation ile başka müşterinin verisine erişimi engeller.
- Not / trade-off: Sadece JWT doğrulamak BOLA'yı çözmez; object-level owner check şarttır.

### CDN
- Tanım: İçeriği edge noktalarında cache/serve eden dağıtık delivery network'üdür.
- FinWallet'ta kullanım: FinWallet API için zorunlu component değildir; DDoS/edge architecture tartışmalarında olası production katmanı olarak geçer.
- Neden / çözdüğü problem: Static content ve edge traffic absorption sağlar.
- Not / trade-off: Authenticated financial API request'leri için caching politikaları çok dikkatli yönetilmelidir.

### Claim
- Tanım: JWT içinde kimlik veya session hakkında taşınan isim/değer bilgisidir.
- FinWallet'ta kullanım: `sub` customer identity, `sid` session identity için kullanılır.
- Neden / çözdüğü problem: Gateway/API'nin request'i server-side identity ile ilişkilendirmesini sağlar.
- Not / trade-off: Client body'den gelen customerId yerine token claim'leri tercih edilerek spoofing azaltılır.

### CSP (Content Security Policy)
- Tanım: Browser'ın hangi script/style/frame kaynaklarını çalıştırabileceğini sınırlayan response policy'sidir.
- FinWallet'ta kullanım: Shared.Web API response'larında restrictive CSP; Swagger için daha uygun ayrı CSP kullanır.
- Neden / çözdüğü problem: XSS/frame injection etkisini azaltan browser defense-in-depth katmanıdır.
- Not / trade-off: API'nin kendisi UI olmadığı için etkisi sınırlı ama güvenli default sağlar.

### Data Masking
- Tanım: Log veya response'ta hassas verinin tamamını göstermeyip güvenli şekilde kısmen gizlemektir.
- FinWallet'ta kullanım: Token, OTP, password ve secret hiç loglanmaz; PII gerektiğinde minimize/mask edilir.
- Neden / çözdüğü problem: Operational görünürlük ile privacy/security arasında denge sağlar.
- Not / trade-off: Masking encryption değildir; masked veri kaynak sistemde hâlâ korunmalıdır.

### DDoS Protection
- Tanım: Çok yüksek hacimli dağıtık trafik saldırılarında network/edge kapasitesini koruyan altyapı mekanizmasıdır.
- FinWallet'ta kullanım: App-level rate limits yalnız L7 abuse sınırlar; production'da cloud/LB/CDN DDoS koruması gerekir.
- Neden / çözdüğü problem: Volumetric trafik uygulamaya ulaşmadan absorbe/filter edilir.
- Not / trade-off: Kestrel rate limit tek başına DDoS çözümü değildir.

### DownstreamServiceKey
- Tanım: Gateway'in FinWallet.Api veya fake provider'a proxy ettiği isteğin Gateway'den geldiğini kanıtlayan ayrı secret'tır.
- FinWallet'ta kullanım: YARP transform proxied request'e header ekler; destination Shared.Web kontrol eder.
- Neden / çözdüğü problem: Gateway bypass edilip backend portuna doğrudan erişildiğinde business endpoint'in trusted olmamasını sağlar.
- Not / trade-off: InternalServiceKey ile aynı secret kullanılmaz; trust boundary ayrımı korunur.

### Fail-Closed
- Tanım: Güvenlik/karar servisi hata verdiğinde işlemi güvenli tarafta reddetme yaklaşımıdır.
- FinWallet'ta kullanım: External fraud provider unavailable olduğunda protected transfer/purchase devam etmez ve 503 döner.
- Neden / çözdüğü problem: Risk değerlendirmesi olmadan para hareketinin gerçekleşmesini engeller.
- Not / trade-off: Availability düşebilir; hangi dependency'nin fail-closed olması business risk iştahına bağlıdır.

### Fail-Open
- Tanım: Kontrol servisi hata verdiğinde işlemin yine de devam etmesi yaklaşımıdır.
- FinWallet'ta kullanım: FinWallet fraud kararında bilinçli olarak kullanılmaz; communication gibi post-commit yan etkilerde ise para rollback edilmez.
- Neden / çözdüğü problem: Bazı non-critical dependency'lerde availability'yi koruyabilir.
- Not / trade-off: Fraud/auth gibi kritik kararlarda finansal risk yaratır; bu yüzden kullanım alanı dikkatle ayrılır.

### Fixed-Time Comparison
- Tanım: Secret karşılaştırmalarında karakter karakter erken çıkış yapmayarak timing side-channel riskini azaltan karşılaştırmadır.
- FinWallet'ta kullanım: Internal service key doğrulamasında `CryptographicOperations.FixedTimeEquals` kullanılır.
- Neden / çözdüğü problem: Doğru secret'in prefix bilgisinin response süresinden tahmin edilmesini zorlaştırır.
- Not / trade-off: Önce uzunluk kontrolü yine gerekir; tamamen tüm side-channel'ları çözmez.

### HMAC
- Tanım: Bir secret key ile mesaj bütünlüğü/doğrulama değeri üretme mekanizmasıdır.
- FinWallet'ta kullanım: JWT HS256 ve OTP doğrulama gibi alanlarda HMAC yaklaşımı kullanılır.
- Neden / çözdüğü problem: Mesajın secret'i bilmeyen biri tarafından geçerli biçimde üretilememesini sağlar.
- Not / trade-off: Encryption değildir; veriyi gizlemez, doğruluk/authenticity sağlar.

### HS256 / HMAC-SHA256
- Tanım: JWT imzasında paylaşılan secret ile SHA-256 tabanlı HMAC kullanan simetrik algoritmadır.
- FinWallet'ta kullanım: FinWallet access token'larında fixed algorithm olarak kullanılır; signing key en az 32 UTF-8 byte olmalıdır.
- Neden / çözdüğü problem: Basit ve hızlıdır; tek güven alanında yeterli olabilir.
- Not / trade-off: Secret'i bilen herkes token imzalayabilir; çoklu trust domain için asymmetric key düşünülebilir.

### HSTS
- Tanım: Browser'a belirli süre boyunca siteye sadece HTTPS ile bağlanmasını söyleyen response policy'sidir.
- FinWallet'ta kullanım: Production'da config ile etkinleştirilebilir.
- Neden / çözdüğü problem: HTTPS downgrade/accidental HTTP kullanımını azaltır.
- Not / trade-off: Local development ve TLS termination topology'si dikkate alınarak kullanılmalıdır.

### InternalServiceKey
- Tanım: FinWallet.Api'nin Gateway'in internal provider route'larına trusted caller olduğunu kanıtlayan secret'tır.
- FinWallet'ta kullanım: `X-Internal-Service-Key` ile `/providers/*` veya internal route'a gönderilir ve Gateway policy doğrular.
- Neden / çözdüğü problem: Normal client'ın provider route'larını doğrudan kullanmasını engeller.
- Not / trade-off: Production'da secret-store/env injection ile verilmeli ve yeterince uzun olmalıdır.

### JWT (JSON Web Token)
- Tanım: İmzalı claim'ler taşıyan stateless access-token formatıdır.
- FinWallet'ta kullanım: Login sonrası access token üretilir; Gateway ve FinWallet.Api issuer/audience/signature/lifetime doğrular.
- Neden / çözdüğü problem: Her request'te DB'den kimlik bilgisi taşımadan authenticated identity sağlar.
- Not / trade-off: JWT iptali doğal olarak zor olduğundan `sid` session state'iyle birlikte kullanılır.

### Least Privilege
- Tanım: Kullanıcı, servis veya workflow'a yalnız gerekli minimum yetkinin verilmesi güvenlik prensibidir.
- FinWallet'ta kullanım: Gateway internal route'ları ayrı key/policy, GitHub workflow permissions sınırlı ve backend portları public değil tutulur.
- Neden / çözdüğü problem: Credential compromise durumunda blast radius'u azaltır.
- Not / trade-off: Gereğinden az yetki operasyonu kırabilir; yetki ihtiyaçları açıkça belgelenmelidir.

### Logout / Session Revocation
- Tanım: Aktif session'ın server-side geçersiz hale getirilmesidir.
- FinWallet'ta kullanım: `POST /api/v1/auth/logout` JWT içindeki `sid` session'ını revoke eder.
- Neden / çözdüğü problem: Access token henüz expire olmamış olsa bile API'nin session kontrolüyle kullanımı engellenebilir.
- Not / trade-off: Tam etki API'nin her korumalı akışta session doğrulaması yapmasına bağlıdır.

### OTP (One-Time Password)
- Tanım: Tek kullanımlık ve kısa ömürlü doğrulama kodudur.
- FinWallet'ta kullanım: Registration verification için SMS üzerinden gönderilir; TTL'li geçici state kullanılır.
- Neden / çözdüğü problem: Telefon sahipliği gibi ikinci bir doğrulama adımı sağlar.
- Not / trade-off: OTP access token değildir; log/response içinde plaintext sızdırılmamalıdır.

### OWASP API Security Top 10
- Tanım: API'lere özgü BOLA, broken authentication, resource consumption ve unsafe consumption gibi riskleri sınıflandırır.
- FinWallet'ta kullanım: Gateway/API auth, owner-aware SQL, rate limit ve provider ACL tasarımında referans alınır.
- Neden / çözdüğü problem: API threat'lerini klasik web UI risklerinden daha net ayırır.
- Not / trade-off: Liste tek başına yeterli değildir; business abuse/fraud ayrıca ele alınır.

### OWASP Top 10
- Tanım: Web uygulamalarındaki yaygın güvenlik risklerini sınıflandıran OWASP rehberidir.
- FinWallet'ta kullanım: FinWallet security dokümanı access control, misconfiguration, cryptography, injection, logging gibi başlıklarla mapping yapar.
- Neden / çözdüğü problem: Güvenlik review'unda ortak checklist/dil sağlar.
- Not / trade-off: Compliance sertifikası değildir; threat model ve gerçek testlerin yerini almaz.

### PBKDF2
- Tanım: Password'ları brute-force'a karşı yavaşlatılmış şekilde hash'lemek için kullanılan key-derivation algoritmasıdır.
- FinWallet'ta kullanım: FinWallet credential storage'da PBKDF2 V1 sabit work factor ve salt ile kullanılır.
- Neden / çözdüğü problem: Plaintext password veya hızlı hash saklama riskini azaltır.
- Not / trade-off: Iteration/work factor config'ten rastgele değiştirilemez; versioned migration/re-hash gerekir.

### Pepper
- Tanım: Hash/HMAC işlemlerine uygulama secret'ı olarak eklenen, veritabanından ayrı tutulan gizli değerdir.
- FinWallet'ta kullanım: Registration OTP doğrulamasında `REGISTRATION_OTP_PEPPER` gibi secret kullanılabilir.
- Neden / çözdüğü problem: Sadece DB ele geçirilse bile doğrulama material'ının tek başına yeterli olmamasını sağlar.
- Not / trade-off: Pepper rotate edilmesi operasyonel plan gerektirir; source control'a konmaz.

### PII (Personally Identifiable Information)
- Tanım: Bir gerçek kişiyi doğrudan veya dolaylı tanımlayabilen veridir.
- FinWallet'ta kullanım: Phone/email gibi registration verileri business için tutulur; fraud/reconciliation response'larında gereksiz PII çıkarılmaz.
- Neden / çözdüğü problem: Log ve internal API'lerde veri minimizasyonu sağlar.
- Not / trade-off: PII tanımı regülasyona göre değişebilir; production privacy sınıflandırması gerekir.

### Refresh Token
- Tanım: Access token süresi dolduğunda yeni access token almak için kullanılan daha uzun ömürlü opaque credential'dır.
- FinWallet'ta kullanım: FinWallet refresh session akışında durable state ve rotation ile yönetilir.
- Neden / çözdüğü problem: Kullanıcıyı sık login ettirmeden access token'ı kısa tutmayı sağlar.
- Not / trade-off: Çalınması yüksek risklidir; hash/rotation/revocation ve güvenli saklama gerekir.

### Refresh Token Rotation
- Tanım: Her başarılı refresh'te eski refresh token'ın geçersizleşip yenisinin üretilmesidir.
- FinWallet'ta kullanım: FinWallet session güvenliğinde replay riskini azaltmak için kullanılır.
- Neden / çözdüğü problem: Ele geçirilen eski refresh token'ın tekrar kullanımını sınırlar.
- Not / trade-off: Concurrent refresh race'leri durable transaction/unique state ile test edilmelidir.

### Salt
- Tanım: Password hash'e kullanıcı/credential başına eklenen rastgele değerdir.
- FinWallet'ta kullanım: PBKDF2 hash kayıtları benzersiz salt kullanır.
- Neden / çözdüğü problem: Aynı password'ün aynı hash'i üretmesini ve precomputed rainbow-table etkisini azaltır.
- Not / trade-off: Salt secret olmak zorunda değildir; her credential için benzersiz ve yeterince rastgele olmalıdır.

### Secret Injection
- Tanım: Signing key, DB password veya service key gibi secret'ların source code yerine deployment ortamından verilmesidir.
- FinWallet'ta kullanım: Production appsettings placeholder bırakır; environment/secret-store override beklenir.
- Neden / çözdüğü problem: Secret'ın Git history veya image içine gömülmesini önler.
- Not / trade-off: Local `.env` yalnız development kolaylığıdır; production secret manager'ın alternatifi değildir.

### Security Headers
- Tanım: Browser/client davranışını daha güvenli default'lara yönlendiren HTTP response header setidir.
- FinWallet'ta kullanım: X-Content-Type-Options, X-Frame-Options, Referrer-Policy, Permissions-Policy, CSP ve no-store gibi header'lar Shared.Web'de merkezi uygulanır.
- Neden / çözdüğü problem: Tek tek endpoint'lerin güvenlik header'ı unutmasını önler.
- Not / trade-off: Authentication/authorization yerine geçmez; ek hardening katmanıdır.

### Service-to-Service Authentication
- Tanım: İnsan kullanıcı yerine bir backend servisin başka servise kimliğini kanıtlamasıdır.
- FinWallet'ta kullanım: FinWallet.Api -> Gateway için InternalServiceKey, Gateway -> backend/provider için DownstreamServiceKey kullanılır.
- Neden / çözdüğü problem: Public JWT'nin internal machine trust için yeniden kullanılmasını engeller.
- Not / trade-off: Static key yerine production'da mTLS/managed identity gibi daha güçlü yöntemler düşünülebilir.

### Session
- Tanım: Authenticated kullanıcının login yaşam döngüsünü temsil eden server-side kayıttır.
- FinWallet'ta kullanım: `sid`, device ve revoke/refresh bilgileri MSSQL'de durable tutulur.
- Neden / çözdüğü problem: JWT stateless olsa bile logout, revocation ve risk sinyali için server-side kontrol sağlar.
- Not / trade-off: Session state financial source of truth değildir ama auth correctness için authoritative'dir.

### SQL Injection
- Tanım: User input'ının SQL komut yapısına karışarak beklenmeyen query çalıştırması zafiyetidir.
- FinWallet'ta kullanım: FinWallet SQL erişiminde parameterized command kullanır ve user-controlled SQL fragment kabul etmez.
- Neden / çözdüğü problem: Veri sızması/değiştirme ve privilege abuse riskini azaltır.
- Not / trade-off: Parametre kullanmak dynamic table/column isimlerini otomatik güvenli yapmaz; bunlar server-owned olmalıdır.

### SSRF (Server-Side Request Forgery)
- Tanım: Client input'ıyla server'ın istenmeyen internal/external URL'lere request atmaya zorlanması zafiyetidir.
- FinWallet'ta kullanım: Provider base URL'leri server-owned configuration'dır; client arbitrary URL gönderemez.
- Neden / çözdüğü problem: Metadata service/internal network erişimi gibi riskleri azaltır.
- Not / trade-off: Config/secrets ele geçirilirse ayrı risk vardır; network egress policy yine faydalıdır.

### WAF (Web Application Firewall)
- Tanım: HTTP/L7 saldırı pattern'lerini edge'de filtreleyen güvenlik katmanıdır.
- FinWallet'ta kullanım: FinWallet code içinde WAF yoktur; production DDoS/OWASP edge protection için dış altyapı olarak önerilir.
- Neden / çözdüğü problem: Uygulama daha request'i almadan bazı abuse/signature trafiğini azaltır.
- Not / trade-off: Business authorization/fraud kurallarının alternatifi değildir.

## 4. Veri, Persistence ve Concurrency (36 terim)

### ACID
- Tanım: Database transaction'larının Atomicity, Consistency, Isolation ve Durability özelliklerini özetleyen kavramdır.
- FinWallet'ta kullanım: Financial posting MSSQL transaction'ları bu özelliklerden yararlanır.
- Neden / çözdüğü problem: Para hareketinin yarım commit edilmemesi ve concurrent işlemlerin kuralları bozmaması için temel sağlar.
- Not / trade-off: External HTTP ACID transaction'ın parçası değildir; distributed consistency ayrıca yönetilir.

### Atomicity
- Tanım: Bir transaction içindeki değişikliklerin ya tamamen uygulanması ya da hiç uygulanmamasıdır.
- FinWallet'ta kullanım: Transfer'da source debit, destination credit, FinancialTransaction, Ledger ve Idempotency birlikte commit/rollback olur.
- Neden / çözdüğü problem: Tek taraflı para hareketini engeller.
- Not / trade-off: External provider çağrısı bu atomicity'nin dışında olduğundan compensation/reconciliation gerekir.

### Check Constraint
- Tanım: Bir row değerinin belirli mantıksal koşulu sağlamasını DB seviyesinde zorunlu tutar.
- FinWallet'ta kullanım: Amount/status/type/financial invariant'ların bazıları schema seviyesinde desteklenir.
- Neden / çözdüğü problem: Application bug'ı olsa bile invalid row yazılmasını azaltır.
- Not / trade-off: Komple business flow'u DB constraint'e taşımak yerine basit invariant'lar için kullanılır.

### Commit
- Tanım: Database transaction'ındaki değişiklikleri kalıcı hale getirmektir.
- FinWallet'ta kullanım: Finansal endpoint başarıya gitmeden önce ilgili atomic posting commit edilir.
- Neden / çözdüğü problem: Success response ile durable state arasındaki sıralamayı netleştirir.
- Not / trade-off: Commit sonrası communication failure para transaction'ını geri alamaz; outbox retry devreye girer.

### Connection Pooling
- Tanım: DB veya HTTP connection'larının her işlemde fiziksel olarak yeniden açılmak yerine havuzdan reuse edilmesidir.
- FinWallet'ta kullanım: SqlConnection logical open/close provider pool kullanır; HttpClient da handler connection pool kullanır.
- Neden / çözdüğü problem: Handshake/login maliyetini ve latency'yi düşürür.
- Not / trade-off: Pool size yanlış ayarlanırsa DB'yi boğabilir veya request'leri kuyrukta bekletebilir.

### Consistency (Database)
- Tanım: Transaction öncesi ve sonrası verinin constraint/invariant'lara uygun geçerli durumda kalmasıdır.
- FinWallet'ta kullanım: Ledger balance, non-negative wallet ve foreign/unique constraints bu amacı destekler.
- Neden / çözdüğü problem: DB'nin invalid state kabul etmesini azaltır.
- Not / trade-off: Business consistency sadece DB constraint ile değil Application/Domain kurallarıyla birlikte sağlanır.

### Database Migration
- Tanım: Schema'nın bir versiyondan diğerine kontrollü değiştirilmesidir.
- FinWallet'ta kullanım: Docker `mssql-init` 001, 002, 003, 004 gibi scriptleri SchemaVersions kontrolüyle sıralı uygular.
- Neden / çözdüğü problem: Yeni kodun ihtiyaç duyduğu schema'yı tekrarlanabilir şekilde kurar.
- Not / trade-off: Migration tekrar çalıştırılabilirlik/idempotency ve rollback/forward-fix planı gerektirir.

### Database Transaction
- Tanım: Bir grup SQL değişikliğini tek commit/rollback birimi olarak çalıştıran mekanizmadır.
- FinWallet'ta kullanım: Wallet posting, correction, outbox claim/finalization ve bazı auth state değişimleri transaction içinde yapılır.
- Neden / çözdüğü problem: Birbiriyle ilişkili row'ların birlikte değişmesini sağlar.
- Not / trade-off: External HTTP çağrısı transaction içinde tutulmaz.

### Decimal Precision
- Tanım: Parasal değerlerin floating-point yerine sabit ondalık hassasiyetle saklanmasıdır.
- FinWallet'ta kullanım: Money amount alanları decimal/DB decimal olarak tutulur.
- Neden / çözdüğü problem: Binary floating-point rounding hatalarının finansal hesaplara sızmasını engeller.
- Not / trade-off: Scale/rounding kuralları domain invariant olarak tutarlı olmalıdır.

### Durability
- Tanım: Commit edilmiş transaction'ın crash sonrası kaybolmaması özelliğidir.
- FinWallet'ta kullanım: Completed transaction ve ledger kayıtları MSSQL commit sonrası kalıcı kabul edilir.
- Neden / çözdüğü problem: Client'a başarı döndükten sonra finansal state'in ortadan kaybolmamasını sağlar.
- Not / trade-off: HA/backup/disaster-recovery durability'nin altyapı boyutunu tamamlar.

### Durable State
- Tanım: Process/container restart olsa bile kalması gereken persist edilmiş veridir.
- FinWallet'ta kullanım: Wallet balance, ledger, transaction, idempotency, session, fraud review ve outbox/inbox MSSQL'de durable tutulur.
- Neden / çözdüğü problem: Crash/restart sonrasında correctness ve replay davranışını korur.
- Not / trade-off: Durable olmak tek başına doğru olmak değildir; transaction/constraint kuralları da gerekir.

### Filtered Index
- Tanım: Sadece belirli koşulu sağlayan row'ları indexleyen SQL Server index türüdür.
- FinWallet'ta kullanım: Pending/active gibi küçük subset sorgularında schema tarafından kullanılabilir.
- Neden / çözdüğü problem: Daha küçük index ile targeted query performansı sağlar.
- Not / trade-off: SQL Server session SET option'ları (ör. QUOTED_IDENTIFIER) migration sırasında doğru olmalıdır.

### Foreign Key
- Tanım: Bir tablodaki referansın başka tablodaki mevcut row'a işaret etmesini zorunlu tutan constraint'tir.
- FinWallet'ta kullanım: Transaction detail, ledger entry ve wallet/customer ilişkileri gibi bağlantıları korur.
- Neden / çözdüğü problem: Orphan data oluşmasını azaltır.
- Not / trade-off: High-write sistemlerde constraint maliyeti vardır ama finansal bütünlük için genellikle değerlidir.

### GUID / UUID
- Tanım: Dağıtık ortamlarda benzersiz kimlik üretmek için kullanılan 128-bit identifier formatıdır.
- FinWallet'ta kullanım: Customer, Session, Wallet, Transaction, Journal, FraudEvent gibi birçok entity kimliği GUID'dir.
- Neden / çözdüğü problem: Merkezi sequence koordinasyonu olmadan kimlik üretimini kolaylaştırır.
- Not / trade-off: Random GUID clustered index fragmentation yaratabilir; index tasarımı ayrı düşünülmelidir.

### Index
- Tanım: Query'lerin row'lara daha hızlı ulaşmasını sağlayan veri yapısıdır.
- FinWallet'ta kullanım: Customer/transaction/idempotency/history sorgularında uygun key'ler için kullanılır.
- Neden / çözdüğü problem: Logical read ve latency'yi düşürebilir.
- Not / trade-off: Her index write/storage maliyeti getirir; Query Store/plan/ölçüm olmadan rastgele eklenmez.

### Isolation
- Tanım: Concurrent transaction'ların birbirlerinin ara state'ini ne ölçüde görebildiğini belirleyen özelliktir.
- FinWallet'ta kullanım: Financial posting store'larında overspend/idempotency race'i önlemek için güçlü isolation/locking kullanılır.
- Neden / çözdüğü problem: Aynı wallet üzerinde eşzamanlı debit'lerin toplam bakiyeyi aşmasını engellemeye yardımcı olur.
- Not / trade-off: Yüksek isolation lock contention yaratabilir; finansal hot-path ölçülerek ayarlanmalıdır.

### Keyset Pagination
- Tanım: Son görülen kayıt key'ini cursor olarak kullanıp sonraki sayfayı `WHERE < cursor` mantığıyla getiren pagination yöntemidir.
- FinWallet'ta kullanım: Transaction history `beforeTransactionId` ve ordering key'leriyle newest-first okunur.
- Neden / çözdüğü problem: Büyük OFFSET değerlerinde oluşan scan maliyetini azaltır ve concurrent insert'lerde daha stabil sayfalama sağlar.
- Not / trade-off: Arbitrary page number'a doğrudan atlamak zordur; cursor tabanlı UX gerektirir.

### Lock
- Tanım: Concurrent işlemlerin aynı veriyi uyumsuz şekilde değiştirmesini engelleyen DB concurrency mekanizmasıdır.
- FinWallet'ta kullanım: Wallet debit ve idempotency create/finalize sırasında SQL locking correctness'in parçasıdır.
- Neden / çözdüğü problem: İki request'in aynı bakiyeyi okuyup ikisinin de harcamasını önler.
- Not / trade-off: Uzun transaction ve yanlış lock order deadlock riskini artırır.

### MSSQL / SQL Server
- Tanım: İlişkisel veritabanı yönetim sistemidir.
- FinWallet'ta kullanım: FinWallet'ın Customer, Session, Wallet, BankAccount, FinancialTransaction, Ledger, Idempotency, FraudEvent, Outbox/Inbox ve Reconciliation için durable source of truth'üdür.
- Neden / çözdüğü problem: ACID transaction, constraint, locking ve güçlü query desteği finansal correctness sağlar.
- Not / trade-off: Scale gereksinimi arttığında indexing/partitioning/HA planı gerekir; Redis yerine geçici cache amacıyla kullanılmaz.

### Optimistic Concurrency
- Tanım: Çakışmanın nadir olduğu varsayımıyla version/CAS kontrolüyle update sırasında conflict yakalama yaklaşımıdır.
- FinWallet'ta kullanım: FinWallet bazı alanlarda DB uniqueness/compare-and-set semantics kullanabilir; ana wallet posting güçlü locking de kullanır.
- Neden / çözdüğü problem: Lock süresini azaltabilir.
- Not / trade-off: Hot financial balance'da yüksek conflict varsa pessimistic locking daha uygun olabilir.

### Parameterized SQL
- Tanım: SQL query değerlerini string concatenation yerine parameter olarak göndermektir.
- FinWallet'ta kullanım: FinWallet store'ları customerId, amount, status vb. değerleri SQL parameters ile kullanır.
- Neden / çözdüğü problem: SQL injection riskini azaltır ve query plan reuse sağlayabilir.
- Not / trade-off: Table/column isimleri parameter olamaz; server-owned kalmalıdır.

### Persistence
- Tanım: Uygulama state'inin kalıcı veri store'una yazılması ve okunmasıdır.
- FinWallet'ta kullanım: Infrastructure SQL store'ları domain/application state'ini MSSQL'e persist eder.
- Neden / çözdüğü problem: Process memory'den bağımsız yaşam döngüsü sağlar.
- Not / trade-off: Persistence modeli domain'i SQL tablo yapısına bağımlı hale getirmemelidir.

### Pessimistic Concurrency
- Tanım: Çakışma ihtimalini yüksek kabul edip işlem sırasında ilgili veriyi lock'lama yaklaşımıdır.
- FinWallet'ta kullanım: Critical wallet balance posting'inde aynı row'un concurrent debit'leri kontrol edilir.
- Neden / çözdüğü problem: Overspend riskini doğrudan DB transaction içinde önler.
- Not / trade-off: Throughput'u lock contention nedeniyle sınırlayabilir.

### Race Condition
- Tanım: Sonucun eşzamanlı işlemlerin timing/sırasına bağlı olarak yanlış değişmesi problemidir.
- FinWallet'ta kullanım: Aynı wallet'tan paralel 600+600 harcama, aynı idempotency key veya refresh token race'i örneklerdir.
- Neden / çözdüğü problem: Financial correctness'in multi-instance ortamda da deterministik kalmasını gerektirir.
- Not / trade-off: Unit test tek-thread race'i kanıtlamaz; gerçek DB concurrency testleri gerekir.

### RDB Snapshot
- Tanım: Redis memory state'inin belirli anda disk snapshot olarak kaydedilmesidir.
- FinWallet'ta kullanım: Docker runbook'ta `BGSAVE` ile snapshot istenebilir.
- Neden / çözdüğü problem: Local recovery/debug için ek persistence seçeneği sağlar.
- Not / trade-off: Point-in-time snapshot olduğundan son write'ları kaçırabilir ve finansal ledger yerine kullanılmaz.

### Redis
- Tanım: Memory-first key/value veri store'udur; TTL, hızlı counter ve geçici coordination için uygundur.
- FinWallet'ta kullanım: FinWallet OTP TTL, temporary counters, fraud velocity ve hot/transient support state için kullanır.
- Neden / çözdüğü problem: Düşük latency gerektiren geçici state'i MSSQL yükünden ayırır.
- Not / trade-off: Wallet balance veya ledger için authoritative değildir; Redis kaybı finansal truth kaybı olmamalıdır.

### Redis AOF
- Tanım: Redis write komutlarını append-only file'a yazarak restart sonrası state recover etmeye yarayan persistence modudur.
- FinWallet'ta kullanım: Docker Redis local ortamında transient support state'i daha dayanıklı tutmak için AOF etkinleştirilebilir.
- Neden / çözdüğü problem: Container restart sonrası bazı transient state'in kaybını azaltır.
- Not / trade-off: Redis yine financial source of truth olmaz; AOF MSSQL backup'ın alternatifi değildir.

### Rollback
- Tanım: Transaction içindeki değişiklikleri commit edilmeden geri almaktır.
- FinWallet'ta kullanım: Ledger imbalance, insufficient balance veya SQL error olduğunda financial posting rollback olur.
- Neden / çözdüğü problem: Partial DB state bırakılmasını engeller.
- Not / trade-off: Commit edilmiş dış provider hareketini rollback edemez; onun için compensation gerekir.

### Schema
- Tanım: Veritabanındaki tablo, kolon, constraint, index ve ilişkilerin yapısal tanımıdır.
- FinWallet'ta kullanım: FinWallet schema scriptleri finansal ve auth tablolarını versioned şekilde oluşturur.
- Neden / çözdüğü problem: Data correctness'i uygulama kodundan bağımsız DB kurallarıyla destekler.
- Not / trade-off: Schema değişikliği backward compatibility ve migration planı gerektirir.

### SchemaVersions
- Tanım: Hangi DB migration'larının uygulanmış olduğunu kaydeden version tablosu/pattern'idir.
- FinWallet'ta kullanım: Init job script uygulanmadan önce version kaydına bakar.
- Neden / çözdüğü problem: Container restart'ında migration'ın yanlışlıkla tekrar çalışmasını önler.
- Not / trade-off: Migration dosyası değiştirildiyse eski version kaydı bunu algılamaz; uygulanmış migration immutable tutulmalıdır.

### Serializable Isolation
- Tanım: SQL transaction'larda en güçlü standart isolation seviyelerinden biridir; concurrent sonucu serial execution'a yakın hale getirir.
- FinWallet'ta kullanım: Critical financial races için bazı atomic posting akışlarında kullanılabilir.
- Neden / çözdüğü problem: Overspend ve phantom-style race'leri güçlü biçimde sınırlar.
- Not / trade-off: Lock contention/deadlock maliyeti daha yüksektir; sadece gereken boundary'de kullanılmalıdır.

### Source of Truth
- Tanım: Bir veri için doğru kabul edilen authoritative sistemdir.
- FinWallet'ta kullanım: Finansal state için MSSQL; dış banka hareketinin dış dünyadaki truth'u için bank provider kendi alanında source of truth'tür.
- Neden / çözdüğü problem: Çelişen cache/provider snapshot'larında hangi sistemin esas olduğunu belirler.
- Not / trade-off: Birden fazla source of truth tanımlamak reconciliation ve ownership'i belirsiz hale getirir.

### Transient State
- Tanım: Kaybolması halinde finansal truth'u bozmayan kısa ömürlü/geçici veridir.
- FinWallet'ta kullanım: OTP TTL, fraud velocity counters veya cache-like bilgiler Redis'te tutulabilir.
- Neden / çözdüğü problem: Hızlı erişim sağlar ve durable DB'yi gereksiz yükten korur.
- Not / trade-off: Kaybolduğunda yeniden üretilebilmeli veya güvenli şekilde expire olabilmelidir.

### TTL (Time To Live)
- Tanım: Bir cache/key verisinin ne kadar süre sonra otomatik expire olacağını belirleyen süredir.
- FinWallet'ta kullanım: Registration OTP ve bazı transient Redis state'leri TTL ile sınırlanır.
- Neden / çözdüğü problem: Geçici doğrulama verisinin sonsuza kadar kalmasını engeller.
- Not / trade-off: TTL finansal transaction retention'ı değildir; durable financial data expire edilmez.

### Unique Constraint
- Tanım: Belirli kolon veya kolon kombinasyonunun duplicate olmasını DB seviyesinde engelleyen kuraldır.
- FinWallet'ta kullanım: Idempotency key, inbox Source+MessageId veya domain-specific uniqueness için kullanılır.
- Neden / çözdüğü problem: Race condition sırasında iki instance aynı kaydı oluşturmaya çalışsa bile tek winner sağlar.
- Not / trade-off: Sadece uygulama `SELECT then INSERT` kontrolüne güvenmekten daha güvenlidir.

### UTC / DateTimeOffset
- Tanım: Zaman bilgisini timezone bağımsız veya offset bilgili biçimde saklama yaklaşımıdır.
- FinWallet'ta kullanım: CreatedAt, CompletedAt, ReviewedAt gibi timestamps UTC/DateTimeOffset kullanır.
- Neden / çözdüğü problem: Farklı servis/ülke saatlerinde sıralama ve audit tutarlılığını artırır.
- Not / trade-off: Business cutoff gibi yerel takvim kuralları yine country/timezone context'iyle hesaplanmalıdır.

## 5. Güvenilirlik, Mesajlaşma ve Dağıtık Sistem (22 terim)

### At-Least-Once Delivery
- Tanım: Bir mesajın en az bir kez teslim edilmesini hedefleyen, duplicate teslimata izin veren messaging guarantee'dir.
- FinWallet'ta kullanım: Outbox worker hata durumunda mesajı retry eder; aynı message birden fazla delivery attempt görebilir.
- Neden / çözdüğü problem: Message loss yerine duplicate ihtimalini kabul eder.
- Not / trade-off: Exactly-once gibi davranış için idempotent consumer/deduplication gerekir.

### Background Worker / Hosted Service
- Tanım: HTTP request'ten bağımsız sürekli veya periyodik arka plan işi çalıştıran process bileşenidir.
- FinWallet'ta kullanım: `BankMoneyMovementBackgroundService` ve `OutboxDispatchBackgroundService` bu rolü taşır.
- Neden / çözdüğü problem: Uzun/pending provider akışlarını client request'ine bağlamadan ilerletir.
- Not / trade-off: Worker state memory'de değil durable DB'de olmalıdır ki restart güvenli olsun.

### Backoff
- Tanım: Retry'ler arasında bekleme süresini artırarak downstream servise sürekli yük bindirmemektir.
- FinWallet'ta kullanım: Outbox failure state sonraki denemeyi ileri zamana planlayabilir.
- Neden / çözdüğü problem: Dependency outage sırasında retry storm'u azaltır.
- Not / trade-off: Backoff tek başına retry limit/dead-letter kararının yerini almaz.

### Callback
- Tanım: Dış provider'ın bir işlemin durumunu daha sonra FinWallet'a bildirdiği server-to-server çağrıdır.
- FinWallet'ta kullanım: Internal bank callback endpoint'i Pending/Completed/Failed provider state'ini Inbox üzerinden işler.
- Neden / çözdüğü problem: Uzun süren provider işlemlerini client polling'e bağımlı bırakmaz.
- Not / trade-off: Callback authentication, dedupe ve replay-safe finalization şarttır.

### Claim / Lease
- Tanım: Bir worker'ın belirli işi geçici süreyle kendine ayırması ve diğer worker'ların aynı işi eşzamanlı almamasıdır.
- FinWallet'ta kullanım: Outbox dispatcher SQL atomik claim ile bir message batch'ini sahiplenir.
- Neden / çözdüğü problem: Multi-instance worker ortamında duplicate concurrent send riskini azaltır.
- Not / trade-off: Worker lease sonrası crash ederse row yeniden claim edilebilir olmalıdır.

### Compensating Transaction
- Tanım: Önceki finansal transaction'ı silmek yerine ters etkili yeni transaction yaratılmasıdır.
- FinWallet'ta kullanım: Refund ve Reversal original journal'ı mutate etmeden karşıt journal/transaction oluşturur.
- Neden / çözdüğü problem: Audit trail'i korur ve geçmişin immutable kalmasını sağlar.
- Not / trade-off: Her transaction türü aynı correction yöntemini kullanmaz; external bank operation provider compensation gerektirebilir.

### Compensation
- Tanım: Distributed akışta daha önce gerçekleşmiş bir etkinin karşıt business işlemiyle telafi edilmesidir.
- FinWallet'ta kullanım: Provider fail sonrası blocked balance release veya external movement sonrası local mismatch için correction/recovery tasarlanır.
- Neden / çözdüğü problem: DB rollback'in dış sistemdeki tamamlanmış işlemi geri alamadığı durumları yönetir.
- Not / trade-off: Compensation rollback değildir; yeni, audit edilebilir bir business action'dır.

### Deduplication
- Tanım: Aynı logical event/request/message'in ikinci kez etkili işlenmesini engellemektir.
- FinWallet'ta kullanım: Inbox unique key, idempotency key ve terminal state kontrolleri farklı katmanlarda dedupe sağlar.
- Neden / çözdüğü problem: Network retry ve provider duplicate'larını güvenli hale getirir.
- Not / trade-off: Dedupe key doğru seçilmezse farklı gerçek işlemler yanlışlıkla aynı kabul edilebilir.

### Eventual Consistency
- Tanım: Farklı sistemlerin aynı anda değil bir süre sonra tutarlı hale gelmesini kabul eden modeldir.
- FinWallet'ta kullanım: Para MSSQL'de commit olduktan sonra notification Outbox ile daha sonra gönderilebilir; provider callback/reconciliation da zaman farkıyla gelir.
- Neden / çözdüğü problem: Distributed transaction olmadan güvenilir entegrasyon sağlar.
- Not / trade-off: Hangi verinin anında consistent, hangisinin eventual olduğu açıkça tanımlanmalıdır.

### Exactly-Once Effect
- Tanım: Distributed sistemde mesaj birden çok kez teslim edilse bile business etkinin tek kez oluşması hedefidir.
- FinWallet'ta kullanım: FinWallet bunu gerçek 'exactly-once transport' yerine idempotency + Inbox + terminal-state guards ile yaklaşır.
- Neden / çözdüğü problem: Pratikte güvenli para hareketi sağlar.
- Not / trade-off: Network seviyesinde mutlak exactly-once iddiası yapılmaz; effect-level dedupe uygulanır.

### ExternalTransactionId
- Tanım: Dış bank/provider tarafından üretilen transaction kimliğidir.
- FinWallet'ta kullanım: Bank movement status query, callback dedupe/finalization ve reconciliation'da local FinancialTransaction ile ilişkilendirilir.
- Neden / çözdüğü problem: FinWallet transaction ID ile provider transaction ID'yi birbirinden ayırır.
- Not / trade-off: Correlation ID değildir; provider'ın business operation identity'sidir.

### Idempotency
- Tanım: Aynı mantıksal request birden fazla kez gönderildiğinde finansal etkinin yalnız bir kez uygulanması özelliğidir.
- FinWallet'ta kullanım: Transfer, Purchase, BankDeposit/Withdrawal ve correction komutlarında durable `Idempotency-Key` kullanılır.
- Neden / çözdüğü problem: Timeout/retry/network belirsizliğinde duplicate para hareketini engeller.
- Not / trade-off: Aynı key + aynı payload replay edilir; aynı key + farklı payload conflict olmalıdır.

### Idempotency-Key
- Tanım: Client'ın aynı finansal komutu tekrar gönderdiğini server'ın anlayabilmesi için verdiği stable request anahtarıdır.
- FinWallet'ta kullanım: Money-moving public POST endpoint'lerinde header olarak zorunludur.
- Neden / çözdüğü problem: Client retry ile yeni transaction yaratılması yerine önceki sonucu replay etmeyi sağlar.
- Not / trade-off: Correlation ID ile aynı değildir; request tekrar güvenliği için özel semantik taşır.

### Inbox Pattern
- Tanım: Dışarıdan gelen mesaj/callback'ı önce durable kaydedip unique message identity ile duplicate processing'i engelleyen pattern'dir.
- FinWallet'ta kullanım: FakeBank callback'ları Source + MessageId ile Inbox'ta dedupe edilir.
- Neden / çözdüğü problem: Provider aynı callback'i yüzlerce kez gönderse bile finansal finalization'ın tekrar uygulanmasını engeller.
- Not / trade-off: Consumer business operation'ın kendisi de replay-safe olmalıdır; Inbox tek başına crash aralığını tamamen çözmez.

### Non-Retryable Error
- Tanım: Aynı request'i tekrar denemenin anlamlı olmadığı terminal/business hatadır.
- FinWallet'ta kullanım: Invalid account, permanent provider reject veya insufficient funds gibi durumlarda operation Failed olur ve blocked amount release edilebilir.
- Neden / çözdüğü problem: Sonsuz retry ve stuck blocked-balance riskini önler.
- Not / trade-off: Yanlış classification availability kaybı veya duplicate effect riski yaratabilir.

### Outbox Pattern
- Tanım: Business transaction ile gönderilecek mesajı aynı database transaction içinde kaydedip dış gönderimi sonradan worker ile yapan reliability pattern'idir.
- FinWallet'ta kullanım: Purchase/Bank movement completion gibi akışlar notification Outbox row'u yazar; worker FakeCommunication'a yollar.
- Neden / çözdüğü problem: Para commit olup process crash olduğunda notification'ın tamamen kaybolmasını önler.
- Not / trade-off: Genellikle at-least-once delivery sağlar; consumer/message operation idempotent olmalıdır.

### Provider Idempotency Key
- Tanım: FinWallet'ın external provider'a aynı logical operation'ı tekrar gönderdiğinde duplicate side effect'i önlemek için kullandığı stable key'dir.
- FinWallet'ta kullanım: Bank account opening ve bank money movement request'leri provider requestKey taşır.
- Neden / çözdüğü problem: Client idempotency'den bağımsız olarak downstream duplicate protection sağlar.
- Not / trade-off: Provider gerçekten key semantiğini garanti etmiyorsa FinWallet blind retry yapmaz.

### Replay
- Tanım: Daha önce tamamlanmış idempotent işlemin aynı sonucunun yeniden döndürülmesidir.
- FinWallet'ta kullanım: Transfer/Purchase handler önce completed replay store'a bakar; fraud/provider tekrar çağrılmadan sonuç dönebilir.
- Neden / çözdüğü problem: Duplicate request'in para ve dış servis maliyetini yeniden üretmesini engeller.
- Not / trade-off: Replay response orijinal completion timestamp ve transaction identity'yi korumalıdır.

### Request Hash
- Tanım: Idempotency key ile birlikte payload'ın aynı olup olmadığını kanıtlamak için canonical request'ten üretilen hash'tir.
- FinWallet'ta kullanım: Fraud event/idempotency akışında source/destination/amount gibi alanlar canonicalize edilip SHA-256 hashlenebilir.
- Neden / çözdüğü problem: Aynı key'in farklı payload ile kötüye kullanılmasını yakalar.
- Not / trade-off: Canonicalization deterministic olmalıdır; field order/format farkı yanlış conflict üretmemelidir.

### Retry
- Tanım: Geçici hata sonrası aynı operasyonu tekrar denemektir.
- FinWallet'ta kullanım: Outbox communication ve retryable bank status processing kontrollü retry edilebilir.
- Neden / çözdüğü problem: Transient network/provider sorunlarında availability'yi artırır.
- Not / trade-off: Financial POST körlemesine retry edilmez; provider idempotency garanti edilmeden duplicate para hareketi riski vardır.

### Retryable Error
- Tanım: Aynı operasyon daha sonra tekrar denendiğinde başarılı olma ihtimali olan geçici hatadır.
- FinWallet'ta kullanım: Provider timeout/network interruption veya temporary communication outage buna örnek olabilir.
- Neden / çözdüğü problem: Worker'ın pending/backoff state'inde kalmasını sağlar.
- Not / trade-off: Business reject veya insufficient funds retryable değildir; error classification doğru yapılmalıdır.

### Terminal State
- Tanım: Bir transaction'ın artık normal lifecycle içinde başka business processing gerektirmeyen son durumudur.
- FinWallet'ta kullanım: Completed ve bazı Failed/Denied state'ler terminal kabul edilir.
- Neden / çözdüğü problem: Duplicate callback/retry geldiğinde yeniden para hareketi yapılmasını engelleyen guard sağlar.
- Not / trade-off: Manual correction terminal transaction üzerinde yeni child transaction yaratabilir; original state yine mutate edilmez.

## 6. Finans, Muhasebe ve Bankacılık (57 terim)

### Asset
- Tanım: Şirketin sahip olduğu veya kontrol ettiği ekonomik değerleri temsil eden muhasebe hesabı türüdür.
- FinWallet'ta kullanım: `BANK-SETTLEMENT:TRY` FinWallet ledger'ında bankacılık tarafındaki karşılık varlığını temsil eder.
- Neden / çözdüğü problem: Wallet liability'nin karşılığının nerede tutulduğunu muhasebe denkleminde görünür kılar.
- Not / trade-off: Mevcut FakeBank modelinde ayrı gerçek FinWallet omnibus hesabı tam modellenmemiştir; settlement asset şimdilik FinWallet muhasebe abstraction'ıdır.

### Atomic Posting
- Tanım: Posting'in tüm finansal bileşenlerinin tek DB transaction içinde ya hep ya hiç uygulanmasıdır.
- FinWallet'ta kullanım: Balance + FinancialTransaction + Journal/Entries + idempotency + gerektiğinde Outbox aynı commit'te tutulur.
- Neden / çözdüğü problem: Partial money movement ve ledger/balance ayrışmasını önler.
- Not / trade-off: External provider atomic posting kapsamına dahil değildir.

### Available Balance
- Tanım: Wallet'ta müşterinin hemen harcayabileceği/transfer edebileceği bakiyedir.
- FinWallet'ta kullanım: Transfer ve Purchase source wallet'tan available balance düşer.
- Neden / çözdüğü problem: Overspend kontrolünün temelidir.
- Not / trade-off: Pending external withdrawal gibi akışlarda para available'dan blocked'a taşınabilir.

### Bank Account
- Tanım: Dış banka/provider tarafında müşteriye ait gerçek/harici hesap modelidir.
- FinWallet'ta kullanım: FinWallet `BankAccount` kaydı FakeBank externalAccountId/IBAN/currency/status ile wallet'a bağlanır.
- Neden / çözdüğü problem: Bankadaki para ile FinWallet wallet bakiyesini ayrı modeller ve funding/withdrawal akışına boundary sağlar.
- Not / trade-off: BankAccount balance ile Wallet balance eşit olmak zorunda değildir.

### Bank Reference
- Tanım: Bank/provider işlemine ait external referans bilgisidir.
- FinWallet'ta kullanım: Transaction history/troubleshooting sırasında ExternalTransactionId veya provider-specific reference ile birlikte kullanılabilir.
- Neden / çözdüğü problem: Customer support/reconciliation'da iki sistemde aynı hareketi bulmayı kolaylaştırır.
- Not / trade-off: Internal transaction ID ile karıştırılmamalıdır.

### Bank Settlement Asset
- Tanım: FinWallet ledger'ında external bank tarafındaki müşteri fonu karşılığını temsil eden asset account abstraction'ıdır.
- FinWallet'ta kullanım: Bank->Wallet deposit'te debit edilir; Wallet->Bank withdrawal final olduğunda credit edilerek azalır.
- Neden / çözdüğü problem: Toplam wallet liability'nin bankacılık tarafındaki karşılığını muhasebe olarak izlemeyi sağlar.
- Not / trade-off: FakeBank'te ayrı FinWallet omnibus/safeguarding account henüz tam modellenmediğinden bu account gerçek provider hesabının birebir kaydı değildir.

### Bank Statement
- Tanım: Dış banka hesabındaki tamamlanmış hareketlerin kronolojik listesi veya ekstresidir.
- FinWallet'ta kullanım: `IBankProvider.GetStatementAsync` reconciliation için provider statement satırlarını getirir.
- Neden / çözdüğü problem: FinWallet local bank movement kayıtları ile dış provider truth'unu karşılaştırmayı sağlar.
- Not / trade-off: Statement query financial mutation yapmaz; reconciliation read source'tur.

### Bank-Settlement Reconciliation
- Tanım: Internal bank transaction kayıtlarının settlement ledger etkisiyle uyumunu kontrol etmektir.
- FinWallet'ta kullanım: Completed BankDeposit/Withdrawal transaction amount/status ile bank-settlement entries karşılaştırılır.
- Neden / çözdüğü problem: Local transaction state ile accounting state ayrışmasını yakalar.
- Not / trade-off: External provider statement reconciliation'dan farklı scope'tur.

### BankDeposit
- Tanım: FinWallet açısından dış banka hesabından müşterinin digital wallet'ına para yükleme işlemidir.
- FinWallet'ta kullanım: Client `POST /bank-movements/deposits` çağırır; provider adapter FakeBank tarafında customer bank account'tan para çekmek için provider `Withdrawal` yönüne map eder; local ledger settlement asset + wallet liability artırır.
- Neden / çözdüğü problem: Bankadaki parayı FinWallet içi kullanılabilir bakiyeye dönüştürür.
- Not / trade-off: İsim provider yönüyle karıştırılmamalıdır: FinWallet Deposit = wallet'a giriş; FakeBank'te aynı ekonomik hareket customer bank account'tan withdrawal'dır.

### BankWithdrawal
- Tanım: FinWallet açısından digital wallet'tan dış banka hesabına para gönderme işlemidir.
- FinWallet'ta kullanım: Amount önce available'dan blocked'a alınabilir; cutoff/provider işlenir; tamamlanınca wallet liability ve settlement asset azalır, provider tarafında customer bank account credit edilir.
- Neden / çözdüğü problem: Pending bank işlemi sırasında aynı paranın ikinci kez harcanmasını engeller.
- Not / trade-off: Provider failure'da blocked release/compensation doğru yapılmalıdır.

### Blocked Balance
- Tanım: Geçici olarak kullanıma kapatılmış fakat henüz final settlement ile wallet'tan tamamen çıkmamış bakiyedir.
- FinWallet'ta kullanım: Wallet->Bank withdrawal provider pending iken amount available'dan blocked'a taşınır.
- Neden / çözdüğü problem: Aynı paranın başka transfer/purchase ile ikinci kez harcanmasını engeller.
- Not / trade-off: Provider terminal failure verirse blocked amount release edilmelidir; aksi halde para stuck olur.

### Business Day / Business Calendar
- Tanım: Bankacılık işlemlerinin çalıştığı gün/tatil kurallarını tanımlayan takvimdir.
- FinWallet'ta kullanım: FakeCutoff weekend/holiday/processing date mantığını simüle eder.
- Neden / çözdüğü problem: Calendar-day ile bank-processing-day farkını doğru modellemeyi sağlar.
- Not / trade-off: Production'da resmi holiday calendar veri kaynağı ve timezone yönetimi gerekir.

### Campaign
- Tanım: Belirli merchant/customer/tutar koşullarında indirim uygulayan business kuralıdır.
- FinWallet'ta kullanım: FakeCampaign eligibility, discount amount ve sponsor type döndürür; accounting FinWallet'ta yapılır.
- Neden / çözdüğü problem: Promotion logic'ini finansal posting'den ayırır.
- Not / trade-off: Provider sadece hesaplama/karar verir; ledger'a yazma yetkisi yoktur.

### Campaign Expense
- Tanım: Platformun finanse ettiği kampanya indirimini şirket maliyeti olarak temsil eden expense account'ıdır.
- FinWallet'ta kullanım: Örneğin 200 TRY purchase, 20 TRY platform discount: customer liability debit 180, campaign expense debit 20, merchant payable credit 200.
- Neden / çözdüğü problem: İndirimin ekonomik kaynağını ledger'da açık tutar.
- Not / trade-off: Merchant sponsorlu discount'ta aynı expense hesabı kullanılmayabilir; payable net tutara göre oluşabilir.

### Credit
- Tanım: Double-entry muhasebede journal'ın sağ tarafındaki entry yönüdür.
- FinWallet'ta kullanım: Liability hesabını artırabilir, asset hesabını azaltabilir; BankDeposit'te customer wallet liability credit edilir.
- Neden / çözdüğü problem: Debit ile birlikte journal balance sağlar.
- Not / trade-off: Credit her zaman 'para geldi' anlamına gelmez; account türüne göre yorumlanır.

### Currency
- Tanım: Paranın hangi para biriminde olduğunu belirleyen kod/enum'dur.
- FinWallet'ta kullanım: TRY, USD, EUR wallet ve account'larda currency boundary oluşturur.
- Neden / çözdüğü problem: Cross-currency yanlış posting'i engeller.
- Not / trade-off: V1'de FX yoktur; farklı currency wallet'lar arasında otomatik dönüşüm yapılmaz.

### Customer Wallet Liability
- Tanım: Müşterinin wallet bakiyesini FinWallet'ın müşteriye borcu olarak temsil eden ledger liability account'ıdır.
- FinWallet'ta kullanım: BankDeposit'te credit ile artar; transfer source veya purchase'ta debit ile azalır.
- Neden / çözdüğü problem: Wallet balance'ın ekonomik anlamını muhasebe olarak doğru gösterir.
- Not / trade-off: Wallet table current balance ile ledger liability toplamının reconciliation ile uyumlu olması beklenir.

### Cutoff
- Tanım: Banka işleminin aynı iş gününde mi yoksa sonraki business day'de mi işleneceğini etkileyen saat/kural sınırıdır.
- FinWallet'ta kullanım: FakeCutoff business calendar ve transaction type'a göre karar verir.
- Neden / çözdüğü problem: After-hours bank withdrawal gibi işlemleri Scheduled state'e almayı sağlar.
- Not / trade-off: Cutoff sabit tek saat değildir; ülke/currency/işlem tipine göre değişebilir.

### Debit
- Tanım: Double-entry muhasebede journal'ın sol tarafındaki entry yönüdür.
- FinWallet'ta kullanım: Asset hesabını artırabilir, liability hesabını azaltabilir; örneğin BankDeposit'te settlement asset debit edilir, transfer source wallet liability debit edilerek azaltılır.
- Neden / çözdüğü problem: Account türüne göre ekonomik yönü açık şekilde modellemeyi sağlar.
- Not / trade-off: Debit her zaman 'para çıktı' demek değildir.

### Digital Wallet
- Tanım: FinWallet içinde müşterinin kullanabilir dijital para bakiyesini temsil eden iç finansal hesaptır.
- FinWallet'ta kullanım: Her wallet currency bazında AvailableBalance/BlockedBalance ve ledger liability ilişkisi taşır.
- Neden / çözdüğü problem: Transfer, Purchase, Refund gibi FinWallet içi para hareketlerinin hızlı ve bağımsız yürütülmesini sağlar.
- Not / trade-off: BankAccount ile aynı hesap değildir; wallet bakiyesi FinWallet'ın müşteriye karşı yükümlülüğünü temsil eder.

### Discount
- Tanım: Purchase original amount'tan düşülen kampanya tutarıdır.
- FinWallet'ta kullanım: OriginalAmount ve DiscountAmount transaction detail/history'de tutulabilir.
- Neden / çözdüğü problem: Customer'ın ödediği net tutar ile merchant'ın hak ettiği tutarı ayırır.
- Not / trade-off: İndirimi kimin finanse ettiği sponsor modeline göre ledger entry değişir.

### Double-Entry Bookkeeping
- Tanım: Her finansal olayın en az iki muhasebe hesabını etkilediği ve toplam debit = toplam credit olduğu muhasebe yöntemidir.
- FinWallet'ta kullanım: FinWallet transfer, bank deposit, purchase, refund/reversal posting'lerinde kullanır.
- Neden / çözdüğü problem: Para 'yoktan oluştu mu/kayboldu mu?' hatalarını balance equation ile yakalamayı sağlar.
- Not / trade-off: Debit/credit işareti 'iyi/kötü' veya basit artı/eksi değildir; account türüne göre anlam değişir.

### Expense
- Tanım: Şirketin belirli faaliyet nedeniyle katlandığı maliyeti temsil eden muhasebe hesap türüdür.
- FinWallet'ta kullanım: Platform sponsorlu campaign discount'ta `CAMPAIGN-EXPENSE` benzeri hesap debit edilebilir.
- Neden / çözdüğü problem: İndirimin merchant veya customer bakiyesinden gizlice düşmek yerine ekonomik sponsorunu açıklar.
- Not / trade-off: Campaign sponsor tipi accounting posting'i değiştirir.

### External Bank Statement Reconciliation
- Tanım: FinWallet local bank movement kayıtlarını FakeBank/gerçek bank statement ile karşılaştırmaktır.
- FinWallet'ta kullanım: ExternalTransactionId ve amount/status/reference üzerinden mismatch tespit edilir.
- Neden / çözdüğü problem: Provider'da tamamlanan ama local'de eksik veya tersi durumları bulur.
- Not / trade-off: Provider call SQL transaction dışında yapılır; result sonra durable issue/run'a yazılır.

### FinancialTransaction
- Tanım: Bir business para hareketinin lifecycle, amount, type, status ve referanslarını taşıyan durable işlem kaydıdır.
- FinWallet'ta kullanım: Transfer, BankDeposit, BankWithdrawal, Purchase, Refund, Reversal gibi tipler tek transaction modelinde izlenir.
- Neden / çözdüğü problem: API/business işlem kimliği ile muhasebe journal'ını birbirinden ayırarak lifecycle takibi sağlar.
- Not / trade-off: FinancialTransaction ledger'ın kendisi değildir; transaction 'ne oldu?', ledger 'muhasebe etkisi neydi?' sorusunu cevaplar.

### FinancialTransactionStatus
- Tanım: FinancialTransaction'ın lifecycle durumunu temsil eden state'tir.
- FinWallet'ta kullanım: Scheduled, Pending, Completed, Failed gibi durumlar bank/financial flows'da kullanılır.
- Neden / çözdüğü problem: Worker, callback ve history API'nin aynı transaction'ın hangi aşamada olduğunu anlamasını sağlar.
- Not / trade-off: Terminal state'ler tekrar posting'i engellemek için önemlidir.

### FinancialTransactionType
- Tanım: FinancialTransaction'ın hangi business hareketini temsil ettiğini belirleyen enum/type'tır.
- FinWallet'ta kullanım: WalletTransfer, BankDeposit, BankWithdrawal, Purchase, Refund, Reversal gibi değerler bulunur.
- Neden / çözdüğü problem: Posting/correction/history logic'inin işlem semantiğini anlamasını sağlar.
- Not / trade-off: DB numeric contract ile code enum değerleri uyumlu kalmalıdır.

### IBAN
- Tanım: Banka hesabını uluslararası standart formatta tanımlayan hesap numarasıdır.
- FinWallet'ta kullanım: FakeBank account açıldığında örnek IBAN üretir ve FinWallet BankAccount'a kaydedilir.
- Neden / çözdüğü problem: Dış banka hesabını user-facing/reference olarak tanımlar.
- Not / trade-off: Simulator IBAN'ı gerçek bankacılık clearing sistemine bağlı değildir.

### Ledger
- Tanım: FinWallet içindeki tüm finansal hareketleri hesaplar bazında debit/credit kayıtlarıyla immutable şekilde izleyen muhasebe defteridir.
- FinWallet'ta kullanım: Her completed financial posting bir LedgerJournal ve en az iki LedgerEntry üretir; toplam Debit toplam Credit'e eşit olmalıdır.
- Neden / çözdüğü problem: Sadece 'şu an bakiye kaç?' değil, 'bu bakiye nasıl oluştu?' sorusunun audit edilebilir cevabını verir.
- Not / trade-off: Wallet tablosu current projection'dır; Ledger geçmişin muhasebe kaynağıdır. Original entry silinmez, correction ters kayıtla yapılır.

### Ledger Account
- Tanım: Ledger içindeki debit/credit hareketlerinin toplandığı muhasebe hesabıdır.
- FinWallet'ta kullanım: `BANK-SETTLEMENT:TRY`, `WALLET-LIABILITY:<walletId>`, `MERCHANT-PAYABLE:<merchant>` gibi logical account'lar kullanılır.
- Neden / çözdüğü problem: Her para hareketinin hangi ekonomik hesaba ait olduğunu açıklar.
- Not / trade-off: Ledger Account, müşterinin BankAccount entity'siyle aynı kavram değildir.

### Ledger Entry
- Tanım: Bir LedgerJournal içindeki tek debit veya credit satırıdır.
- FinWallet'ta kullanım: Account, side (Debit/Credit), amount/currency ve reference bilgisi taşır.
- Neden / çözdüğü problem: Journal'ın ekonomik etkisini atomik hesap satırlarına ayırır.
- Not / trade-off: Entry tek başına transaction'ın tamamını anlatmaz; journal ile birlikte okunur.

### Ledger Journal
- Tanım: Tek bir finansal olayın tüm debit/credit entry'lerini birlikte gruplayan kayıt başlığıdır.
- FinWallet'ta kullanım: Transfer, deposit, purchase veya correction için bir journal oluşturulur; journal altındaki entries dengeli olmalıdır.
- Neden / çözdüğü problem: Bir işlemin muhasebe etkilerini tek business event olarak audit etmeyi sağlar.
- Not / trade-off: Journal mutate edilmez; reversal/refund yeni journal üretir.

### Liability
- Tanım: Şirketin başka bir tarafa karşı borç/yükümlülüğünü temsil eden muhasebe hesabı türüdür.
- FinWallet'ta kullanım: Müşterinin wallet bakiyesi `WALLET-LIABILITY:<wallet>` ile FinWallet'ın müşteriye karşı yükümlülüğü olarak modellenir.
- Neden / çözdüğü problem: Wallet bakiyesini şirketin 'kendi parası' gibi yanlış modellemeyi engeller.
- Not / trade-off: Liability arttığında normalde credit, azaldığında debit edilir.

### Merchant
- Tanım: Müşterinin ürün/hizmet satın aldığı ticari tarafı temsil eder.
- FinWallet'ta kullanım: Purchase request merchantId taşır; Campaign ve Fraud değerlendirmeleri merchant context kullanabilir.
- Neden / çözdüğü problem: Wallet harcamasının karşı tarafını ve payable hesabını belirler.
- Not / trade-off: V1 merchant onboarding/settlement sistemi tam kapsamlı ödeme kuruluşu modeli değildir.

### Merchant Payable
- Tanım: Purchase sonrası FinWallet'ın merchant'a borçlandığı tutarı temsil eden liability account'ıdır.
- FinWallet'ta kullanım: Purchase journal'ında net/gross campaign sponsor modeline göre merchant payable credit edilir.
- Neden / çözdüğü problem: Customer wallet debit'ini merchant settlement obligation'a dönüştürür.
- Not / trade-off: Merchant payable'ın gerçek bank settlement'ı v1'de ayrı operasyonel kapsam olabilir.

### Money
- Tanım: Amount ve Currency'yi tek domain değeri olarak birlikte taşıyan finansal Value Object'tur.
- FinWallet'ta kullanım: Transfer, purchase, bank movement ve fraud evaluation amount'ları Money semantiğiyle işlenir.
- Neden / çözdüğü problem: 100 TRY ile 100 USD'nin yanlışlıkla aynı değer gibi kullanılmasını engeller.
- Not / trade-off: Rounding/scale kuralları Money invariant'ının parçası olmalıdır.

### Negative Balance
- Tanım: Wallet available/blocked veya ilgili finansal account'un iş kuralına aykırı biçimde sıfırın altına düşmesidir.
- FinWallet'ta kullanım: Customer wallet'ta negatif/overspend v1 invariant olarak yasaktır.
- Neden / çözdüğü problem: Kredi/overdraft özelliği olmadığı için finansal doğruluğu sade tutar.
- Not / trade-off: İleride credit product eklenirse ayrı limit/loan modeline ihtiyaç olur; mevcut invariant sessizce değiştirilmemelidir.

### Omnibus Account
- Tanım: Bir fintech/payment kuruluşunun birçok müşterinin fonunu tek bank account'ta toplu tuttuğu hesap modelidir.
- FinWallet'ta kullanım: FinWallet'ta ayrı gerçek FakeBank omnibus account henüz tam modellenmemiştir; `BANK-SETTLEMENT` ledger asset bu ekonomik fikri muhasebe olarak temsil eder.
- Neden / çözdüğü problem: Müşteri başına fiziksel banka hesabı yerine iç ledger'ın müşteri dağılımını tutmasını açıklar.
- Not / trade-off: Production regülasyonu safeguarding/segregation ve legal ownership kuralları gerektirebilir.

### Original Transaction
- Tanım: Refund/Reversal gibi correction'ın referans verdiği ilk completed financial transaction'dır.
- FinWallet'ta kullanım: ParentTransactionId/correction checks original type/status/ownership üzerinden yapılır.
- Neden / çözdüğü problem: Correction'ın hangi ekonomik olayı terslediğini audit etmeyi sağlar.
- Not / trade-off: Original row status/history silinmez veya overwrite edilmez.

### Overspend
- Tanım: Wallet'ta mevcut kullanılabilir bakiyeden daha fazla harcama/transfer yapılmasıdır.
- FinWallet'ta kullanım: Atomic SQL balance check/lock ile engellenir; concurrent request'lerde de negatif bakiye oluşmamalıdır.
- Neden / çözdüğü problem: Müşterinin olmayan parayı harcamasını ve ledger/balance insolvency'yi önler.
- Not / trade-off: Sadece application `if(balance >= amount)` kontrolü race-safe değildir.

### Parent Transaction
- Tanım: Bir correction/child transaction'ın hangi önceki transaction'dan türediğini gösteren referanstır.
- FinWallet'ta kullanım: Refund/Reversal history response'unda ParentTransactionId bulunabilir.
- Neden / çözdüğü problem: Original ve compensating transaction zincirini görünür kılar.
- Not / trade-off: Parent relation accounting entry yerine geçmez; ledger journal'ları ayrıca dengeli olmalıdır.

### Payable
- Tanım: Şirketin bir merchant/üçüncü tarafa ödemesi gereken tutarı temsil eden liability türüdür.
- FinWallet'ta kullanım: Purchase posting'inde `MERCHANT-PAYABLE:<merchant>` credit ile artar.
- Neden / çözdüğü problem: Customer harcamasını merchant'a olan settlement yükümlülüğüne dönüştürür.
- Not / trade-off: Merchant'a gerçek bank transferi ayrı settlement process olabilir.

### Posting
- Tanım: Bir finansal business event'in wallet state ve double-entry ledger'a kalıcı olarak uygulanmasıdır.
- FinWallet'ta kullanım: Transfer posting source/destination balance, transaction, journal, entries ve idempotency sonucunu yazar.
- Neden / çözdüğü problem: Business kararını gerçek muhasebe state'ine dönüştürür.
- Not / trade-off: Fraud/provider external I/O posting transaction'ı açıkken yapılmaz.

### Processing Date
- Tanım: Bank işleminin operasyonda işleneceği business date'tir.
- FinWallet'ta kullanım: FakeCutoff country/currency/transaction/time bilgisine göre hesaplayabilir.
- Neden / çözdüğü problem: Request tarihi ile gerçek bank iş günü işlenişini ayırır.
- Not / trade-off: UTC timestamp ile aynı şey değildir; business calendar semantiği taşır.

### Provider Deposit / Withdrawal Direction
- Tanım: FakeBank hesabı açısından account bakiyesini artırma/azaltma yönleridir.
- FinWallet'ta kullanım: FakeBank `Deposit` bank account'u artırır; `Withdrawal` bank account'u azaltır. FinWallet BankDeposit bunun ters provider yönünü kullanabilir.
- Neden / çözdüğü problem: Aynı 'deposit' kelimesinin hangi sistem perspektifinden söylendiğini netleştirir.
- Not / trade-off: Adapter/ACL bu semantic farkı izole eder; domain'e provider enum'u sızdırılmaz.

### Purchase
- Tanım: Müşterinin wallet bakiyesini kullanarak merchant'a ödeme oluşturduğu finansal işlemdir.
- FinWallet'ta kullanım: Fraud + Campaign değerlendirmesinden sonra wallet liability azalır, merchant payable artar, campaign sponsoruna göre ek expense/netting entry oluşur.
- Neden / çözdüğü problem: Wallet harcamasını merchant'a olan ekonomik yükümlülüğe dönüştürür.
- Not / trade-off: Bank account her purchase'ta anında hareket etmek zorunda değildir; internal ledger settlement modelidir.

### Reconciliation
- Tanım: İki veya daha fazla finansal kayıt kaynağının beklenen/gerçek değerlerini karşılaştırıp farkları raporlama sürecidir.
- FinWallet'ta kullanım: Wallet<->Ledger, BankTransaction<->SettlementLedger ve FinWallet<->FakeBankStatement scope'ları vardır.
- Neden / çözdüğü problem: Silent drift, bug, missing callback veya provider mismatch'i tespit eder.
- Not / trade-off: FinWallet reconciliation otomatik bakiye düzeltmez; issue üretir ve finansal history'yi mutate etmez.

### Reconciliation Issue
- Tanım: Reconciliation sırasında bulunan mismatch'i temsil eden durable kayıtlı problemdir.
- FinWallet'ta kullanım: ExpectedAmount/ActualAmount, transaction/wallet/bank reference ve PII içermeyen details saklanabilir.
- Neden / çözdüğü problem: Farkı görünür ve takip edilebilir hale getirir.
- Not / trade-off: Issue çözümü manual investigation/correction gerektirebilir; sistem sessizce overwrite etmez.

### Reconciliation Run
- Tanım: Belirli scope için yapılan tek reconciliation çalışma örneğidir.
- FinWallet'ta kullanım: RunId, scope, status, started/completed time ve issue count durable kaydedilir.
- Neden / çözdüğü problem: Operasyon ekibinin hangi kontrolün ne zaman yapıldığını audit etmesini sağlar.
- Not / trade-off: Run sonucu otomatik correction anlamına gelmez.

### Refund
- Tanım: Completed Purchase'ın tamamını veya tanımlı kısmını müşteriye geri veren correction işlemidir.
- FinWallet'ta kullanım: FinWallet v1 full Purchase refund için original journal'ı silmeden opposite journal ve yeni FinancialTransaction oluşturur.
- Neden / çözdüğü problem: Audit trail'i koruyarak müşteri wallet liability'sini geri artırır ve merchant/campaign etkisini tersler.
- Not / trade-off: Refund original purchase'ı mutate etmez; parent transaction ilişkisiyle izlenir.

### Reversal
- Tanım: Hatalı/geri alınması gereken completed transaction'ın ekonomik etkisini tersleyen yeni transaction'dır.
- FinWallet'ta kullanım: Public reversal güvenli internal WalletTransfer için destination->source ters balance ve opposite ledger journal yaratır.
- Neden / çözdüğü problem: Immutable history korunurken yanlış para hareketi düzeltilebilir.
- Not / trade-off: External BankDeposit/Withdrawal doğrudan bu endpoint ile reverse edilmez; provider compensation gerekir.

### Safeguarding
- Tanım: Müşteri fonlarının şirketin kendi operasyonel fonlarından hukuken/operasyonel olarak ayrıştırılarak korunması yaklaşımıdır.
- FinWallet'ta kullanım: FinWallet v1 teknik simülasyonunda tam regülasyon modeli yoktur; settlement asset/liability ayrımı kavramsal temel sağlar.
- Neden / çözdüğü problem: Wallet bakiyesinin şirket geliri olmadığını ve karşılığının korunması gerektiğini açıklar.
- Not / trade-off: Gerçek ürün için ülke regülasyonu, lisans, bank partner ve legal accounting tasarımı gerekir.

### Settlement
- Tanım: Bir finansal işlemin taraflar arasındaki ekonomik yükümlülüğünün nihai olarak karşılanması sürecidir.
- FinWallet'ta kullanım: Bank movement processing/settlement date ve `BANK-SETTLEMENT` ledger account'ları bu kavramı kullanır.
- Neden / çözdüğü problem: Wallet içi book entry ile gerçek dış banka hareketini kavramsal olarak ayırır.
- Not / trade-off: Internal posting ile external bank settlement aynı anda olmak zorunda değildir; reconciliation gerekir.

### Settlement Date
- Tanım: Bir bank/financial işlemin ekonomik olarak nihai settlement'a ulaşacağı business date'tir.
- FinWallet'ta kullanım: Cutoff provider processing/settlement tarihleri döndürür ve transaction detail'de saklanabilir.
- Neden / çözdüğü problem: Pending/scheduled işlemin ne zaman tamamlanmasının beklendiğini açıklar.
- Not / trade-off: Gerçek provider callback zamanı settlement date'ten farklı olabilir; reconciliation yine gerekir.

### Sponsor
- Tanım: Kampanya indiriminin ekonomik maliyetini üstlenen tarafı belirtir.
- FinWallet'ta kullanım: Platform-sponsored ve merchant-sponsored senaryolarda posting farklıdır.
- Neden / çözdüğü problem: Discount'ın muhasebede hangi account'a yansıyacağını belirler.
- Not / trade-off: Sponsor bilgisi sadece UI etiketi değil accounting input'udur.

### Wallet Transfer
- Tanım: Bir FinWallet wallet'ından başka wallet'a aynı currency'de internal para hareketidir.
- FinWallet'ta kullanım: Fraud allow sonrası source liability debit, destination liability credit ve balance update tek atomic posting'de yapılır.
- Neden / çözdüğü problem: Banka çağrısı olmadan internal book transfer sağlar.
- Not / trade-off: Source ve destination currency uyumlu olmalı; overspend ve idempotency korunmalıdır.

### Wallet-Ledger Reconciliation
- Tanım: Wallet tablosundaki current balance ile ledger'dan türetilebilen ekonomik balance'ın karşılaştırılmasıdır.
- FinWallet'ta kullanım: Reconciliation scope'larından biridir.
- Neden / çözdüğü problem: Projection drift veya yanlış posting'i tespit eder.
- Not / trade-off: Mismatch bulunduğunda wallet doğrudan ledger'a eşitlenmez; issue oluşturulur.

## 7. Fraud ve Risk (17 terim)

### Allow
- Tanım: Fraud değerlendirmesinde işlemin devam etmesine izin veren karardır.
- FinWallet'ta kullanım: Durable fraud state Allow/Approved ise transfer/purchase atomic posting'e geçer.
- Neden / çözdüğü problem: Risk kontrolünü geçtikten sonra business flow'u sürdürür.
- Not / trade-off: Allow 'işlem kesinlikle fraud değildir' anlamına gelmez; mevcut sinyallerle kabul edilebilir risk demektir.

### Deny
- Tanım: Fraud değerlendirmesinde işlemin gerçekleştirilmemesi kararıdır.
- FinWallet'ta kullanım: Internal fraud doğrudan Deny verebilir veya combined policy final Deny üretebilir.
- Neden / çözdüğü problem: Riskli işlemin wallet/ledger posting'e geçmesini engeller.
- Not / trade-off: Deny reason code audit/operations için güvenli biçimde kaydedilmelidir; hassas model detail'i client'a sızdırılmamalıdır.

### External Fraud Provider
- Tanım: Fraud kararının bir kısmını dış servisten alan entegrasyon boundary'sidir.
- FinWallet'ta kullanım: FakeFraud v1 simulator olarak Allow/Review/Deny, score ve reason code döndürür.
- Neden / çözdüğü problem: FinWallet'ın kendi fraud kurallarını dış risk motorundan bağımsız tutar.
- Not / trade-off: Provider unavailable olduğunda protected flow fail-closed davranır.

### Fraud
- Tanım: Yetkisiz, kötü niyetli veya riskli finansal davranışı tespit/önleme problem alanıdır.
- FinWallet'ta kullanım: WalletTransfer ve Purchase öncesinde internal + external fraud değerlendirmesi çalışır.
- Neden / çözdüğü problem: Para hareketi gerçekleşmeden önce risk kararını business flow'a dahil eder.
- Not / trade-off: BankDeposit akışında mevcut v1 tasarımında fraud çağrısı yoktur; dokümanlarda sahte fraud adımı eklenmez.

### Fraud Decision
- Tanım: Risk değerlendirmesi sonucunda verilen Allow, Review veya Deny kararıdır.
- FinWallet'ta kullanım: Internal ve external decision policy ile final karar oluşturulur.
- Neden / çözdüğü problem: Posting'in devam edip etmeyeceğini açık state olarak belirler.
- Not / trade-off: Decision ile HTTP status aynı şey değildir; Review 202 gibi API davranışına map edilir.

### Fraud Decision Policy
- Tanım: Internal ve external fraud kararlarını tek final karara dönüştüren business policy'dir.
- FinWallet'ta kullanım: Örneğin herhangi bir Deny final Deny; Review kombinasyonları Review; yalnız güvenli kombinasyonlar Allow olabilir.
- Neden / çözdüğü problem: Karar birleştirmeyi handler içine dağılmış if'lerden çıkarır.
- Not / trade-off: Policy değişikliği risk appetite değişikliğidir ve test/audit gerektirir.

### Fraud Score
- Tanım: Risk seviyesini numeric olarak ifade eden provider/model çıktısıdır.
- FinWallet'ta kullanım: FakeFraud score döndürebilir; FinWallet final kararını yalnız score'a körlemesine bağlamaz.
- Neden / çözdüğü problem: Threshold/policy kararlarına ek sinyal sağlar.
- Not / trade-off: Score calibration model/provider'a özgüdür ve zaman içinde değişebilir.

### FraudEvent
- Tanım: Bir fraud değerlendirmesinin input identity, internal/external/final decision, reason code ve review state'ini durable saklayan kayıttır.
- FinWallet'ta kullanım: WalletTransfer/Purchase idempotent request'leri aynı fraud sonucunu replay etmek için FraudEvent kullanır.
- Neden / çözdüğü problem: Provider tekrar çağrısını ve decision kaybını önler; manual review audit trail sağlar.
- Not / trade-off: Raw sensitive payload/hash/PII internal response'larda gereksiz expose edilmez.

### Internal Fraud Engine
- Tanım: FinWallet'ın kendi server-side verileriyle deterministic/rule-based risk kararı üreten bileşenidir.
- FinWallet'ta kullanım: Amount, velocity, new-device, beneficiary/merchant familiarity gibi sinyalleri değerlendirir.
- Neden / çözdüğü problem: External provider'a gitmeden açık riskleri yakalar ve domain-specific kontrol sağlar.
- Not / trade-off: Rule engine tek başına production ML fraud sistemi değildir; gerçek veriyle tuning gerekir.

### Known Beneficiary
- Tanım: Transfer destination'ın müşteri tarafından daha önce kullanılan/tanınan beneficiary olup olmadığını belirten sinyaldir.
- FinWallet'ta kullanım: WalletTransfer risk context'inde kullanılabilir.
- Neden / çözdüğü problem: Yeni hedefe büyük transfer gibi daha riskli kombinasyonları ayırır.
- Not / trade-off: Beneficiary familiarity server-side history'den türetilmelidir; client boolean'ına güvenilmez.

### Manual Fraud Review
- Tanım: Pending FraudEvent'in bir internal operasyon/reviewer tarafından approve veya deny edilmesidir.
- FinWallet'ta kullanım: `/api/v1/internal/fraud-reviews` Gateway InternalService policy arkasında çalışır.
- Neden / çözdüğü problem: Human-in-the-loop risk kararını müşteri admin user tipi yaratmadan güvenli internal boundary'de tutar.
- Not / trade-off: Reviewer identity ve reviewedAt audit kaydı gerekir; karar bir kez final olmalıdır.

### Merchant Familiarity
- Tanım: Purchase merchant'ının müşteri geçmişinde tanıdık olup olmadığını belirten risk sinyalidir.
- FinWallet'ta kullanım: Purchase fraud evaluation'da beneficiary yerine merchant context kullanılabilir.
- Neden / çözdüğü problem: Yeni merchant + yüksek amount gibi pattern'leri değerlendirmeyi sağlar.
- Not / trade-off: Tek başına fraud kanıtı değildir; diğer sinyallerle combine edilir.

### New Device
- Tanım: Session/customer için daha önce görülmemiş device reference'ı olduğunu belirten risk sinyalidir.
- FinWallet'ta kullanım: Fraud context'te yeni cihaz işlemi daha riskli kabul edilebilir.
- Neden / çözdüğü problem: Credential ele geçirilmesi sonrası farklı cihazdan yapılan hareketi görünür kılar.
- Not / trade-off: Device fingerprint privacy ve spoofing riskleri nedeniyle tek başına deny sebebi olmamalıdır.

### Reason Code
- Tanım: Fraud veya business kararının nedenini machine-readable kısa kodla açıklayan değerdir.
- FinWallet'ta kullanım: Internal/external fraud reason code'ları normalize edilip FraudEvent'e kaydedilir.
- Neden / çözdüğü problem: Operations, analytics ve testlerde karar nedenini text parse etmeden anlamayı sağlar.
- Not / trade-off: Client'a model bypass edecek kadar ayrıntılı risk rule bilgisi verilmemelidir.

### Review
- Tanım: İşlemin otomatik allow/deny yerine manual review gerektirdiğini belirten fraud kararıdır.
- FinWallet'ta kullanım: FraudEvent durable Pending review state oluşturur ve public financial request 202 Accepted dönebilir.
- Neden / çözdüğü problem: Şüpheli ama kesin deny olmayan işlemleri para hareketi yapmadan bekletir.
- Not / trade-off: Review state durably saklanır; aynı idempotency request external fraud'u tekrar çağırmamalıdır.

### Risk Signal
- Tanım: Fraud değerlendirmesinde kullanılan server-side davranış/bağlam verisidir.
- FinWallet'ta kullanım: Transaction count, 24h amount, country, device, known beneficiary ve merchant familiarity örnekleridir.
- Neden / çözdüğü problem: Client'ın 'ben güvenliyim' demesine güvenmek yerine risk context'i server'da üretir.
- Not / trade-off: Signal quality yanlışsa false-positive/false-negative artar.

### Velocity
- Tanım: Kısa zaman aralığında yapılan işlem sayısı/tutar yoğunluğunu ölçen fraud sinyalidir.
- FinWallet'ta kullanım: Son 5 dakikadaki transaction count ve son 24 saatteki amount gibi ölçüler kullanılır.
- Neden / çözdüğü problem: Account takeover veya automated abuse gibi hızlı davranışları yakalamaya yardımcı olur.
- Not / trade-off: Redis counter transient olabilir; final financial truth değildir.

## 8. Performans ve Gözlemlenebilirlik (13 terim)

### Correlation / Traceability
- Tanım: Tek business flow'daki log, provider ve DB referanslarını birbirine bağlayabilme yeteneğidir.
- FinWallet'ta kullanım: CorrelationId, TransactionId, ExternalTransactionId, FraudReference ve Outbox/Inbox message ID birlikte kullanılır.
- Neden / çözdüğü problem: Distributed failure'ın hangi adımda oluştuğunu bulmayı kolaylaştırır.
- Not / trade-off: Tek bir universal ID her semantiği taşımaz; her reference'ın rolü ayrıdır.

### Health Endpoint
- Tanım: Orchestrator/proxy/operator'ın servisin durumunu HTTP ile kontrol ettiği endpoint'tir.
- FinWallet'ta kullanım: Docker smoke test ve YARP health mekanizmaları `/health/*` endpoint'lerini kullanır.
- Neden / çözdüğü problem: Startup ve routing otomasyonunda service availability sinyali sağlar.
- Not / trade-off: Health endpoint hassas dependency/secret detail'i expose etmemelidir.

### Latency
- Tanım: Bir request/operasyonun başlangıcından sonucuna kadar geçen süredir.
- FinWallet'ta kullanım: Gateway, provider HTTP, MSSQL ve Redis süreleri toplam API latency'yi oluşturur.
- Neden / çözdüğü problem: Kullanıcı deneyimi ve timeout tasarımı için temel metriktir.
- Not / trade-off: Sadece ortalama latency yeterli değildir; p95/p99 tail latency de izlenmelidir.

### Liveness
- Tanım: Process'in çalışıyor ve deadlock/crash durumda olmadığını gösteren health kavramıdır.
- FinWallet'ta kullanım: Gateway/API live endpoint'i container'ın restart gerektirip gerektirmediğine sinyal verebilir.
- Neden / çözdüğü problem: Dependency geçici bozuldu diye process'i gereksiz restart etmekten kaçınmayı sağlar.
- Not / trade-off: Readiness ile ayrılması production orchestrator davranışını iyileştirir.

### Log Rotation
- Tanım: Log dosyası/container logunun sınırsız büyümesini önlemek için boyut/sayı bazında döndürülmesidir.
- FinWallet'ta kullanım: Docker json-file logging için size/count sınırları compose'da verilir.
- Neden / çözdüğü problem: Disk dolması nedeniyle servis/host failure riskini azaltır.
- Not / trade-off: Central log retention'ın alternatifi değildir; local disk safety mekanizmasıdır.

### Logical Reads
- Tanım: SQL query'nin buffer cache üzerinden kaç data page okuduğunu gösteren maliyet göstergesidir.
- FinWallet'ta kullanım: Index/query tuning kararlarında elapsed time yanında değerlendirilir.
- Neden / çözdüğü problem: Fast test ortamında bile gereksiz scan'i görünür kılar.
- Not / trade-off: Tek başına yeterli değildir; CPU, wait, row count ve plan ile birlikte okunmalıdır.

### Observability
- Tanım: Sistemin iç durumunu log, metric, trace ve health sinyallerinden anlayabilme yeteneğidir.
- FinWallet'ta kullanım: FinWallet correlation IDs, structured logs, health endpoints, transaction/reconciliation records ile observability temeli kurar.
- Neden / çözdüğü problem: Incident ve performance analizinde 'neden oldu?' sorusunu cevaplamayı sağlar.
- Not / trade-off: Tam production observability için centralized log/metrics/tracing backend ayrıca gerekir.

### p95 / p99
- Tanım: Request sürelerinin yüzde 95/99'unun altında kaldığı latency percentile ölçüleridir.
- FinWallet'ta kullanım: Performance review'unda hot endpoint/provider/SQL için tail latency değerlendirmesi önerilir.
- Neden / çözdüğü problem: Az sayıdaki çok yavaş request'in ortalamada gizlenmesini önler.
- Not / trade-off: Percentile anlamlı olmak için yeterli sample ve doğru time-window gerektirir.

### Query Plan
- Tanım: SQL Server'ın bir query'yi hangi index/join/access yöntemleriyle çalıştıracağını gösteren execution plan'dır.
- FinWallet'ta kullanım: Performance tuning'de yeni index eklemeden önce plan ve logical reads incelenir.
- Neden / çözdüğü problem: Gerçek bottleneck'i tahmin yerine ölçmeye yardımcı olur.
- Not / trade-off: Plan environment/data distribution'a göre değişebilir; tek test database sonucu genellenmemelidir.

### Query Store
- Tanım: SQL Server'ın query performance history, plans ve runtime statistics toplama özelliğidir.
- FinWallet'ta kullanım: Production-like tuning'de regressions ve expensive query'leri bulmak için önerilir.
- Neden / çözdüğü problem: Yeni index veya query değişikliğinin gerçek etkisini ölçmeyi kolaylaştırır.
- Not / trade-off: Storage/retention ayarları yönetilmelidir; uygulama logging'in yerine geçmez.

### Readiness
- Tanım: Servisin yeni trafik almaya hazır olup olmadığını gösteren health kavramıdır.
- FinWallet'ta kullanım: MSSQL/Redis/schema init gibi dependency'ler hazır olmadan API'nin trafik almaması için kullanılabilir.
- Neden / çözdüğü problem: Startup race ve failed dependency durumunda request kaybını azaltır.
- Not / trade-off: Her dependency readiness'e dahil edilmemelidir; optional provider outage tüm servisi unready yapmak zorunda değildir.

### Structured Logging
- Tanım: Log mesajını serbest text yerine isimli alanlarla üretme yaklaşımıdır.
- FinWallet'ta kullanım: CorrelationId, TransactionId, path, status ve safe error code gibi alanlar structured log olarak yazılabilir.
- Neden / çözdüğü problem: Search/aggregation ve incident investigation'ı kolaylaştırır.
- Not / trade-off: Password, OTP, token, connection string ve gereksiz PII structured field olarak loglanmaz.

### Throughput
- Tanım: Bir sistemin belirli sürede işleyebildiği request/transaction sayısıdır.
- FinWallet'ta kullanım: Gateway, API, SQL pool ve provider capacity toplam throughput'u sınırlar.
- Neden / çözdüğü problem: Scale ve capacity planning için kullanılır.
- Not / trade-off: Yüksek throughput finansal correctness pahasına artırılmaz; lock/idempotency invariant'ları korunur.

## 9. Test ve Kalite (15 terim)

### Chaos Test
- Tanım: Dependency delay/failure/restart gibi arızaları bilinçli üreterek sistem recovery davranışını test eder.
- FinWallet'ta kullanım: Fake provider fail/delay/timeout modları, container restart ve communication outage senaryoları buna temel oluşturur.
- Neden / çözdüğü problem: Fail-closed, outbox retry, blocked release ve reconciliation davranışını doğrular.
- Not / trade-off: Production chaos test kontrollü blast radius ve gözlemlenebilirlik gerektirir.

### Concurrency Test
- Tanım: Aynı resource üzerinde eşzamanlı request'lerin invariant'ları bozmadığını doğrulayan testtir.
- FinWallet'ta kullanım: Örneğin 1000 balance için paralel iki 600 transferden yalnız birinin tamamlanması beklenir.
- Neden / çözdüğü problem: Overspend, idempotency race ve lock behavior'ını gerçek DB üzerinde kanıtlar.
- Not / trade-off: Thread-sleep tabanlı yapay test yerine gerçek parallel requests ve durable result doğrulaması gerekir.

### Definition of Done
- Tanım: Bir feature'ın 'tamamlandı' sayılması için gereken kriterler bütünüdür.
- FinWallet'ta kullanım: Kod yanında API, DB, security, test, docs, config ve runtime doğrulaması güncellenmelidir.
- Neden / çözdüğü problem: Kod merge olup doküman/test geride kalması problemini azaltır.
- Not / trade-off: Checklist yaşayan bir standarttır; proje büyüdükçe yeni kalite kapıları eklenebilir.

### End-to-End (E2E) Test
- Tanım: Client girişinden final durable sonucu kadar tüm kritik akışı gerçek component'lerle test eder.
- FinWallet'ta kullanım: Register -> login -> wallet -> bank movement -> transfer/purchase gibi happy/fail path'ler E2E olabilir.
- Neden / çözdüğü problem: Kullanıcı açısından sistemin gerçekten çalıştığını doğrular.
- Not / trade-off: Failure root cause'u unit test kadar hızlı lokalize etmez; daha az sayıda kritik senaryoya odaklanmalıdır.

### Fixture
- Tanım: Testin ihtiyaç duyduğu önceden hazırlanmış veri veya environment state'idir.
- FinWallet'ta kullanım: Integration/E2E senaryolarında customer, wallet, bank account ve balance seed fixture olabilir.
- Neden / çözdüğü problem: Tekrarlanabilir senaryolar sağlar.
- Not / trade-off: Finansal fixture doğrudan balance update ile ledger'ı bozmak yerine dengeli seed mekanizması kullanmalıdır.

### Integration Test
- Tanım: Birden fazla gerçek component/dependency'nin birlikte çalışmasını test eder.
- FinWallet'ta kullanım: Docker stack üzerinde Gateway, FinWallet.Api, MSSQL, Redis ve fake provider zinciri integration test hedefidir.
- Neden / çözdüğü problem: DI, config, serialization, SQL schema ve network boundary hatalarını yakalar.
- Not / trade-off: Unit testten daha yavaş ve environment'a bağımlıdır.

### Load Test
- Tanım: Sisteme kontrollü yüksek trafik vererek throughput, latency ve resource davranışını ölçen testtir.
- FinWallet'ta kullanım: Gateway/API/SQL/Redis pool ve rate-limit değerlerini production'a taşımadan önce doğrulamak için gerekir.
- Neden / çözdüğü problem: Config tuning'i tahmin yerine ölçüme bağlar.
- Not / trade-off: Financial test datası ve side effect'ler izole environment'ta tutulmalıdır.

### Mock
- Tanım: Gerçek dependency yerine testin kontrol ettiği davranış sağlayan test double'dır.
- FinWallet'ta kullanım: Moq ile `IBankProvider`, store veya external dependency unit testlerde mocklanabilir.
- Neden / çözdüğü problem: Failure/edge-case davranışını hızlı ve deterministik üretir.
- Not / trade-off: Mock gerçek provider/SQL behavior'ını kanıtlamaz; fazla mock test'i implementation'a bağımlı hale getirebilir.

### Moq
- Tanım: .NET unit testlerde interface/class mock üretmek için kullanılan mocking kütüphanesidir.
- FinWallet'ta kullanım: FinWallet test project'inde external/store dependency'leri izole etmek için kullanılır.
- Neden / çözdüğü problem: Setup/Verify ile expected interaction testini kolaylaştırır.
- Not / trade-off: Gerçek integration testin alternatifi değildir.

### Smoke Test
- Tanım: Deploy/build sonrası sistemin temel olarak ayağa kalkıp kritik dependency'lere erişebildiğini hızlı kontrol eden testtir.
- FinWallet'ta kullanım: Docker CI tüm service'leri başlatıp Gateway health, MSSQL schema ve Redis connectivity doğrular.
- Neden / çözdüğü problem: YAML/build doğru olsa bile runtime DI/config/startup problemlerini erken yakalar.
- Not / trade-off: Derin business correctness testi değildir.

### Strict Mock
- Tanım: Setup edilmeyen dependency çağrısı olduğunda testi fail eden mock davranışıdır.
- FinWallet'ta kullanım: Bazı handler testleri Moq Strict kullanarak beklenmeyen provider/store çağrısını yakalar.
- Neden / çözdüğü problem: Örneğin validation fail olduğunda bank provider'ın hiç çağrılmadığını kanıtlar.
- Not / trade-off: Çok sıkı testler refactor sırasında gereksiz kırılgan olabilir; business interaction için kullanılır.

### Test Double
- Tanım: Gerçek dependency yerine testte kullanılan fake/mock/stub benzeri genel terimdir.
- FinWallet'ta kullanım: Unit testte provider/store davranışlarını kontrol etmek için kullanılır.
- Neden / çözdüğü problem: Testi hızlı ve deterministik yapar.
- Not / trade-off: Hangi double türünün kullanıldığı test niyetini açık tutmalıdır.

### Unit Test
- Tanım: Tek sınıf veya küçük business behavior'ı dış dependency'lerden izole test eden test türüdür.
- FinWallet'ta kullanım: FinWallet.Application.Tests handler davranışlarını fake/mock dependency'lerle doğrular.
- Neden / çözdüğü problem: Hızlı feedback ve branch/error davranışını deterministik test etmeyi sağlar.
- Not / trade-off: Gerçek SQL locking, Redis atomicity veya Gateway routing'i kanıtlamaz.

### Warnings as Errors
- Tanım: Compiler warning'lerini CI'da build failure olarak değerlendirme yaklaşımıdır.
- FinWallet'ta kullanım: Release build `--warnaserror` ile çalıştırılır.
- Neden / çözdüğü problem: Yeni warning'lerin sessizce birikmesini önler.
- Not / trade-off: Third-party/generated warning'ler gerektiğinde kontrollü suppression gerektirebilir.

### xUnit
- Tanım: .NET için unit/integration test framework'üdür.
- FinWallet'ta kullanım: FinWallet.Application.Tests test runner/framework olarak xUnit v3 kullanır.
- Neden / çözdüğü problem: Fact/theory tabanlı test yazımı ve CI entegrasyonu sağlar.
- Not / trade-off: Test framework doğru test stratejisinin kendisi değildir; coverage ve scenario seçimi ayrıca tasarlanır.

## 10. Docker, CI/CD ve Konfigürasyon (25 terim)

### .env
- Tanım: Docker Compose/local development için environment variable değerlerini tutan yerel dosyadır.
- FinWallet'ta kullanım: `.env.example` version-control'dadır, gerçek `.env` gitignore içindedir.
- Neden / çözdüğü problem: Local developer'ın gerekli config'i kolay hazırlamasını sağlar.
- Not / trade-off: Production secret-management yöntemi değildir.

### appsettings.json
- Tanım: .NET configuration'ın temel JSON dosyasıdır.
- FinWallet'ta kullanım: Platform limitleri, provider URLs/timeouts, Redis/SQL tuning, Swagger ve Gateway config burada tanımlanır.
- Neden / çözdüğü problem: Operational değerleri koddan ayırır.
- Not / trade-off: Financial/cryptographic invariants config switch yapılmaz.

### appsettings.Production.json
- Tanım: Production environment için base appsettings'i override eden standart .NET config dosyasıdır.
- FinWallet'ta kullanım: Swagger default-off, production URL/security ve boş secret placeholders gibi değerler taşır.
- Neden / çözdüğü problem: Development ile production davranışını source-code if'lerine boğmadan ayırır.
- Not / trade-off: Secret değerleri dosyaya commit edilmez; external injection beklenir.

### BuildKit Cache
- Tanım: Docker build katman/restore çıktısını tekrar kullanarak sonraki image build'lerini hızlandıran cache mekanizmasıdır.
- FinWallet'ta kullanım: NuGet restore cache mount'ları .NET image build'lerinde kullanılır; parallel corruption riskine karşı locked sharing uygulanabilir.
- Neden / çözdüğü problem: CI build süresini azaltır.
- Not / trade-off: Cache correctness'ten önemli değildir; bozulursa clean build ile doğrulama yapılmalıdır.

### CI (Continuous Integration)
- Tanım: Her değişikliği otomatik build/test/validation ile ana dala girmeden kontrol etme pratiğidir.
- FinWallet'ta kullanım: FinWallet CI Release build, warnings-as-errors ve tests çalıştırır; Docker workflow runtime doğrular.
- Neden / çözdüğü problem: Compile/runtime/schema regression'larını erken yakalar.
- Not / trade-off: CI yeşil olması production correctness'in tümünü garanti etmez; kritik E2E/security/load testleri de gerekir.

### Compose Overlay
- Tanım: Ana compose config'e environment/debug-specific override ekleyen ek YAML dosyasıdır.
- FinWallet'ta kullanım: `compose.debug.yml` localhost debug portları açar; `compose.production.yml` production-like override sağlar.
- Neden / çözdüğü problem: Tek dosyada tüm environment conditional logic'i karıştırmadan config varyasyonu sağlar.
- Not / trade-off: Overlay sırası önemlidir; yanlış kombinasyon beklenmeyen port/setting açabilir.

### Configuration Precedence
- Tanım: Aynı config key'in birden fazla kaynaktan geldiğinde hangisinin kazanacağını belirleyen sıradır.
- FinWallet'ta kullanım: Genel olarak appsettings -> environment-specific appsettings -> environment variables/secret injection üst üste uygulanır.
- Neden / çözdüğü problem: Aynı image'ı environment bazlı configure etmeyi sağlar.
- Not / trade-off: Resolved config çıktısı secret içerebileceği için loglanmamalıdır.

### Container
- Tanım: Bir image'ın izole process, filesystem ve network namespace ile çalışan instance'ıdır.
- FinWallet'ta kullanım: Her FinWallet HTTP service ve MSSQL/Redis ayrı container olarak çalışır.
- Neden / çözdüğü problem: Dependency/version isolation ve hızlı recreate sağlar.
- Not / trade-off: Application container'ları stateless tasarlanır; durable data named volume/DB'dedir.

### Docker
- Tanım: Uygulama ve dependency'leri izole container image/runtime olarak çalıştırma platformudur.
- FinWallet'ta kullanım: Gateway, FinWallet.Api, fake provider'lar, MSSQL ve Redis Docker Compose ile ayağa kaldırılabilir.
- Neden / çözdüğü problem: Local/integration environment'ın tekrar üretilebilir olmasını sağlar.
- Not / trade-off: Container kullanmak production orchestration/HA sorunlarını otomatik çözmez.

### Docker Compose
- Tanım: Birden fazla container service, network ve volume'u tek YAML modelinde birlikte çalıştırma aracıdır.
- FinWallet'ta kullanım: `compose.yml` tüm FinWallet stack'ini tanımlar.
- Neden / çözdüğü problem: Local full-stack start/stop, dependency order ve config'i tek komutta yönetir.
- Not / trade-off: Production orchestrator'ın tam alternatifi olmak zorunda değildir.

### Docker DNS / Service Discovery
- Tanım: Compose network içindeki service isimlerinin otomatik hostname olarak çözülmesidir.
- FinWallet'ta kullanım: Gateway destination'ları `finwallet-api`, `fake-bank` gibi service name'leri kullanabilir.
- Neden / çözdüğü problem: Hardcoded container IP ihtiyacını kaldırır.
- Not / trade-off: Service name environment dışında geçerli değildir; config ortam bazlı yönetilir.

### Docker Network
- Tanım: Container'ların birbirini DNS/service name ile görebildiği izole sanal network'tür.
- FinWallet'ta kullanım: `finwallet-backend` HTTP servislerini; `finwallet-data` FinWallet.Api ile MSSQL/Redis'i bağlar.
- Neden / çözdüğü problem: DB/Redis'i public host network'e açmadan service connectivity sağlar.
- Not / trade-off: Network segmentation credential/auth yerine geçmez; service-key ve DB auth yine gerekir.

### Dockerfile
- Tanım: Bir container image'ın nasıl build edileceğini tanımlayan declarative dosyadır.
- FinWallet'ta kullanım: `docker/Dockerfile.webapi` Gateway/API/fake servisler için ortak multi-stage build kullanır.
- Neden / çözdüğü problem: Build adımlarını version-control altında tekrarlanabilir hale getirir.
- Not / trade-off: Runtime image mümkün olduğunca küçük/non-root tutulmalıdır.

### Environment Variable
- Tanım: Runtime config'i process environment üzerinden verme yöntemidir.
- FinWallet'ta kullanım: Connection string, password, JWT/service key ve tuning ayarları appsettings'i override edebilir.
- Neden / çözdüğü problem: Image/source değiştirmeden environment-specific config sağlar.
- Not / trade-off: Secret environment variable process inspection riskleri taşıyabilir; production secret store/orchestrator entegrasyonu tercih edilir.

### GitHub Actions
- Tanım: GitHub repository event'lerinde otomatik CI/workflow çalıştıran platformdur.
- FinWallet'ta kullanım: Restore/build/test, Docker validation/smoke ve PDF generation workflow'ları kullanılır.
- Neden / çözdüğü problem: PR'a reproducible kalite kapısı ve artifact üretimi sağlar.
- Not / trade-off: Workflow permission ve secret kullanımında least privilege uygulanmalıdır.

### Image
- Tanım: Container oluşturmak için kullanılan immutable filesystem/runtime paketidir.
- FinWallet'ta kullanım: .NET service image'ları multi-stage Dockerfile ile build edilir.
- Neden / çözdüğü problem: Aynı binary/runtime paketinin farklı environment'larda tutarlı çalışmasını sağlar.
- Not / trade-off: Secret veya mutable runtime data image içine bake edilmez.

### Multi-Stage Build
- Tanım: Build araçları ile final runtime image'ını farklı Docker stage'lerinde ayıran tekniktir.
- FinWallet'ta kullanım: SDK stage restore/build/publish yapar; final ASP.NET runtime stage yalnız publish output taşır.
- Neden / çözdüğü problem: Final image boyutunu ve attack surface'i azaltır.
- Not / trade-off: Build cache ve project copy sırası iyi tasarlanmazsa CI yavaşlayabilir.

### Named Volume
- Tanım: Docker'ın yaşam döngüsünü container'dan bağımsız yönettiği persistent storage alanıdır.
- FinWallet'ta kullanım: `finwallet_mssql_data`, `finwallet_mssql_backup`, `finwallet_redis_data` named volume'lardır.
- Neden / çözdüğü problem: Container recreate edildiğinde data'nın kalmasını sağlar.
- Not / trade-off: `docker compose down -v` volume'ları siler; destructive operasyon olarak dokümante edilir.

### Non-Root Container
- Tanım: Application process'inin container içinde root kullanıcıyla çalışmamasıdır.
- FinWallet'ta kullanım: .NET runtime image `USER app` yaklaşımıyla çalıştırılır.
- Neden / çözdüğü problem: Container escape/compromise durumunda privilege impact'i azaltır.
- Not / trade-off: Bazı filesystem/port işlemleri için permission planı gerekir.

### Read-Only Filesystem
- Tanım: Application container filesystem'inin runtime'da yazılamaz hale getirilmesi hardening yaklaşımıdır.
- FinWallet'ta kullanım: Stateless API container'larında mümkün olduğunca read-only root filesystem kullanılabilir.
- Neden / çözdüğü problem: Malware/config tampering ve accidental local state riskini azaltır.
- Not / trade-off: Temp/runtime write ihtiyacı olan path'ler ayrı tmpfs/volume gerektirebilir.

### Resource Limit
- Tanım: Container'ın CPU, memory ve PID kullanımına üst sınır koymaktır.
- FinWallet'ta kullanım: Compose application container'larında local safety baseline limitleri tanımlanabilir.
- Neden / çözdüğü problem: Tek bozuk servisin host resource'larını tüketmesini sınırlar.
- Not / trade-off: Production limitleri load test ve capacity ölçümüyle belirlenmelidir.

### SBOM (Software Bill of Materials)
- Tanım: Bir build/artifact içinde hangi software dependency ve versionların bulunduğunu listeleyen envanterdir.
- FinWallet'ta kullanım: Security dokümanında supply-chain improvement olarak değerlendirilir; v1'in temel runtime component'i değildir.
- Neden / çözdüğü problem: Vulnerability/incident sırasında hangi artifact'ın etkilendiğini hızlı bulmayı sağlar.
- Not / trade-off: SBOM üretmek vulnerability'yi otomatik çözmez; scan/remediation workflow'u gerekir.

### Supply Chain Security
- Tanım: Build/package/dependency zincirinin kötü niyetli veya vulnerable bileşenlerden korunmasıdır.
- FinWallet'ta kullanım: Central NuGet versions, explicit dependencies, CI restore/build/test ve planlanan vulnerability/SBOM kontrolleri bu alana girer.
- Neden / çözdüğü problem: Kod doğru olsa bile third-party dependency kaynaklı riskleri azaltır.
- Not / trade-off: Sadece package pinlemek yeterli değildir; provenance/vulnerability/update süreci gerekir.

### Vulnerability Scan
- Tanım: Dependency, container image veya code üzerinde bilinen güvenlik açıklarını otomatik kontrol etme işlemidir.
- FinWallet'ta kullanım: FinWallet CI hardening roadmap'ında supply-chain kalite kapısı olarak yer alabilir.
- Neden / çözdüğü problem: Known-CVE risklerini merge/deploy öncesi görünür kılar.
- Not / trade-off: False positive ve exploitability context'i için human review gerekebilir.

### Workflow Artifact
- Tanım: CI run sırasında üretilen ve indirilebilir saklanan dosya paketidir.
- FinWallet'ta kullanım: Generated TR/EN PDF setleri GitHub Actions artifact olarak da upload edilir.
- Neden / çözdüğü problem: Binary çıktıyı merge öncesi inspect/render etmeyi kolaylaştırır.
- Not / trade-off: Artifact retention geçicidir; final versioned PDF'ler repo içinde ayrıca commit edilir.

## 11. .NET ve Uygulama Kodlama (15 terim)

### .NET 8
- Tanım: FinWallet'ın hedef runtime/framework sürümüdür.
- FinWallet'ta kullanım: Tüm ana API ve fake provider projeleri .NET 8 ile build edilir.
- Neden / çözdüğü problem: Modern ASP.NET Core, built-in DI, rate limiting, HttpClientFactory ve container desteği sağlar.
- Not / trade-off: Framework upgrade'leri ayrı compatibility/test çalışması gerektirir.

### ASP.NET Core
- Tanım: .NET üzerinde HTTP API ve web uygulaması geliştirme framework'üdür.
- FinWallet'ta kullanım: FinWallet.Gateway, FinWallet.Api ve simulator API'leri ASP.NET Core kullanır.
- Neden / çözdüğü problem: Kestrel, middleware, authentication, controllers, DI ve config altyapısını sağlar.
- Not / trade-off: Business domain'i framework API'lerine bağımlı tutulmaz.

### async/await
- Tanım: .NET'te I/O beklerken thread'i bloklamadan asynchronous programlama yapma modelidir.
- FinWallet'ta kullanım: HTTP, SQL, Redis ve background worker I/O akışları async çalışır.
- Neden / çözdüğü problem: Yük altında thread-pool starvation riskini azaltır.
- Not / trade-off: Async, işlemi otomatik paralel veya transactional yapmaz.

### Built-in DI Container
- Tanım: ASP.NET Core'un kendi dependency injection container'ıdır.
- FinWallet'ta kullanım: FinWallet üçüncü parti DI container yerine `IServiceCollection` registration kullanır.
- Neden / çözdüğü problem: Dependency graph'i standart framework mekanizmasıyla yönetir.
- Not / trade-off: Çok ileri interception/child-container ihtiyacı olmadıkça ekstra container eklenmez.

### CancellationToken
- Tanım: .NET async operasyonlarında caller request iptalini alt dependency'lere ileten cancellation sinyalidir.
- FinWallet'ta kullanım: Controller -> handler -> SQL/Redis/HttpClient çağrılarına propagate edilir.
- Neden / çözdüğü problem: Client bağlantısı koptuğunda gereksiz I/O'nun devam etmesini azaltır.
- Not / trade-off: Committed financial transaction cancellation ile 'undo' edilmez; cancellation yalnız commit öncesi/ongoing work'u durdurur.

### Central Package Management
- Tanım: NuGet package versionlarının her csproj yerine merkezi dosyadan yönetilmesidir.
- FinWallet'ta kullanım: Directory.Packages.props gibi yapı ile xUnit, Moq, YARP, Swashbuckle vb. versiyonlar merkezi pinlenir.
- Neden / çözdüğü problem: Solution içinde version drift ve duplicate version tanımlarını azaltır.
- Not / trade-off: Major upgrade yine compatibility review gerektirir; merkezi pin otomatik güvenlik sağlamaz.

### Controller-Based API
- Tanım: ASP.NET Core endpoint'lerini attribute/routing kullanan Controller sınıflarıyla tanımlama yaklaşımıdır.
- FinWallet'ta kullanım: FinWallet Minimal API yerine controllers kullanır.
- Neden / çözdüğü problem: Büyük API surface'te response metadata, authorization ve organization'ı daha görünür tutar.
- Not / trade-off: Küçük endpointlerde daha fazla boilerplate olabilir.

### Exception Mapping
- Tanım: Application/domain exception'larını kontrollü HTTP status + machine code response'a dönüştürme katmanıdır.
- FinWallet'ta kullanım: Insufficient balance, fraud deny/review, idempotency conflict, provider unavailable gibi expected exception'lar merkezi API mapping ile ServiceResult'a çevrilir.
- Neden / çözdüğü problem: Business error'ın 500 internal error olarak sızmasını önler.
- Not / trade-off: Unexpected exception detay/stack trace client'a verilmez; loglanır ve generic 500 döner.

### Immutable Model
- Tanım: Oluşturulduktan sonra key financial history alanlarının değiştirilmemesi yaklaşımıdır.
- FinWallet'ta kullanım: Ledger entries/journals ve completed transaction history correction ile overwrite edilmez.
- Neden / çözdüğü problem: Audit trail ve reasoning'i korur.
- Not / trade-off: Current-state projection'lar (Wallet balance) mutable olabilir; history ile projection ayrımı önemlidir.

### Machine-Readable Error Code
- Tanım: Client/operation logic'in free-text message yerine stabil kodla hata türünü ayırt etmesini sağlayan string'dir.
- FinWallet'ta kullanım: `IDEMPOTENCY_CONFLICT`, `FRAUD_REVIEW_REQUIRED`, `RATE_LIMITED` gibi kodlar kullanılır.
- Neden / çözdüğü problem: Localization/message değişse bile client branching'i stabil tutar.
- Not / trade-off: Aynı code semantiği farklı endpointlerde çelişkili kullanılmamalıdır; error-code kataloğu bu nedenle tutulur.

### Middleware
- Tanım: HTTP request/response pipeline'ında controller'dan önce/sonra çalışan cross-cutting bileşendir.
- FinWallet'ta kullanım: Shared.Web security headers, method/content checks, correlation ve rate-limit pipeline davranışlarını uygular.
- Neden / çözdüğü problem: Her controller'da aynı platform kontrolünü tekrar yazmayı önler.
- Not / trade-off: Business use-case logic middleware'e taşınmaz.

### NuGet
- Tanım: .NET package/dependency dağıtım ekosistemidir.
- FinWallet'ta kullanım: YARP, Swashbuckle, Moq, xUnit ve SQL/Redis client gibi external packages NuGet üzerinden alınır.
- Neden / çözdüğü problem: Tekrar kullanılabilir library/tooling entegrasyonunu kolaylaştırır.
- Not / trade-off: Supply-chain riski nedeniyle package sayısı, lisans ve vulnerability durumu kontrol edilmelidir.

### TimeProvider
- Tanım: .NET'te zamanı doğrudan `DateTime.UtcNow` çağırmak yerine inject edilebilir abstraction üzerinden alma mekanizmasıdır.
- FinWallet'ta kullanım: Fraud evaluation/review timestamp gibi Application logic'inde kullanılır.
- Neden / çözdüğü problem: Time-dependent testleri deterministik hale getirir.
- Not / trade-off: DB/provider authoritative time ile application time farkları ayrıca düşünülmelidir.

### Typed HttpClient
- Tanım: Belirli provider için HttpClient configuration ve adapter logic'ini strongly-typed class ile birleştiren .NET pattern'idir.
- FinWallet'ta kullanım: Bank/Fraud/Cutoff/Campaign/Communication provider client'ları typed HttpClient olarak DI'a bağlanır.
- Neden / çözdüğü problem: Base URL, timeout, handler ve provider API çağrılarını tek boundary'de tutar.
- Not / trade-off: Client sınıfı domain business orchestration yapmamalıdır.

### XML Documentation Comments
- Tanım: C# public type/method/property'lerde IDE/Swagger/doküman için kullanılan `/// <summary>` yorum formatıdır.
- FinWallet'ta kullanım: FinWallet public class/interface/metodlarda TR ve EN açıklama standardı uygular.
- Neden / çözdüğü problem: Yeni geliştiricinin API/code intent'ini hızlı anlamasını sağlar.
- Not / trade-off: Yorum kodla güncel tutulmazsa yanlış bilgi üretir; Definition of Done'a dahildir.

## 12. Bilinçli Olarak Kullanılmayan / Yerine Başka Yaklaşım Seçilen Terimler (11 terim)

### ASP.NET Core Identity
- Tanım: Microsoft'un hazır user, password, token, role ve auth persistence framework'üdür.
- FinWallet'ta kullanım: FinWallet custom auth/session/refresh tasarımı nedeniyle kullanılmaz.
- Neden / çözdüğü problem: Proje bankacılık benzeri custom session, refresh rotation, OTP ve explicit schema davranışını öğrenme/kontrol amacıyla kendisi uygular.
- Not / trade-off: Gerçek production üründe hazır ve battle-tested identity solution kullanmak bazı riskleri azaltabilir; custom auth daha fazla security sorumluluğu getirir.

### Blind Retry
- Tanım: Operasyonun side effect/idempotency semantiğini bilmeden otomatik tekrar denemektir.
- FinWallet'ta kullanım: Financial provider POST'larında kullanılmaz.
- Neden / çözdüğü problem: İlk request provider'da başarılı ama response kaybolmuşsa ikinci retry duplicate para hareketi yaratabilir.
- Not / trade-off: Retry ancak stable provider request key/idempotency ve operation status semantics ile güvenli hale getirilir.

### CQRS
- Tanım: Command/write modeli ile query/read modelini ayrı model veya pipeline'lara bölme yaklaşımıdır.
- FinWallet'ta kullanım: FinWallet tam CQRS framework uygulamaz; bazı write handler ve read history handler ayrımları doğal olarak vardır.
- Neden / çözdüğü problem: V1'de ekstra mediator/read-store complexity yerine sade Application handlers tercih edilir.
- Not / trade-off: İleride read scale veya model divergence artarsa CQRS daha anlamlı olabilir.

### Direct Balance UPDATE
- Tanım: Test veya operasyon kolaylığı için wallet balance kolonunu ledger/transaction olmadan elle değiştirmektir.
- FinWallet'ta kullanım: FinWallet happy-path dokümanlarında özellikle yapılmaması belirtilir.
- Neden / çözdüğü problem: Wallet projection ile Ledger'ı ayırarak reconciliation mismatch ve açıklanamayan para yaratır.
- Not / trade-off: Test funding bile dengeli provider/ledger fixture veya resmi financial posting akışıyla yapılmalıdır.

### Distributed Transaction
- Tanım: Birden fazla database/service'in tek atomik commit gibi koordine edilmesi problemidir.
- FinWallet'ta kullanım: FinWallet external HTTP çağrısını MSSQL transaction içine almaz ve 2PC benzeri distributed transaction kurmaz.
- Neden / çözdüğü problem: Lock/availability/operational complexity'i azaltmak için local ACID + idempotency + compensation + reconciliation kullanılır.
- Not / trade-off: Dış sistemle tam eşzamanlı atomicity yerine explicit eventual consistency kabul edilir.

### Event Sourcing
- Tanım: Current state'i doğrudan saklamak yerine tüm domain event'lerini source of truth olarak saklayıp state'i event'lerden türetme yaklaşımıdır.
- FinWallet'ta kullanım: FinWallet'ta kullanılmaz; immutable double-entry Ledger history vardır ama domain'in tamamı Event Sourcing değildir.
- Neden / çözdüğü problem: Ledger auditability sağlarken Wallet current projection ve normal relational tables pratik sorgu/işletim kolaylığı sağlar.
- Not / trade-off: Ledger ile Event Sourcing aynı şey değildir.

### Generic Repository
- Tanım: Her entity için aynı CRUD interface'ini sunan genel repository abstraction'ıdır.
- FinWallet'ta kullanım: FinWallet finansal store'larda kullanılmaz; use-case'e özel `IWalletTransferPostingStore`, `IFraudEventStore` gibi boundary'ler tercih edilir.
- Neden / çözdüğü problem: Transaction/locking/idempotency gibi financial semantics'i generic CRUD arkasında gizlememeyi sağlar.
- Not / trade-off: Basit CRUD alanlarında generic abstraction mümkün olsa da financial hot-path için uygun görülmemiştir.

### MediatR
- Tanım: .NET'te request/handler dispatch için yaygın mediator kütüphanesidir.
- FinWallet'ta kullanım: FinWallet bilinçli olarak kullanmaz; Controller handler'ı DI üzerinden doğrudan çağırır.
- Neden / çözdüğü problem: Call path'i görünür tutar ve gereksiz abstraction/package katmanını azaltır.
- Not / trade-off: Cross-cutting pipeline ihtiyacı büyürse mediator tekrar değerlendirilebilir.

### Microservices
- Tanım: Her domain/servisin bağımsız deploy/scale edilebildiği dağıtık mimari yaklaşımıdır.
- FinWallet'ta kullanım: FinWallet finansal çekirdekte v1 için bilinçli olarak kullanılmaz; fake external providers ayrı HTTP servisleri olarak tutulur.
- Neden / çözdüğü problem: Wallet/Ledger/Transaction gibi aynı atomik commit gerektiren alanları erken bölmemek distributed consistency maliyetini azaltır.
- Not / trade-off: İleride bağımsız scale/team ownership ihtiyacı gerçek olduğunda sınırlar yeniden değerlendirilebilir.

### Redis as Financial Source of Truth
- Tanım: Wallet/ledger gibi kalıcı finansal state'i sadece Redis'te tutma yaklaşımıdır.
- FinWallet'ta kullanım: FinWallet bilinçli olarak kullanmaz.
- Neden / çözdüğü problem: Redis restart/eviction/operational behavior'ının para doğruluğunu belirlemesini engellemek için MSSQL authoritative tutulur.
- Not / trade-off: Redis performans destek katmanıdır; financial truth ve audit history değildir.

### Two-Phase Commit (2PC)
- Tanım: Birden fazla resource manager'ın prepare/commit aşamalarıyla distributed transaction koordine ettiği protokoldür.
- FinWallet'ta kullanım: FinWallet v1 bank/provider entegrasyonunda kullanılmaz.
- Neden / çözdüğü problem: External provider'ların 2PC desteklememesi ve availability/complexity maliyeti nedeniyle local transaction pattern'leri tercih edilir.
- Not / trade-off: Banking entegrasyonlarında çoğu gerçek HTTP provider zaten distributed XA participant değildir.


---

## English

## Purpose

This document is a comprehensive glossary of the main technical and financial terms used in the FinWallet architecture, technical design, source code and maintained project documents numbered 00-25.

Each term answers four questions:
- Definition: What does the term mean generally?
- FinWallet usage: Where and how is it used in this project?
- Why: Which problem does it solve?
- Note / trade-off: What can be misunderstood, what does it cost, or what alternative exists?

The glossary contains 314 terms. Terms in the "deliberately avoided" section are also included because they appear in the documentation as architecture-decision rationale.

## Quick Architecture and Technical Decision Map

| Choice / Technique | What is it used for in FinWallet? | Why was it chosen? |
|---|---|---|
| Modular Monolith | Keep the financial core in one deploy/database transaction boundary | Preserve Wallet/Ledger/Transaction atomicity and avoid premature distributed transactions |
| Layered + pragmatic Clean Architecture | Separate HTTP, use cases, domain rules and infrastructure | Readability, testability and technology-independent domain logic |
| DDD-lite | Model the financial language directly | Express Money, Wallet, Ledger and FraudDecision as real business concepts |
| Ports + Adapters / ACL | Isolate provider-specific contracts from Application/Domain | Allow provider changes without contaminating core rules |
| YARP API Gateway | Provide one public entry point with JWT, routing, rate limits and load balancing | Hide backend topology and centralize edge controls |
| Defense in Depth | Layer Gateway, API and internal-service validation | Reduce gateway-bypass and single-control failure risk |
| MSSQL | Act as the durable financial source of truth | ACID transactions, locking, constraints and audit history |
| Redis | Hold only transient/TTL/counter state | Provide low-latency OTP and fraud-velocity support |
| Double-Entry Ledger | Record every money movement as debit/credit entries | Explain source/destination of value and enforce Debit=Credit |
| Wallet Current Balance | Provide a fast current projection | Avoid summing the entire ledger on every request |
| Durable Idempotency | Prevent duplicate financial effects under retries/timeouts | Ensure the same command cannot move money twice |
| Outbox | Atomically store messages with committed business state | Prevent post-commit message loss after crashes |
| Inbox | Deduplicate provider callbacks | Prevent repeated callbacks from applying money twice |
| Internal + External Fraud | Combine server-side risk rules with provider risk decisions | Control transfer/purchase risk before posting |
| Manual Fraud Review | Pause suspicious operations before money movement | Human-in-the-loop Allow/Deny |
| Cutoff / Business Calendar | Schedule bank processing according to business days | Handle after-hours/holiday withdrawals correctly |
| Blocked Balance | Reserve funds during pending wallet-to-bank movement | Prevent double spending of pending funds |
| Compensation / Reversal / Refund | Correct committed effects using new opposite transactions | Preserve immutable audit history |
| Reconciliation | Compare local wallet/ledger/bank/provider records | Detect silent drift, missing callbacks and mismatches |
| Keyset Pagination | Read transaction history without large OFFSET scans | Stable and efficient pagination on large tables |
| HttpClientFactory + connection pooling | Reuse provider connections safely | Reduce socket exhaustion and latency |
| Structured Logging + Correlation ID | Trace distributed flows through logs | Support incident investigation and latency analysis |
| Docker Compose | Run the entire stack reproducibly for local/integration work | Start Gateway + APIs + MSSQL + Redis + fake providers together |
| Named Volumes | Keep MSSQL/Redis state independent of container recreation | Local persistence and backup convenience |
| GitHub Actions CI | Automate build/test/Docker/PDF validation | Catch regressions before merge |
| xUnit + Moq | Unit-test Application behavior quickly | Validate business branches and expected dependency interactions |
| Real Docker Smoke/Integration Tests | Validate DI, config, SQL schema and networking on the real stack | Catch 'builds but does not run' failures |

## Core Financial Concepts - Short Examples

### Why are Wallet and BankAccount separate?

```text
Before:
Customer FakeBank account : 5,000 TRY
FinWallet Wallet           :     0 TRY

After funding 1,000 TRY from bank to wallet:
Customer FakeBank account : 4,000 TRY
FinWallet Wallet           : 1,000 TRY
```

A Wallet is not a copy of the same row in the bank. BankAccount represents the external bank account; Wallet represents the customer's spendable balance inside FinWallet and FinWallet's obligation to that customer.

### What is a Ledger?

The Ledger is the immutable accounting history that explains why money moved and between which economic accounts through debit/credit entries. The Wallet table answers "what can be spent now?"; the Ledger explains how that balance came to exist.

Example: funding 1,000 TRY from bank to wallet:

```text
Debit   BANK-SETTLEMENT:TRY               1,000
Credit  WALLET-LIABILITY:<walletId>       1,000
------------------------------------------------
Total Debit                               1,000
Total Credit                              1,000
```

Economic meaning:
- Settlement Asset +1,000: FinWallet's bank-side backing asset increased.
- Customer Wallet Liability +1,000: FinWallet now owes the customer 1,000 TRY of wallet value.

### Wallet-to-Wallet Transfer example

Ali sends Ayse 300 TRY:

```text
Debit   WALLET-LIABILITY:ALI              300
Credit  WALLET-LIABILITY:AYSE             300
------------------------------------------------
Debit = Credit
```

The source liability decreases by debit and the destination liability increases by credit. No bank call is required because this is an internal book transfer.

### Purchase + Platform Campaign example

Item price is 200 TRY, platform sponsors a 20 TRY discount and the customer pays 180 TRY:

```text
Debit   WALLET-LIABILITY:CUSTOMER         180
Debit   CAMPAIGN-EXPENSE                   20
Credit  MERCHANT-PAYABLE                  200
------------------------------------------------
Total Debit                               200
Total Credit                              200
```

This explicitly answers "where did the extra 20 TRY come from?": the platform funded it as an expense.

### Why are Refund and Reversal not UPDATE statements?

The original transaction and journal are never deleted or overwritten. A new opposite FinancialTransaction and journal are created.

```text
Original Purchase
        |
        +--> immutable history

Refund
        |
        +--> new transaction
        +--> new journal reversing the economic effect
```

This preserves the audit trail.

### Why is Idempotency critical in a financial system?

The client sends a transfer, the server moves money, but the response is lost on the network. If the client retries, money must not move twice.

```text
Idempotency-Key: transfer-abc-001

1st request -> operation Completed
2nd same key + same payload -> previous result replayed
same key + different payload -> Conflict
```

### Why do Outbox and Inbox exist?

```text
Outbox:
Financial DB COMMIT
    |
    +--> Outbox message stored in the same transaction
    |
    +--> Worker sends SMS/notification later

Inbox:
Provider callback
    |
    +--> deduplicate by Source + MessageId
    |
    +--> even 100 duplicate callbacks do not apply the financial effect twice
```

### Why does Reconciliation not automatically fix balances?

Reconciliation is a discrepancy-detection mechanism, not a silent balance-overwrite mechanism.

```text
Wallet current balance
        vs
Ledger-derived balance

FinWallet bank movement
        vs
FakeBank statement
```

A discrepancy creates a ReconciliationIssue. Financial history is not silently changed.

## Glossary

## 1. Architecture and Design (32 terms)

### Adapter
- Definition: A concrete implementation that connects a port to a specific technology or protocol.
- FinWallet usage: The FakeBank HTTP adapter maps `IBankProvider` to REST calls.
- Why / problem solved: It allows provider contracts to change without forcing Application changes.
- Note / trade-off: Validation and DTO mapping belong in the adapter; provider types should not leak into Domain.

### Aggregate
- Definition: A DDD concept representing a consistency/transaction boundary around related entities and value objects.
- FinWallet usage: FinWallet does not use a heavyweight aggregate framework, but Wallet/Transaction/Ledger posting rules share one consistency boundary.
- Why / problem solved: It helps reason about which state must change atomically.
- Note / trade-off: The concept is applied lightly; not every table is treated as an aggregate.

### Aggregate Root
- Definition: The main entity through which an aggregate is accessed.
- FinWallet usage: FinWallet does not enforce a heavy aggregate-root framework, but Wallet behavior is protected around its identity and invariants.
- Why / problem solved: It provides controlled entry to aggregate consistency rules.
- Note / trade-off: The term is used lightly because the project has no formal aggregate-root hierarchy.

### Anti-Corruption Layer (ACL)
- Definition: A translation layer that isolates an external system's model from the internal domain model.
- FinWallet usage: Bank, Fraud, Cutoff and Campaign responses are converted into FinWallet types inside Infrastructure.
- Why / problem solved: It prevents external naming, enums and error semantics from corrupting the domain.
- Note / trade-off: It adds mapping code but substantially reduces integration coupling.

### Append-Only
- Definition: A data model where new history records are appended and prior records are not overwritten or deleted.
- FinWallet usage: Ledger journals/entries and correction history follow this principle.
- Why / problem solved: It preserves auditability and prevents rewriting financial history.
- Note / trade-off: Current-state tables do not need to be append-only; history and projection serve different purposes.

### Audit Trail
- Definition: A record allowing the history of business/financial state changes to be traced back to events and actors.
- FinWallet usage: FinancialTransaction, Ledger, FraudEvent, reconciliation and review metadata together form an audit trail.
- Why / problem solved: It supports incident, dispute and financial investigation.
- Note / trade-off: Audit records are not the same as debug logs and may require stronger retention/integrity controls.

### Bounded Context
- Definition: A business boundary within which terms have a consistent meaning.
- FinWallet usage: FinWallet's financial core and FakeBank/FakeFraud/FakeCampaign provider models are treated as separate semantic boundaries.
- Why / problem solved: It prevents provider DTOs from leaking directly into the domain model.
- Note / trade-off: In v1 these are conceptual boundaries protected by adapters, not a full microservice bounded-context organization.

### Clean Architecture
- Definition: A set of architectural principles where dependencies point inward toward business rules.
- FinWallet usage: Domain knows no SQL/HTTP/framework details; Application defines ports and Infrastructure implements them.
- Why / problem solved: It keeps business rules technology-independent and improves testability.
- Note / trade-off: FinWallet is a pragmatic simplified Clean Architecture, not a maximal academic implementation.

### Consistency Boundary
- Definition: The boundary of data that must remain correct together during an operation.
- FinWallet usage: Wallet balance, FinancialTransaction, Ledger, durable idempotency and sometimes Outbox are committed in one MSSQL transaction.
- Why / problem solved: It prevents partial financial commits.
- Note / trade-off: External provider state lies outside this boundary, which is why compensation and reconciliation are needed.

### DDD-lite
- Definition: A pragmatic use of useful DDD modeling ideas without heavyweight process or infrastructure.
- FinWallet usage: FinWallet uses value objects, entities, invariants and domain language but not full Event Sourcing or a large domain-event bus.
- Why / problem solved: It keeps the financial domain expressive while limiting over-engineering.
- Note / trade-off: More complex future domains may require stronger DDD mechanisms.

### Defense in Depth
- Definition: Applying multiple layers of security controls instead of relying on a single control.
- FinWallet usage: Gateway validates JWT while FinWallet.Api independently validates JWT/session/ownership and service credentials.
- Why / problem solved: If one layer is bypassed, another can still reject the request.
- Note / trade-off: It is not blind duplication; each layer protects a different trust boundary.

### Dependency Injection (DI)
- Definition: Providing a class's dependencies from the outside.
- FinWallet usage: ASP.NET Core built-in DI registers handlers, stores, providers, TimeProvider and background services.
- Why / problem solved: It removes object construction from business classes and improves testability/runtime composition.
- Note / trade-off: Missing registrations may fail only at startup or first resolution, so runtime smoke testing is important.

### Dependency Inversion Principle (DIP)
- Definition: The principle that high-level business logic depends on abstractions rather than low-level details.
- FinWallet usage: Application depends on interfaces such as `IBankProvider`, stores and fraud-provider ports; Infrastructure supplies implementations.
- Why / problem solved: It makes provider substitution and unit-test mocking easier.
- Note / trade-off: It does not mean creating an interface for every class; FinWallet uses it at real boundaries.

### Dependency Rule
- Definition: The rule that inner layers must not depend on outer layers.
- FinWallet usage: FinWallet keeps `Api -> Application -> Domain` and `Infrastructure -> Application/Domain` dependency directions.
- Why / problem solved: It prevents Domain from depending on SQL, Redis or provider DTOs.
- Note / trade-off: Breaking the rule couples core business logic to technology choices.

### Domain-Driven Design (DDD)
- Definition: An approach that models software around real business concepts and rules.
- FinWallet usage: Wallet, Money, Currency, FinancialTransaction, Ledger and FraudDecision are direct domain concepts.
- Why / problem solved: It reduces semantic drift between code and the finance/banking domain.
- Note / trade-off: FinWallet uses DDD-lite; it avoids heavyweight aggregate/event-sourcing infrastructure.

### Entity
- Definition: A domain object with identity that can change state over time.
- FinWallet usage: Wallet, BankAccount and FinancialTransaction are identity-based entities.
- Why / problem solved: It keeps lifecycle and rules for the same business identity together.
- Note / trade-off: Entity equality is identity-oriented, not just value-oriented.

### Handler
- Definition: An Application class that orchestrates a use-case command or query.
- FinWallet usage: Classes such as `ExecuteWalletTransferHandler` and `ExecuteFraudProtectedPurchaseHandler` serve this role.
- Why / problem solved: It keeps controllers thin and separates business flow from HTTP.
- Note / trade-off: Handlers are invoked directly through DI; MediatR is not used.

### Horizontal Scaling
- Definition: Increasing capacity by running more instances/replicas of the same service.
- FinWallet usage: YARP clusters can contain multiple FinWallet.Api or provider replicas.
- Why / problem solved: It reduces single-instance bottlenecks.
- Note / trade-off: Correctness must rely on DB constraints, idempotency and concurrency rules rather than instance-local state.

### Invariant
- Definition: A business rule that must remain true regardless of execution path.
- FinWallet usage: `Debit == Credit`, no negative wallet balance and no double correction are invariants.
- Why / problem solved: It makes financial correctness a model/data property rather than an endpoint convention.
- Note / trade-off: It is not a runtime toggle; changing an invariant may require data/accounting migration.

### Layered Architecture
- Definition: An architecture that separates responsibilities into layers.
- FinWallet usage: FinWallet uses separate Api, Application, Domain and Infrastructure projects.
- Why / problem solved: It prevents HTTP, use-case, domain-rule and infrastructure concerns from becoming mixed together.
- Note / trade-off: Too many abstractions can cause over-engineering; the project intentionally keeps the layers small.

### Modular Monolith
- Definition: An architectural style where domain areas are separated into modules inside one deployable application.
- FinWallet usage: FinWallet keeps Wallet, BankAccount, Transaction, Ledger, Fraud and Reconciliation modules inside the same application and MSSQL transaction boundary.
- Why / problem solved: It reduces the need for distributed transactions while preserving domain separation and atomic financial commits.
- Note / trade-off: It does not provide microservice-level independent deployment; v1 prioritizes financial correctness and simplicity.

### Orchestration
- Definition: Coordinating the ordered steps of a use case.
- FinWallet usage: The transfer handler manages replay -> risk signals -> internal fraud -> external fraud -> durable fraud -> posting.
- Why / problem solved: It is critical for keeping external I/O and DB transaction boundaries correct.
- Note / trade-off: The orchestrator should coordinate, not absorb every business rule itself.

### Port
- Definition: An interface/boundary defined by Application to interact with the outside world.
- FinWallet usage: `IBankProvider`, `IExternalFraudProvider` and store interfaces are ports.
- Why / problem solved: It describes the capability Application needs without choosing a technology.
- Note / trade-off: The implementation lives in Infrastructure.

### Projection
- Definition: A current read-friendly view derived from history or business state.
- FinWallet usage: Wallet AvailableBalance/BlockedBalance can be viewed as current state reconciled against ledger history.
- Why / problem solved: It avoids summing the entire ledger on every request.
- Note / trade-off: A projection can drift from its source history, which is why reconciliation is required.

### Separation of Concerns
- Definition: The principle of keeping different responsibilities out of the same class or layer.
- FinWallet usage: HTTP mapping lives in Controllers, orchestration in Application, rules in Domain and SQL/HTTP clients in Infrastructure.
- Why / problem solved: It reduces change impact and improves readability.
- Note / trade-off: Over-fragmentation is also costly; FinWallet uses relatively coarse practical boundaries.

### State Machine / Lifecycle
- Definition: A model defining allowed states and transitions for an entity or transaction.
- FinWallet usage: FinancialTransaction and bank-movement flows use states such as Scheduled, Pending, Completed and Failed.
- Why / problem solved: It prevents invalid transitions and makes retry/callback behavior deterministic.
- Note / trade-off: State names represent domain lifecycle, not merely API response labels.

### Stateless Service
- Definition: A service that does not keep durable business truth in process/container memory.
- FinWallet usage: Gateway, FinWallet.Api and fake HTTP services can be recreated; durable financial state lives in MSSQL.
- Why / problem solved: It enables safer horizontal scaling and container restarts.
- Note / trade-off: Redis/cache may hold transient state, but financial truth is not tied to process memory.

### Topology
- Definition: The runtime arrangement of services, network paths, data stores and trust boundaries.
- FinWallet usage: `25-topology.md` describes Gateway, API, fake providers, MSSQL, Redis, workers and Docker networks.
- Why / problem solved: It explains actual traffic/dependency flow rather than only source-code structure.
- Note / trade-off: Development and production topologies may differ in replicas and edge infrastructure.

### Transaction Boundary
- Definition: The design decision defining where a database transaction starts and ends.
- FinWallet usage: FinWallet never keeps a SQL transaction open during external HTTP; it opens a short transaction after the provider result.
- Why / problem solved: This reduces lock duration, deadlock risk and connection-pool pressure.
- Note / trade-off: It does not provide distributed atomicity; external mismatches are handled through reconciliation.

### Trust Boundary
- Definition: A security boundary where data or identity crossing from one side is not automatically trusted.
- FinWallet usage: Client->Gateway, Gateway->FinWallet.Api, FinWallet.Api->Gateway provider route and Gateway->provider are separate trust boundaries.
- Why / problem solved: It reduces risks such as gateway bypass and credential reuse.
- Note / trade-off: Each boundary must perform its own validation.

### Use Case
- Definition: An application behavior that fulfills one user or system goal.
- FinWallet usage: Register, Login, CreateWallet, BankDeposit, Transfer, Purchase, Refund and Reconciliation are use cases.
- Why / problem solved: It centralizes orchestration between controllers and domain/persistence details.
- Note / trade-off: A use case does not have to map one-to-one to an HTTP endpoint.

### Value Object
- Definition: An immutable domain object whose value and validation rules matter more than identity.
- FinWallet usage: `Money` and currency concepts are modeled this way.
- Why / problem solved: It keeps amount/currency together and prevents invalid monetary values from entering the domain.
- Note / trade-off: Value objects should remain small and immutable.

## 2. API, Gateway and Networking (32 terms)

### Active Health Check
- Definition: A proxy-driven periodic probe of backend destinations.
- FinWallet usage: YARP can actively check FinWallet/FakeBank/FakeFraud destinations.
- Why / problem solved: It detects failures even when no user traffic is flowing.
- Note / trade-off: Excessive probing adds load, so intervals/timeouts are configurable.

### API (Application Programming Interface)
- Definition: A contract defining how software can be invoked by other software.
- FinWallet usage: FinWallet exposes public customer APIs and internal provider APIs over HTTP.
- Why / problem solved: It provides a stable contract between clients and business logic.
- Note / trade-off: The API contract should not mirror the domain model directly; DTOs and ServiceResult preserve the boundary.

### API Gateway
- Definition: An edge application that provides a shared entry point to backend services.
- FinWallet usage: FinWallet.Gateway performs JWT validation, routing, rate limits, service-key policies and load balancing.
- Why / problem solved: It hides backend topology from clients and centralizes shared edge policies.
- Note / trade-off: Domain business logic does not belong in the Gateway.

### Cluster
- Definition: A YARP group containing one or more destinations for the same logical backend.
- FinWallet usage: FinWallet.Api and each fake provider are configured as separate clusters.
- Why / problem solved: It enables replicas, load balancing and health policies.
- Note / trade-off: A single development destination is still useful because the cluster model supports production scaling.

### Connection Keep-Alive
- Definition: Reusing the same TCP/HTTP connection for multiple requests.
- FinWallet usage: FinWallet controls it through Kestrel and outbound HttpClient lifetime settings.
- Why / problem solved: It reduces handshake cost and latency.
- Note / trade-off: Excessively long lifetimes can cause stale connections or delayed DNS changes.

### Content-Type
- Definition: An HTTP header describing the format of the request body.
- FinWallet usage: FinWallet requires `application/json` for write requests.
- Why / problem solved: It reduces ambiguous or unexpected payload parsing.
- Note / trade-off: The requirement does not apply to requests without a body.

### Controller
- Definition: An ASP.NET Core class that receives HTTP requests and delegates to Application use cases.
- FinWallet usage: FinWallet uses controller-based APIs rather than Minimal APIs.
- Why / problem solved: It separates HTTP concerns from business orchestration and provides clear Swagger metadata.
- Note / trade-off: Business logic should not accumulate inside controllers.

### Correlation ID
- Definition: A unique reference propagated across services to trace one request flow.
- FinWallet usage: `X-Correlation-Id` travels through Gateway, API and provider calls and is regenerated if invalid.
- Why / problem solved: It correlates distributed logs for the same operation.
- Note / trade-off: It is not an idempotency key or FinancialTransactionId and provides no duplicate protection.

### CORS
- Definition: A browser policy controlling which cross-origin requests are permitted.
- FinWallet usage: Allowed origins are configured as an appsettings allow-list.
- Why / problem solved: It limits browser-based cross-origin access.
- Note / trade-off: CORS is not authentication and does not block non-browser clients.

### Destination
- Definition: A concrete backend instance address inside a YARP cluster.
- FinWallet usage: For example, `http://finwallet-api:8080` can be a Docker destination.
- Why / problem solved: It is the actual service instance selected by the load balancer.
- Note / trade-off: An unhealthy destination can be removed from traffic based on health checks.

### DTO (Data Transfer Object)
- Definition: An object used to transfer data across layers or system boundaries.
- FinWallet usage: Request/response models and FakeBank/FakeFraud provider contracts are DTOs.
- Why / problem solved: It prevents direct exposure of domain entities.
- Note / trade-off: DTOs add validation/mapping work but make boundaries explicit.

### Endpoint
- Definition: An API operation addressed by a specific HTTP method and URL combination.
- FinWallet usage: `POST /api/v1/transfers` and `GET /api/v1/transactions` are examples.
- Why / problem solved: It is the external entry point to a use case.
- Note / trade-off: Endpoints should map requests/responses rather than contain core business logic.

### Fixed-Window Rate Limiter
- Definition: A rate-limit algorithm that divides time into fixed windows with a permit count per window.
- FinWallet usage: Shared.Web uses this model for the global limiter.
- Why / problem solved: It is simple, inexpensive and configuration-driven.
- Note / trade-off: Bursts can occur at window boundaries; sliding-window or token-bucket models may suit stricter needs.

### Health Check
- Definition: A mechanism that determines whether a service is fit to receive traffic.
- FinWallet usage: Gateway/Compose use health endpoints and provider health checks.
- Why / problem solved: It reduces traffic to broken instances and validates startup dependencies.
- Note / trade-off: Health design should distinguish process liveness from dependency readiness when appropriate.

### HTTP Status Code
- Definition: A standardized numeric result of an HTTP response.
- FinWallet usage: FinWallet uses statuses such as 200, 202, 400, 401, 403, 404, 409, 422, 429 and 503.
- Why / problem solved: It standardizes transport-level outcome while machine codes provide business detail.
- Note / trade-off: The same HTTP status can contain different business codes; clients should not rely on status alone.

### HttpClientFactory
- Definition: .NET infrastructure for centrally managing HttpClient lifetimes and handler pooling.
- FinWallet usage: Bank, Fraud, Cutoff, Campaign and Communication typed clients are created through it.
- Why / problem solved: It reduces socket exhaustion and incorrect HttpClient lifetime patterns.
- Note / trade-off: It does not automatically make financial POST retries safe; retry semantics are designed separately.

### JSON
- Definition: A text-based data format commonly used by HTTP APIs.
- FinWallet usage: FinWallet requires `application/json` for write requests.
- Why / problem solved: It provides easy cross-platform serialization and debugging.
- Note / trade-off: Financial decimals and date formats still need controlled contract semantics.

### Kestrel
- Definition: ASP.NET Core's HTTP server.
- FinWallet usage: Shared.Web configures request-body/header, connection, keep-alive and header-timeout limits through Kestrel.
- Why / problem solved: It bounds resource consumption and header/body abuse at the application level.
- Note / trade-off: It does not replace edge protection and can run behind a reverse proxy/load balancer.

### Load Balancing
- Definition: Distributing requests across multiple backend instances.
- FinWallet usage: YARP can distribute production traffic across replicas in the same cluster.
- Why / problem solved: It improves capacity and availability.
- Note / trade-off: Financial correctness must not depend on sticky sessions; durable state belongs in MSSQL.

### OpenAPI
- Definition: A machine-readable specification for describing REST API contracts.
- FinWallet usage: Swagger tooling produces an OpenAPI document behind the scenes, while Swagger is the user-facing term in the project.
- Why / problem solved: It enables tooling, client generation and schema discovery.
- Note / trade-off: FinWallet's domain architecture does not depend on OpenAPI.

### Passive Health Check
- Definition: Health evaluation based on failures observed during real traffic.
- FinWallet usage: A FinWallet API destination may be temporarily deactivated after transport failures.
- Why / problem solved: It uses real request failures without extra probes.
- Note / trade-off: Low traffic can delay detection, so it is stronger when combined with active checks.

### PowerOfTwoChoices
- Definition: A load-balancing algorithm that samples two candidates and selects the less-loaded one.
- FinWallet usage: It is the preferred YARP policy for FinWallet clusters.
- Why / problem solved: It can provide better load distribution than simple round-robin with little overhead.
- Note / trade-off: With one destination there is no meaningful choice; it matters when replicas are added.

### Rate Limiting
- Definition: Limiting how many requests are accepted within a period.
- FinWallet usage: Gateway applies per-IP fixed-window limits and backend can retain a defense-in-depth limit.
- Why / problem solved: It reduces resource exhaustion, brute force and simple L7 abuse.
- Note / trade-off: It is not volumetric DDoS protection; edge DDoS/WAF controls are still required.

### REST
- Definition: A web-API style that uses HTTP resource and verb semantics.
- FinWallet usage: FinWallet controller endpoints use HTTP methods such as GET/POST with JSON payloads.
- Why / problem solved: It provides a simple, widely supported integration model.
- Note / trade-off: FinWallet uses pragmatic REST rather than a strict HATEOAS-oriented academic implementation.

### Retry-After
- Definition: An HTTP response header telling the client when it may retry.
- FinWallet usage: FinWallet adds it to rate-limit rejections when available.
- Why / problem solved: It reduces immediate repeated retries from clients.
- Note / trade-off: Retries of financial POSTs still depend on idempotency and provider semantics.

### Reverse Proxy
- Definition: An HTTP intermediary that receives a request and forwards it to a backend service.
- FinWallet usage: Gateway forwards public `/api/*` traffic to FinWallet.Api and `/providers/*` traffic to fake providers.
- Why / problem solved: It hides backend addresses and centralizes traffic controls.
- Note / trade-off: The proxy must not become a business source of truth; financial state is not stored in Gateway.

### Route
- Definition: A rule determining which backend cluster receives an incoming path.
- FinWallet usage: FinWallet has separate YARP routes for public auth, protected APIs, internal callbacks and `/providers/*`.
- Why / problem solved: It controls path-level routing and authorization policy selection.
- Note / trade-off: Incorrect precedence can route internal endpoints through the wrong authorization policy.

### ServiceResult<T>
- Definition: FinWallet's common envelope for consistent success/failure responses.
- FinWallet usage: It carries fields such as `isSuccess`, `code`, `message`, `data` and `errors` across APIs.
- Why / problem solved: It prevents clients from parsing a different error shape for every endpoint.
- Note / trade-off: It does not replace HTTP status; transport status and ServiceResult code are used together.

### SocketsHttpHandler
- Definition: The low-level .NET HTTP handler controlling connection pools, DNS/lifetimes and transport behavior.
- FinWallet usage: FinWallet configures PooledConnectionLifetime, idle timeout and MaxConnectionsPerServer.
- Why / problem solved: It balances connection reuse with DNS refresh in long-running services.
- Note / trade-off: Values should not be increased aggressively without measurement.

### Swagger
- Definition: An interactive interface for discovering API endpoints, request/response schemas and contracts.
- FinWallet usage: Gateway, FinWallet.Api and all fake APIs expose it through Shared.Web/Swashbuckle.
- Why / problem solved: It improves developer onboarding, manual testing and contract visibility.
- Note / trade-off: It is disabled by default in production and never bypasses endpoint authorization.

### Timeout
- Definition: A time limit preventing an I/O operation from waiting indefinitely.
- FinWallet usage: Provider HttpClients, YARP activity and request-header processing are bounded by configurable timeouts.
- Why / problem solved: It reduces blocked resources and cascading stalls.
- Note / trade-off: Timeouts that are too short cause false failures; too long can exhaust resources.

### YARP
- Definition: Microsoft's reverse-proxy toolkit for .NET.
- FinWallet usage: `FinWallet.Gateway` uses YARP for routes, clusters, load balancing, health and request transforms.
- Why / problem solved: It provides a configuration-driven gateway without custom low-level proxy code.
- Note / trade-off: YARP is an application gateway; it is not a substitute for volumetric DDoS edge/WAF protection.

## 3. Security and Identity (39 terms)

### `sid` Claim
- Definition: A JWT claim carrying the session identifier.
- FinWallet usage: The durable session ID created at login is included in the access token and used for logout and fraud signals.
- Why / problem solved: It links the token to server-side session lifecycle and revocation state.
- Note / trade-off: This adds session validation beyond cryptographic token validation alone.

### `sub` Claim
- Definition: The standard JWT subject claim.
- FinWallet usage: In FinWallet it carries the authenticated CustomerId.
- Why / problem solved: It allows controllers to derive owner context without trusting a customer ID from the request body.
- Note / trade-off: An invalid or missing GUID leads to `INVALID_ACCESS_TOKEN`.

### Access Token
- Definition: A short-lived credential used to call protected APIs.
- FinWallet usage: FinWallet issues it as a JWT after login or refresh.
- Why / problem solved: It authenticates user identity on each request.
- Note / trade-off: Short lifetimes reduce stolen-token risk but require a refresh mechanism.

### Authentication
- Definition: Verifying who a user or service is.
- FinWallet usage: Public clients use login/JWT while internal service calls use service keys.
- Why / problem solved: It prevents unauthenticated actors from reaching protected endpoints.
- Note / trade-off: Authentication does not decide what an identity may do; authorization is a separate concern.

### Authorization
- Definition: Checking whether an authenticated identity is allowed to perform an action on a resource.
- FinWallet usage: FinWallet uses gateway policies, `[Authorize]`, owner-aware SQL and internal-service policies.
- Why / problem solved: It reduces risks such as accessing another customer's wallet or transaction data.
- Note / trade-off: Role checks alone are insufficient; resource ownership is also validated.

### Bearer Token
- Definition: A token usage model where possession of the token allows it to be presented in the HTTP Authorization header.
- FinWallet usage: `Authorization: Bearer <JWT>` is used on protected client endpoints.
- Why / problem solved: It integrates cleanly with standard HTTP tooling.
- Note / trade-off: A stolen token remains risky until expiry or session revocation, so TLS and short lifetimes are important.

### BOLA (Broken Object Level Authorization)
- Definition: A vulnerability where an authenticated user accesses another user's object by changing an identifier.
- FinWallet usage: FinWallet validates customer ownership for wallets, transactions and bank accounts on the server side.
- Why / problem solved: It prevents cross-customer access through guessed or manipulated IDs.
- Note / trade-off: JWT validation alone does not solve BOLA; object-level ownership checks are required.

### CDN
- Definition: A distributed network that serves/caches content from edge locations.
- FinWallet usage: It is not a required FinWallet API component but can appear in production edge/DDoS architecture discussions.
- Why / problem solved: It can provide static-content delivery and edge traffic absorption.
- Note / trade-off: Caching authenticated financial API responses requires extreme care.

### Claim
- Definition: A name/value statement carried inside a JWT about identity or session context.
- FinWallet usage: `sub` identifies the customer and `sid` identifies the session.
- Why / problem solved: Claims let Gateway/API associate requests with server-issued identity.
- Note / trade-off: Using claims instead of client-supplied customer IDs reduces spoofing.

### CSP (Content Security Policy)
- Definition: A browser response policy limiting allowed script, style, frame and other content sources.
- FinWallet usage: Shared.Web applies a restrictive CSP to APIs and a Swagger-compatible CSP to Swagger pages.
- Why / problem solved: It is a browser defense-in-depth control against XSS/frame injection.
- Note / trade-off: Its impact on pure APIs is limited, but it provides a secure default.

### Data Masking
- Definition: Hiding all or part of sensitive values in logs or responses.
- FinWallet usage: Tokens, OTPs, passwords and secrets are not logged; PII is minimized or masked when needed.
- Why / problem solved: It balances operational visibility with privacy/security.
- Note / trade-off: Masking is not encryption; the underlying sensitive data still requires protection.

### DDoS Protection
- Definition: Infrastructure that protects network/edge capacity against very high-volume distributed traffic attacks.
- FinWallet usage: Application rate limits only address L7 abuse; production needs cloud/load-balancer/CDN DDoS protection.
- Why / problem solved: Volumetric traffic is absorbed or filtered before reaching the application.
- Note / trade-off: A Kestrel/application limiter is not sufficient DDoS protection.

### DownstreamServiceKey
- Definition: A separate secret proving that a proxied request reaching FinWallet.Api or a fake provider came through Gateway.
- FinWallet usage: A YARP transform adds it and the destination validates it through Shared.Web.
- Why / problem solved: It prevents direct backend-port access from automatically becoming trusted.
- Note / trade-off: It is intentionally distinct from InternalServiceKey to preserve separate trust boundaries.

### Fail-Closed
- Definition: A security posture where an operation is denied when a required decision/control service fails.
- FinWallet usage: Protected transfer/purchase does not proceed when the external fraud provider is unavailable and returns 503.
- Why / problem solved: It prevents money movement without required risk evaluation.
- Note / trade-off: Availability can decrease; which dependencies fail closed depends on business risk appetite.

### Fail-Open
- Definition: A posture where an operation proceeds even if a control dependency fails.
- FinWallet usage: FinWallet deliberately avoids it for fraud decisions; however post-commit communication failure does not roll money back.
- Why / problem solved: It can preserve availability for non-critical dependencies.
- Note / trade-off: For fraud/auth controls it can create financial risk, so the distinction must be explicit.

### Fixed-Time Comparison
- Definition: Comparing secrets without early exit to reduce timing side-channel information.
- FinWallet usage: FinWallet validates internal service keys with `CryptographicOperations.FixedTimeEquals`.
- Why / problem solved: It makes it harder to infer correct secret prefixes from response timing.
- Note / trade-off: Length must still be handled and it does not eliminate every possible side channel.

### HMAC
- Definition: A keyed mechanism for producing message-integrity/authentication values.
- FinWallet usage: FinWallet uses HMAC concepts for HS256 JWT signing and OTP protection.
- Why / problem solved: It prevents valid values from being forged without the secret key.
- Note / trade-off: HMAC is not encryption; it authenticates integrity but does not hide the message.

### HS256 / HMAC-SHA256
- Definition: A symmetric JWT signing algorithm based on HMAC with SHA-256 and a shared secret.
- FinWallet usage: FinWallet uses it as a fixed access-token algorithm and requires a signing key of at least 32 UTF-8 bytes.
- Why / problem solved: It is simple and fast for a single trust domain.
- Note / trade-off: Anyone with the secret can sign tokens; asymmetric keys may be preferable across broader trust domains.

### HSTS
- Definition: A browser policy instructing clients to use HTTPS only for a period of time.
- FinWallet usage: FinWallet can enable it through production configuration.
- Why / problem solved: It reduces HTTPS downgrade and accidental HTTP usage.
- Note / trade-off: Local development and TLS termination topology must be considered when enabling it.

### InternalServiceKey
- Definition: A secret proving that FinWallet.Api is a trusted caller of Gateway internal/provider routes.
- FinWallet usage: It is sent as `X-Internal-Service-Key` and validated by the Gateway policy.
- Why / problem solved: It prevents normal clients from directly using provider routes.
- Note / trade-off: In production it must be injected from a secret store/environment and be sufficiently long.

### JWT (JSON Web Token)
- Definition: A signed token format carrying identity claims.
- FinWallet usage: Login issues an access token; Gateway and FinWallet.Api validate issuer, audience, signature and lifetime.
- Why / problem solved: It provides authenticated identity on each request without storing the whole identity in process memory.
- Note / trade-off: JWT revocation is difficult by itself, so FinWallet combines it with durable `sid` session state.

### Least Privilege
- Definition: The principle of granting only the minimum permissions required.
- FinWallet usage: FinWallet separates internal route credentials/policies, limits workflow permissions and avoids public backend ports.
- Why / problem solved: It reduces blast radius if credentials are compromised.
- Note / trade-off: Permissions that are too restrictive can break operations, so required capabilities should be explicit.

### Logout / Session Revocation
- Definition: Making an active server-side session invalid.
- FinWallet usage: `POST /api/v1/auth/logout` revokes the `sid` carried by the JWT.
- Why / problem solved: Even before token expiry, API session checks can reject use of the token.
- Note / trade-off: The effectiveness depends on protected flows actually validating durable session state.

### OTP (One-Time Password)
- Definition: A short-lived one-time verification code.
- FinWallet usage: FinWallet sends it by SMS for registration verification using TTL-based transient state.
- Why / problem solved: It provides an additional proof such as phone possession.
- Note / trade-off: An OTP is not an access token and must not be leaked in logs or normal responses.

### OWASP API Security Top 10
- Definition: OWASP guidance focused on API risks such as BOLA, broken authentication, resource consumption and unsafe API consumption.
- FinWallet usage: FinWallet references it for gateway/API auth, owner-aware SQL, rate limits and provider ACL design.
- Why / problem solved: It separates API threats from traditional browser/UI risks.
- Note / trade-off: The list is not sufficient by itself; business abuse and fraud need separate controls.

### OWASP Top 10
- Definition: OWASP guidance classifying common web-application security risks.
- FinWallet usage: FinWallet security documentation maps controls to access control, misconfiguration, cryptography, injection and logging concerns.
- Why / problem solved: It provides a shared security-review vocabulary/checklist.
- Note / trade-off: It is not a compliance certificate and does not replace threat modeling or real testing.

### PBKDF2
- Definition: A password key-derivation algorithm intentionally made expensive against brute force.
- FinWallet usage: FinWallet stores credentials using PBKDF2 V1 with a fixed work factor and salt.
- Why / problem solved: It avoids plaintext passwords and weak fast hashes.
- Note / trade-off: The work factor cannot be changed casually through config; versioned migration/re-hash is required.

### Pepper
- Definition: A secret application value added to hashing/HMAC and stored separately from the database.
- FinWallet usage: FinWallet can use a secret such as `REGISTRATION_OTP_PEPPER` for registration OTP protection.
- Why / problem solved: A database compromise alone is less useful without the separate secret.
- Note / trade-off: Pepper rotation needs an operational plan and the value must never be committed to source control.

### PII (Personally Identifiable Information)
- Definition: Data that can directly or indirectly identify a real person.
- FinWallet usage: Phone/email are stored where needed for registration, while fraud/reconciliation responses avoid unnecessary PII.
- Why / problem solved: It supports data minimization in logs and internal APIs.
- Note / trade-off: PII definitions vary by regulation; production systems need formal privacy classification.

### Refresh Token
- Definition: A longer-lived opaque credential used to obtain a new access token after expiry.
- FinWallet usage: FinWallet manages refresh state durably with rotation.
- Why / problem solved: It allows short access-token lifetimes without forcing frequent logins.
- Note / trade-off: It is high-value if stolen, so secure storage, rotation and revocation are required.

### Refresh Token Rotation
- Definition: Replacing the refresh token on every successful refresh and invalidating the old one.
- FinWallet usage: FinWallet uses rotation to reduce refresh-token replay risk.
- Why / problem solved: It limits reuse of a previously stolen token.
- Note / trade-off: Concurrent refresh races must be handled with durable transaction/uniqueness rules.

### Salt
- Definition: A random value added per password/credential before deriving the password hash.
- FinWallet usage: FinWallet PBKDF2 records use unique salts.
- Why / problem solved: It prevents equal passwords from producing equal hashes and weakens precomputed rainbow tables.
- Note / trade-off: A salt does not need to be secret; it must be unique and sufficiently random.

### Secret Injection
- Definition: Providing secrets such as signing keys, DB passwords or service keys from deployment infrastructure instead of source code.
- FinWallet usage: Production appsettings uses empty placeholders and expects environment/secret-store overrides.
- Why / problem solved: It prevents secrets from being embedded in Git history or images.
- Note / trade-off: A local `.env` file is a development convenience, not a production secret manager.

### Security Headers
- Definition: HTTP response headers that push browsers/clients toward safer defaults.
- FinWallet usage: Shared.Web centrally applies X-Content-Type-Options, X-Frame-Options, Referrer-Policy, Permissions-Policy, CSP and no-store controls.
- Why / problem solved: It prevents individual endpoints from forgetting baseline headers.
- Note / trade-off: These headers do not replace authentication or authorization; they are hardening controls.

### Service-to-Service Authentication
- Definition: Authentication where one backend service proves its identity to another.
- FinWallet usage: FinWallet.Api -> Gateway uses InternalServiceKey while Gateway -> backend/provider uses DownstreamServiceKey.
- Why / problem solved: It avoids reusing public user JWTs as machine trust credentials.
- Note / trade-off: Production systems may later prefer mTLS or managed identity over static service keys.

### Session
- Definition: A server-side record representing an authenticated login lifecycle.
- FinWallet usage: `sid`, device and revoke/refresh information are stored durably in MSSQL.
- Why / problem solved: It provides logout, revocation and risk-state control even though JWT itself is stateless.
- Note / trade-off: Session state is not financial truth, but it is authoritative for authentication lifecycle.

### SQL Injection
- Definition: A vulnerability where user input becomes part of SQL syntax and changes query behavior.
- FinWallet usage: FinWallet uses parameterized SQL and does not accept user-controlled SQL fragments.
- Why / problem solved: It reduces data exfiltration, modification and privilege-abuse risk.
- Note / trade-off: Parameters do not make dynamic table/column identifiers safe; those must remain server-owned.

### SSRF (Server-Side Request Forgery)
- Definition: A vulnerability where client input makes the server request unintended internal or external URLs.
- FinWallet usage: FinWallet provider base URLs are server-owned configuration; clients cannot supply arbitrary destinations.
- Why / problem solved: It reduces risks such as metadata-service or internal-network access.
- Note / trade-off: Compromised configuration is a separate risk; network egress policy is still useful.

### WAF (Web Application Firewall)
- Definition: An edge security layer that filters HTTP/L7 attack patterns.
- FinWallet usage: FinWallet does not implement a WAF in application code; it is recommended as production edge protection.
- Why / problem solved: It can block certain abuse/signature traffic before it reaches the application.
- Note / trade-off: It does not replace business authorization or fraud controls.

## 4. Data, Persistence and Concurrency (36 terms)

### ACID
- Definition: A model summarizing transaction properties: Atomicity, Consistency, Isolation and Durability.
- FinWallet usage: FinWallet financial postings rely on MSSQL transaction guarantees.
- Why / problem solved: It is foundational for preventing partial commits and protecting rules under concurrency.
- Note / trade-off: External HTTP is not part of the ACID transaction; distributed consistency is handled separately.

### Atomicity
- Definition: The property that all changes in a transaction either commit together or none commit.
- FinWallet usage: For transfers, source debit, destination credit, FinancialTransaction, Ledger and Idempotency commit or roll back together.
- Why / problem solved: It prevents one-sided money movement.
- Note / trade-off: External provider calls are outside this atomicity, so compensation/reconciliation are required.

### Check Constraint
- Definition: A database constraint requiring row values to satisfy a logical condition.
- FinWallet usage: FinWallet uses schema-level checks for suitable amount/status/type/financial invariants.
- Why / problem solved: It reduces invalid rows even if application code has a bug.
- Note / trade-off: It is used for simple invariants rather than encoding the entire business workflow in SQL.

### Commit
- Definition: Making transaction changes durable.
- FinWallet usage: Financial atomic posting is committed before a completed success result is returned.
- Why / problem solved: It defines the ordering between durable state and client success.
- Note / trade-off: A communication failure after commit does not roll money back; Outbox retry handles it.

### Connection Pooling
- Definition: Reusing physical DB or HTTP connections from a pool instead of creating them for every operation.
- FinWallet usage: SqlConnection logical open/close uses provider pooling and HttpClient uses handler connection pools.
- Why / problem solved: It reduces handshake/login cost and latency.
- Note / trade-off: Bad pool sizing can overload the database or cause request queuing.

### Consistency (Database)
- Definition: The property that data remains valid against constraints/invariants before and after a transaction.
- FinWallet usage: Ledger balancing, non-negative wallet rules and foreign/unique constraints support it.
- Why / problem solved: It reduces the chance of invalid persisted state.
- Note / trade-off: Business consistency requires both DB constraints and Application/Domain rules.

### Database Migration
- Definition: A controlled change of database schema from one version to another.
- FinWallet usage: Docker `mssql-init` applies scripts such as 001, 002, 003 and 004 in order using SchemaVersions.
- Why / problem solved: It makes required schema reproducible for new code.
- Note / trade-off: Migrations need idempotent execution checks and a rollback or forward-fix strategy.

### Database Transaction
- Definition: A mechanism executing a group of SQL changes as one commit/rollback unit.
- FinWallet usage: Wallet postings, corrections, outbox claiming/finalization and some auth changes use transactions.
- Why / problem solved: It keeps related rows changing together.
- Note / trade-off: External HTTP calls are deliberately kept outside the transaction.

### Decimal Precision
- Definition: Storing monetary values with fixed decimal precision rather than binary floating point.
- FinWallet usage: Money amounts use decimal types in application and database.
- Why / problem solved: It prevents floating-point rounding errors from entering financial calculations.
- Note / trade-off: Scale and rounding rules must remain consistent domain invariants.

### Durability
- Definition: The property that a committed transaction survives crashes.
- FinWallet usage: Completed transactions and ledger records are considered durable after MSSQL commit.
- Why / problem solved: It ensures financial state does not disappear after a success response.
- Note / trade-off: HA, backups and disaster recovery complement database durability at infrastructure level.

### Durable State
- Definition: Persisted state that must survive process or container restarts.
- FinWallet usage: Wallet balances, ledger, transactions, idempotency, sessions, fraud reviews and outbox/inbox are durable in MSSQL.
- Why / problem solved: It preserves correctness and replay behavior after crashes or restarts.
- Note / trade-off: Durability alone does not guarantee correctness; transactions and constraints are also required.

### Filtered Index
- Definition: A SQL Server index containing only rows that match a filter predicate.
- FinWallet usage: It can support small subsets such as pending/active rows.
- Why / problem solved: It provides targeted query performance with a smaller index.
- Note / trade-off: Required SQL Server SET options such as QUOTED_IDENTIFIER must be correct during migration.

### Foreign Key
- Definition: A constraint requiring a reference in one table to point to an existing row in another.
- FinWallet usage: It protects relationships such as transaction details, ledger entries and wallet/customer links.
- Why / problem solved: It reduces orphaned data.
- Note / trade-off: Constraints add some write cost but are usually valuable for financial integrity.

### GUID / UUID
- Definition: A 128-bit identifier format commonly used for uniqueness in distributed systems.
- FinWallet usage: Customer, Session, Wallet, Transaction, Journal and FraudEvent identities use GUIDs.
- Why / problem solved: It enables identifier creation without a central sequence coordinator.
- Note / trade-off: Random GUIDs can fragment clustered indexes, so physical index design still matters.

### Index
- Definition: A data structure that helps queries locate rows faster.
- FinWallet usage: FinWallet uses indexes on appropriate customer, transaction, idempotency and history keys.
- Why / problem solved: Indexes can reduce logical reads and latency.
- Note / trade-off: Every index adds write/storage cost, so the project avoids speculative indexing without Query Store/plan evidence.

### Isolation
- Definition: The transaction property controlling how concurrent operations observe each other's intermediate state.
- FinWallet usage: Financial posting stores use strong isolation/locking to prevent overspend and idempotency races.
- Why / problem solved: It helps stop concurrent debits from exceeding the same wallet balance.
- Note / trade-off: Strong isolation can increase lock contention and must be measured on hot paths.

### Keyset Pagination
- Definition: A pagination method using the last-seen key as a cursor to fetch the next page.
- FinWallet usage: Transaction history uses a cursor such as `beforeTransactionId` with newest-first ordering.
- Why / problem solved: It avoids large OFFSET scans and is more stable with concurrent inserts.
- Note / trade-off: Jumping directly to arbitrary page numbers is harder; the client uses cursor-based navigation.

### Lock
- Definition: A database concurrency mechanism preventing incompatible concurrent changes to the same data.
- FinWallet usage: SQL locking is part of wallet debit and idempotency correctness.
- Why / problem solved: It prevents two requests from reading the same balance and both spending it.
- Note / trade-off: Long transactions and inconsistent lock ordering increase deadlock risk.

### MSSQL / SQL Server
- Definition: A relational database management system.
- FinWallet usage: FinWallet uses SQL Server as the durable source of truth for Customer, Session, Wallet, BankAccount, FinancialTransaction, Ledger, Idempotency, FraudEvent, Outbox/Inbox and Reconciliation.
- Why / problem solved: ACID transactions, constraints, locking and rich querying support financial correctness.
- Note / trade-off: As scale grows it needs indexing/partitioning/HA planning; it is not used as a transient cache substitute.

### Optimistic Concurrency
- Definition: A concurrency approach that assumes conflicts are rare and detects them during update using versions/CAS semantics.
- FinWallet usage: FinWallet may use uniqueness/compare-and-set semantics in some paths while wallet posting also uses strong locking.
- Why / problem solved: It can reduce lock duration.
- Note / trade-off: For hot financial balances with frequent conflicts, pessimistic locking may be more suitable.

### Parameterized SQL
- Definition: Sending SQL values as parameters instead of concatenating them into query text.
- FinWallet usage: FinWallet stores parameterize customer IDs, amounts, statuses and other values.
- Why / problem solved: It reduces SQL injection risk and can improve plan reuse.
- Note / trade-off: Table/column identifiers cannot be parameterized and must remain server-owned.

### Persistence
- Definition: Storing and retrieving application state from a durable data store.
- FinWallet usage: Infrastructure SQL stores persist domain/application state to MSSQL.
- Why / problem solved: It provides lifecycle independent of process memory.
- Note / trade-off: The persistence model should not force Domain to depend on table structure.

### Pessimistic Concurrency
- Definition: A concurrency approach that assumes conflicts are likely and locks relevant data during the operation.
- FinWallet usage: Critical wallet balance posting controls concurrent debits on the same rows.
- Why / problem solved: It directly prevents overspend inside the database transaction.
- Note / trade-off: Throughput can be limited by lock contention.

### Race Condition
- Definition: A bug where correctness depends on the timing/order of concurrent operations.
- FinWallet usage: Parallel 600+600 spending from a 1000 wallet, duplicate idempotency keys or refresh-token races are examples.
- Why / problem solved: Financial correctness must remain deterministic across multiple instances.
- Note / trade-off: Single-thread unit tests are insufficient; real database concurrency tests are required.

### RDB Snapshot
- Definition: A point-in-time disk snapshot of Redis memory state.
- FinWallet usage: The Docker runbook can request one using `BGSAVE`.
- Why / problem solved: It provides an additional local recovery/debug mechanism.
- Note / trade-off: Because it is point-in-time, it may miss recent writes and is never used instead of the financial ledger.

### Redis
- Definition: A memory-first key/value store suited to TTLs, fast counters and transient coordination.
- FinWallet usage: FinWallet uses Redis for OTP TTLs, temporary counters, fraud velocity and hot/transient support state.
- Why / problem solved: It separates low-latency transient state from MSSQL workload.
- Note / trade-off: It is not authoritative for wallet balances or ledger data; losing Redis must not lose financial truth.

### Redis AOF
- Definition: A Redis persistence mode that logs write commands to an append-only file for recovery after restart.
- FinWallet usage: Docker Redis can enable AOF to make transient support state more resilient locally.
- Why / problem solved: It reduces loss of some transient state across container restarts.
- Note / trade-off: Redis still does not become financial truth and AOF is not a substitute for MSSQL backups.

### Rollback
- Definition: Discarding transaction changes before they are committed.
- FinWallet usage: Financial postings roll back on ledger imbalance, insufficient balance or SQL errors.
- Why / problem solved: It prevents partial database state.
- Note / trade-off: It cannot undo an already-completed external provider movement; compensation is required for that.

### Schema
- Definition: The structural definition of database tables, columns, constraints, indexes and relationships.
- FinWallet usage: FinWallet versioned schema scripts create auth and financial tables.
- Why / problem solved: It supports data correctness with DB-level rules independent of application code.
- Note / trade-off: Schema changes require backward-compatibility and migration planning.

### SchemaVersions
- Definition: A table/pattern recording which database migrations have already been applied.
- FinWallet usage: The init job checks it before running each migration script.
- Why / problem solved: It prevents accidental re-execution after container restarts.
- Note / trade-off: If an already-applied migration file is edited, the version record cannot detect that; applied migrations should be immutable.

### Serializable Isolation
- Definition: One of the strongest standard SQL isolation levels, making concurrent effects close to serial execution.
- FinWallet usage: It can be used on critical atomic financial posting paths.
- Why / problem solved: It strongly limits overspend and phantom-style races.
- Note / trade-off: It increases lock contention/deadlock risk and should be limited to boundaries that need it.

### Source of Truth
- Definition: The authoritative system considered correct for a piece of data.
- FinWallet usage: MSSQL is authoritative for FinWallet financial state; the bank provider is authoritative for its own external bank movements.
- Why / problem solved: It defines which system wins when caches or snapshots disagree.
- Note / trade-off: Multiple competing sources of truth create ambiguous ownership and reconciliation.

### Transient State
- Definition: Short-lived state whose loss must not corrupt financial truth.
- FinWallet usage: OTP TTLs, fraud velocity counters and cache-like data may live in Redis.
- Why / problem solved: It provides fast access and reduces unnecessary durable-DB load.
- Note / trade-off: It must be reconstructable or safely expirable.

### TTL (Time To Live)
- Definition: The duration after which a cache/key automatically expires.
- FinWallet usage: Registration OTP and some transient Redis state use TTLs.
- Why / problem solved: It prevents temporary verification data from living forever.
- Note / trade-off: TTL is not a retention policy for financial transactions; durable financial data is not expired this way.

### Unique Constraint
- Definition: A database rule preventing duplicate values for a column or column combination.
- FinWallet usage: FinWallet uses uniqueness for idempotency, Inbox Source+MessageId and domain-specific keys.
- Why / problem solved: It provides a single winner even when multiple instances race to create the same row.
- Note / trade-off: It is safer than relying only on an application-side `SELECT then INSERT` check.

### UTC / DateTimeOffset
- Definition: Representing timestamps in a timezone-neutral or offset-aware way.
- FinWallet usage: CreatedAt, CompletedAt and ReviewedAt timestamps use UTC/DateTimeOffset semantics.
- Why / problem solved: It improves ordering and audit consistency across services and regions.
- Note / trade-off: Local business cutoff rules still require country/timezone context.

## 5. Reliability, Messaging and Distributed Systems (22 terms)

### At-Least-Once Delivery
- Definition: A messaging guarantee that aims for at least one delivery and allows duplicates.
- FinWallet usage: The Outbox worker retries failures, so a message can have multiple delivery attempts.
- Why / problem solved: It accepts duplicate risk instead of silently losing messages.
- Note / trade-off: Exactly-once-like behavior requires idempotent consumers or deduplication.

### Background Worker / Hosted Service
- Definition: A process component that performs continuous or periodic work outside an HTTP request.
- FinWallet usage: `BankMoneyMovementBackgroundService` and `OutboxDispatchBackgroundService` serve this role.
- Why / problem solved: They progress long-running/pending provider flows without tying them to client request lifetime.
- Note / trade-off: Worker state must live in durable storage rather than process memory for restart safety.

### Backoff
- Definition: Delaying retries, often progressively, so a failing dependency is not hammered continuously.
- FinWallet usage: Outbox failure handling can schedule the next attempt for a later time.
- Why / problem solved: It reduces retry storms during dependency outages.
- Note / trade-off: Backoff alone does not replace retry limits or dead-letter/manual handling decisions.

### Callback
- Definition: A server-to-server call where an external provider later reports operation status.
- FinWallet usage: The internal bank callback endpoint processes Pending/Completed/Failed provider state through the Inbox.
- Why / problem solved: It avoids depending solely on client polling for long-running provider operations.
- Note / trade-off: Callbacks require authentication, deduplication and replay-safe finalization.

### Claim / Lease
- Definition: Temporarily assigning work to one worker so other workers do not process it concurrently.
- FinWallet usage: The Outbox dispatcher atomically claims messages in SQL.
- Why / problem solved: It reduces concurrent duplicate sends across multiple worker instances.
- Note / trade-off: If the worker crashes after claiming, the item must eventually become claimable again.

### Compensating Transaction
- Definition: A new transaction that offsets a prior financial transaction instead of deleting it.
- FinWallet usage: Refund and Reversal create an opposite journal/transaction without mutating the original.
- Why / problem solved: It preserves auditability and immutable history.
- Note / trade-off: Not every transaction type uses the same correction path; external-bank operations may require provider compensation.

### Compensation
- Definition: A business action that offsets a previously completed effect in a distributed flow.
- FinWallet usage: Examples include releasing blocked balance after provider failure or applying a corrective flow for external/local mismatch.
- Why / problem solved: It handles cases where a database rollback cannot undo a completed external action.
- Note / trade-off: Compensation is not rollback; it is a new auditable business action.

### Deduplication
- Definition: Preventing a repeated logical event/request/message from applying its effect again.
- FinWallet usage: Inbox unique keys, idempotency keys and terminal-state checks provide deduplication at different layers.
- Why / problem solved: It makes network retries and provider duplicates safe.
- Note / trade-off: A bad dedupe key can incorrectly collapse distinct real operations.

### Eventual Consistency
- Definition: A model where different systems are allowed to become consistent after a delay rather than instantly.
- FinWallet usage: Money can commit in MSSQL before a notification is later sent via Outbox; callbacks and reconciliation also arrive asynchronously.
- Why / problem solved: It enables reliable integration without a distributed transaction.
- Note / trade-off: The system must explicitly define which state is strongly consistent and which is eventually consistent.

### Exactly-Once Effect
- Definition: The goal that a business effect occurs once even if transport delivers a message multiple times.
- FinWallet usage: FinWallet approaches this with idempotency, Inbox and terminal-state guards rather than claiming exactly-once transport.
- Why / problem solved: It provides practical duplicate-safe financial behavior.
- Note / trade-off: The system does not claim absolute network-level exactly-once delivery.

### ExternalTransactionId
- Definition: A transaction identifier generated by the external bank/provider.
- FinWallet usage: It links local FinancialTransaction records to provider status queries, callbacks and reconciliation.
- Why / problem solved: It separates FinWallet transaction identity from provider transaction identity.
- Note / trade-off: It is not a Correlation ID; it is the provider's business-operation identity.

### Idempotency
- Definition: The property that repeating the same logical request does not apply its financial effect more than once.
- FinWallet usage: Transfer, Purchase, BankDeposit/Withdrawal and correction commands use durable `Idempotency-Key` handling.
- Why / problem solved: It prevents duplicate money movement under timeout, retry and network ambiguity.
- Note / trade-off: Same key + same payload replays the result; same key + different payload must conflict.

### Idempotency-Key
- Definition: A stable request key supplied by the client so the server can recognize a repeated financial command.
- FinWallet usage: It is required as a header on money-moving public POST endpoints.
- Why / problem solved: It allows client retries to replay an existing result instead of creating another transaction.
- Note / trade-off: It is distinct from Correlation ID and carries duplicate-protection semantics.

### Inbox Pattern
- Definition: A pattern that durably records incoming messages/callbacks and deduplicates them by a stable identity.
- FinWallet usage: FakeBank callbacks are deduplicated using Source + MessageId in the Inbox.
- Why / problem solved: It prevents repeated provider callbacks from applying financial finalization multiple times.
- Note / trade-off: The underlying business operation should also be replay-safe because Inbox alone cannot eliminate every crash window.

### Non-Retryable Error
- Definition: A terminal/business error where retrying the same request is not meaningful.
- FinWallet usage: Invalid account, permanent provider rejection or insufficient funds can fail the operation and release blocked funds.
- Why / problem solved: It prevents endless retries and permanently stuck blocked balances.
- Note / trade-off: Incorrect classification can either reduce availability or create duplicate-effect risk.

### Outbox Pattern
- Definition: A reliability pattern that stores an outgoing message in the same database transaction as business state and sends it later via a worker.
- FinWallet usage: Purchase/Bank-movement completion can create notification Outbox rows that a worker sends to FakeCommunication.
- Why / problem solved: It prevents a committed money operation from permanently losing its notification after a crash.
- Note / trade-off: It typically provides at-least-once delivery, so downstream effects should be idempotent.

### Provider Idempotency Key
- Definition: A stable key sent by FinWallet to an external provider to prevent duplicate downstream side effects.
- FinWallet usage: Bank-account opening and bank-movement requests carry provider request keys.
- Why / problem solved: It provides downstream duplicate protection independently of client idempotency.
- Note / trade-off: If the provider does not guarantee key semantics, FinWallet does not blindly retry.

### Replay
- Definition: Returning the result of an already-completed idempotent operation.
- FinWallet usage: Transfer/Purchase handlers check completed replay state before calling fraud/providers again.
- Why / problem solved: It prevents duplicate requests from repeating money movement or external-service cost.
- Note / trade-off: Replay responses should preserve original transaction identity and completion time.

### Request Hash
- Definition: A hash of a canonical request payload used with an idempotency key to prove the payload is unchanged.
- FinWallet usage: Fraud/idempotency flows can hash canonical source/destination/amount data with SHA-256.
- Why / problem solved: It detects reuse of the same key with a different payload.
- Note / trade-off: Canonicalization must be deterministic so formatting differences do not create false conflicts.

### Retry
- Definition: Trying an operation again after a transient failure.
- FinWallet usage: Outbox communication and retryable bank-status processing can be retried in a controlled way.
- Why / problem solved: It improves availability under transient provider/network failures.
- Note / trade-off: Financial POSTs are never blindly retried unless provider idempotency semantics make that safe.

### Retryable Error
- Definition: A transient error that may succeed if the same operation is attempted later.
- FinWallet usage: Provider timeout/network interruption or temporary communication outage are examples.
- Why / problem solved: It allows workers to remain pending and retry with backoff.
- Note / trade-off: Business rejection or insufficient funds are not retryable; error classification must be accurate.

### Terminal State
- Definition: A final transaction state that requires no further normal lifecycle processing.
- FinWallet usage: Completed and certain Failed/Denied states are terminal.
- Why / problem solved: Terminal checks prevent duplicate callbacks/retries from moving money again.
- Note / trade-off: Manual corrections may create a new child transaction while leaving the original terminal state unchanged.

## 6. Finance, Accounting and Banking (57 terms)

### Asset
- Definition: An accounting account type representing economic resources owned or controlled by the company.
- FinWallet usage: `BANK-SETTLEMENT:TRY` represents the banking-side backing asset in the FinWallet ledger.
- Why / problem solved: It makes the backing for wallet liabilities visible in the accounting equation.
- Note / trade-off: The current FakeBank model does not fully model a separate real FinWallet omnibus account; settlement asset is currently an accounting abstraction.

### Atomic Posting
- Definition: Applying all financial posting components in one database transaction as all-or-nothing.
- FinWallet usage: Balance, FinancialTransaction, Journal/Entries, idempotency and when needed Outbox commit together.
- Why / problem solved: It prevents partial money movement and ledger/balance divergence.
- Note / trade-off: External providers are outside this atomic posting boundary.

### Available Balance
- Definition: The portion of wallet balance immediately available for spending or transfer.
- FinWallet usage: Transfers and purchases debit the source wallet's available balance.
- Why / problem solved: It is central to overspend prevention.
- Note / trade-off: Pending external withdrawals may move funds from available to blocked.

### Bank Account
- Definition: An external account representing the customer's real/provider-side bank account.
- FinWallet usage: FinWallet links a BankAccount record to a wallet using FakeBank externalAccountId, IBAN, currency and status.
- Why / problem solved: It separates bank-held money from FinWallet wallet balance and provides the boundary for funding/withdrawal.
- Note / trade-off: BankAccount balance and Wallet balance do not have to be equal.

### Bank Reference
- Definition: An external reference associated with a bank/provider operation.
- FinWallet usage: It can be used with ExternalTransactionId for transaction history and troubleshooting.
- Why / problem solved: It makes matching the same movement across systems easier for support and reconciliation.
- Note / trade-off: It must not be confused with the internal FinWallet transaction ID.

### Bank Settlement Asset
- Definition: An asset-account abstraction representing FinWallet's bank-side backing for customer funds.
- FinWallet usage: It is debited on Bank->Wallet funding and credited down when Wallet->Bank withdrawal settles.
- Why / problem solved: It lets the ledger track the banking-side backing of customer wallet liabilities.
- Note / trade-off: Because FakeBank does not yet model a distinct FinWallet omnibus/safeguarding account, this is not a one-to-one record of a real provider account.

### Bank Statement
- Definition: A chronological list/statement of completed movements on an external bank account.
- FinWallet usage: `IBankProvider.GetStatementAsync` retrieves provider statement items for reconciliation.
- Why / problem solved: It allows comparison between FinWallet local bank-movement records and provider truth.
- Note / trade-off: Statement retrieval is read-only and does not mutate financial state.

### Bank-Settlement Reconciliation
- Definition: Comparing internal bank-transaction records with their expected settlement-ledger effects.
- FinWallet usage: Completed BankDeposit/Withdrawal amount/status is checked against bank-settlement entries.
- Why / problem solved: It detects divergence between local transaction state and accounting state.
- Note / trade-off: It is distinct from external provider-statement reconciliation.

### BankDeposit
- Definition: From FinWallet's perspective, funding the digital wallet from the customer's external bank account.
- FinWallet usage: The client calls `POST /bank-movements/deposits`; the provider adapter maps it to a FakeBank `Withdrawal` from the customer bank account, then local ledger increases settlement asset and wallet liability.
- Why / problem solved: It converts bank-held funds into spendable FinWallet balance.
- Note / trade-off: Do not confuse direction names: FinWallet Deposit means money enters the wallet; at FakeBank the same economic movement is a withdrawal from the customer account.

### BankWithdrawal
- Definition: From FinWallet's perspective, moving money from the digital wallet back to the external bank account.
- FinWallet usage: Funds can move from available to blocked, cutoff/provider processing occurs, and completion decreases wallet liability and settlement asset while crediting the customer's bank account.
- Why / problem solved: It prevents spending the same funds while an external bank movement is pending.
- Note / trade-off: Provider failure requires correct blocked-fund release/compensation.

### Blocked Balance
- Definition: Wallet funds temporarily reserved and unavailable but not yet finally settled out of the wallet.
- FinWallet usage: During a pending Wallet->Bank withdrawal, amount moves from available to blocked.
- Why / problem solved: It prevents the same money from being spent again in another transfer or purchase.
- Note / trade-off: A terminal provider failure must release the blocked amount or funds become stuck.

### Business Day / Business Calendar
- Definition: A calendar defining working days and holidays for banking operations.
- FinWallet usage: FakeCutoff simulates weekend/holiday/processing-date rules.
- Why / problem solved: It correctly models the difference between calendar days and bank processing days.
- Note / trade-off: Production requires an authoritative holiday-calendar source and timezone management.

### Campaign
- Definition: A business rule applying a discount under merchant/customer/amount conditions.
- FinWallet usage: FakeCampaign returns eligibility, discount amount and sponsor type; FinWallet performs the accounting.
- Why / problem solved: It separates promotion logic from financial posting.
- Note / trade-off: The provider only returns a decision/calculation and never writes the ledger.

### Campaign Expense
- Definition: An expense account representing a discount funded by the platform.
- FinWallet usage: For a 200 TRY purchase with a 20 TRY platform discount: debit customer liability 180, debit campaign expense 20, credit merchant payable 200.
- Why / problem solved: It makes the economic source of the discount explicit in the ledger.
- Note / trade-off: A merchant-sponsored discount may not use the same expense account; merchant payable can be net of the discount.

### Credit
- Definition: The right-side entry direction in double-entry accounting.
- FinWallet usage: It can increase a liability or decrease an asset; BankDeposit credits the customer wallet liability.
- Why / problem solved: Together with debit it keeps the journal balanced.
- Note / trade-off: Credit does not always mean 'money arrived'; interpretation depends on account type.

### Currency
- Definition: The code/enum identifying the monetary unit.
- FinWallet usage: TRY, USD and EUR create currency boundaries for wallets and accounts.
- Why / problem solved: It prevents accidental cross-currency posting.
- Note / trade-off: V1 has no FX conversion; different currency wallets are not automatically converted.

### Customer Wallet Liability
- Definition: A ledger liability account representing the customer's wallet balance as money FinWallet owes the customer.
- FinWallet usage: It increases with a credit on BankDeposit and decreases with a debit on source transfer or purchase.
- Why / problem solved: It captures the correct accounting meaning of wallet balance.
- Note / trade-off: The Wallet current balance should reconcile to the corresponding ledger liability.

### Cutoff
- Definition: A time/rule boundary affecting whether a bank operation processes on the same or a later business day.
- FinWallet usage: FakeCutoff decides using the business calendar and transaction type.
- Why / problem solved: It allows after-hours bank withdrawals to become Scheduled rather than failing.
- Note / trade-off: Cutoff is not necessarily one universal time; it can vary by country, currency and operation type.

### Debit
- Definition: The left-side entry direction in double-entry accounting.
- FinWallet usage: It can increase an asset or decrease a liability; BankDeposit debits settlement asset, while a transfer debits the source wallet liability to reduce it.
- Why / problem solved: It models economic direction according to account type.
- Note / trade-off: Debit does not always mean 'money went out'.

### Digital Wallet
- Definition: An internal financial account representing a customer's spendable digital-money balance in FinWallet.
- FinWallet usage: Each wallet is currency-specific and has AvailableBalance/BlockedBalance plus a ledger liability relationship.
- Why / problem solved: It supports fast internal transfers, purchases and refunds independently of the external bank.
- Note / trade-off: It is not the same as a BankAccount; wallet balance represents FinWallet's obligation to the customer.

### Discount
- Definition: The campaign amount deducted from a purchase's original price.
- FinWallet usage: OriginalAmount and DiscountAmount can be stored in transaction details/history.
- Why / problem solved: It separates the customer's net payment from the merchant's economic entitlement.
- Note / trade-off: Ledger entries depend on who sponsors the discount.

### Double-Entry Bookkeeping
- Definition: An accounting method where every event affects at least two accounts and total debit equals total credit.
- FinWallet usage: FinWallet uses it for transfers, bank deposits, purchases and corrections.
- Why / problem solved: The balance equation helps detect money being created or disappearing without an accounting source.
- Note / trade-off: Debit/credit do not mean good/bad or simply plus/minus; their effect depends on account type.

### Expense
- Definition: An accounting account type representing a cost incurred by the company.
- FinWallet usage: A platform-sponsored campaign discount can debit a `CAMPAIGN-EXPENSE`-type account.
- Why / problem solved: It makes the economic sponsor explicit instead of silently reducing merchant or customer value.
- Note / trade-off: The accounting posting depends on who sponsors the campaign.

### External Bank Statement Reconciliation
- Definition: Comparing FinWallet local bank movements with FakeBank/real-bank statements.
- FinWallet usage: ExternalTransactionId, amount, status and references are used to detect mismatches.
- Why / problem solved: It finds movements completed at the provider but missing locally, or the reverse.
- Note / trade-off: Provider I/O occurs outside the SQL transaction and results are later persisted as reconciliation data.

### FinancialTransaction
- Definition: A durable business-transaction record carrying lifecycle, amount, type, status and references.
- FinWallet usage: Transfer, BankDeposit, BankWithdrawal, Purchase, Refund and Reversal are tracked through this model.
- Why / problem solved: It separates business transaction identity/lifecycle from accounting journals.
- Note / trade-off: FinancialTransaction is not the ledger itself: the transaction says what happened; the ledger says the accounting effect.

### FinancialTransactionStatus
- Definition: The lifecycle state of a FinancialTransaction.
- FinWallet usage: States such as Scheduled, Pending, Completed and Failed are used in financial/bank flows.
- Why / problem solved: Workers, callbacks and history APIs use it to understand current progress.
- Note / trade-off: Terminal states are important guards against duplicate posting.

### FinancialTransactionType
- Definition: The enum/type identifying which business movement a FinancialTransaction represents.
- FinWallet usage: Values include WalletTransfer, BankDeposit, BankWithdrawal, Purchase, Refund and Reversal.
- Why / problem solved: It lets posting, correction and history logic understand transaction semantics.
- Note / trade-off: Database numeric contracts and code enum values must remain aligned.

### IBAN
- Definition: An international standardized identifier for a bank account.
- FinWallet usage: FakeBank generates an example IBAN when an external account is opened and FinWallet stores it on BankAccount.
- Why / problem solved: It provides a familiar external account reference.
- Note / trade-off: The simulator IBAN is not connected to a real banking clearing network.

### Ledger
- Definition: The accounting book that immutably records all FinWallet financial movements as debit/credit entries across accounts.
- FinWallet usage: Every completed financial posting creates a LedgerJournal with at least two LedgerEntries and total debits must equal total credits.
- Why / problem solved: It answers not only 'what is the balance now?' but also 'how was this balance formed?' in an auditable way.
- Note / trade-off: The Wallet table is current state/projection; Ledger is accounting history. Original entries are not deleted; corrections use opposite postings.

### Ledger Account
- Definition: An accounting account that receives debit/credit entries in the ledger.
- FinWallet usage: Logical accounts can include `BANK-SETTLEMENT:TRY`, `WALLET-LIABILITY:<walletId>` and `MERCHANT-PAYABLE:<merchant>`.
- Why / problem solved: It identifies the economic account affected by each movement.
- Note / trade-off: A Ledger Account is not the same concept as the customer's BankAccount entity.

### Ledger Entry
- Definition: A single debit or credit line inside a LedgerJournal.
- FinWallet usage: It carries account, side (Debit/Credit), amount/currency and references.
- Why / problem solved: It decomposes a business event into atomic accounting movements.
- Note / trade-off: An entry alone does not describe the entire transaction; it is interpreted as part of its journal.

### Ledger Journal
- Definition: A record grouping all debit/credit entries belonging to one financial event.
- FinWallet usage: A transfer, deposit, purchase or correction creates a journal whose entries must balance.
- Why / problem solved: It allows the accounting impact of one business event to be audited as a unit.
- Note / trade-off: Journals are not mutated; reversals/refunds create new journals.

### Liability
- Definition: An accounting account type representing an obligation owed to another party.
- FinWallet usage: A customer's wallet balance is modeled as `WALLET-LIABILITY:<wallet>`, an obligation FinWallet owes the customer.
- Why / problem solved: It prevents treating customer wallet funds as FinWallet's own money.
- Note / trade-off: Liabilities normally increase with credits and decrease with debits.

### Merchant
- Definition: The commercial party from whom the customer purchases goods or services.
- FinWallet usage: Purchase requests carry merchantId and Campaign/Fraud evaluation can use merchant context.
- Why / problem solved: It identifies the counterparty and merchant payable account for spending.
- Note / trade-off: V1 is not a complete merchant onboarding/settlement platform.

### Merchant Payable
- Definition: A liability account representing the amount FinWallet owes a merchant after a purchase.
- FinWallet usage: The purchase journal credits merchant payable according to the campaign sponsorship model.
- Why / problem solved: It converts customer wallet debit into a merchant settlement obligation.
- Note / trade-off: Actual bank settlement of merchant payable can be a separate operational scope.

### Money
- Definition: A financial value object combining Amount and Currency.
- FinWallet usage: Transfer, purchase, bank-movement and fraud-evaluation amounts use Money semantics.
- Why / problem solved: It prevents 100 TRY and 100 USD from being treated as the same value.
- Note / trade-off: Rounding and scale rules belong to the Money invariant.

### Negative Balance
- Definition: A wallet or related financial balance falling below zero contrary to business rules.
- FinWallet usage: Negative customer-wallet balances/overspend are forbidden v1 invariants.
- Why / problem solved: Without a credit/overdraft product, this keeps financial correctness simple.
- Note / trade-off: A future credit product would need an explicit limit/loan model rather than silently weakening the invariant.

### Omnibus Account
- Definition: A bank account model where a fintech/payment institution holds funds belonging to many customers in one pooled account.
- FinWallet usage: FinWallet does not yet fully model a separate real FakeBank omnibus account; the `BANK-SETTLEMENT` ledger asset represents the economic idea in accounting.
- Why / problem solved: It explains how one physical bank balance can be allocated to many customer wallet liabilities through the internal ledger.
- Note / trade-off: Production regulations may require safeguarding, segregation and legal-ownership controls.

### Original Transaction
- Definition: The original completed financial transaction referenced by a Refund or Reversal.
- FinWallet usage: Correction checks use original type, status and ownership through parent transaction references.
- Why / problem solved: It makes the economic event being corrected auditable.
- Note / trade-off: The original row/history is not deleted or overwritten.

### Overspend
- Definition: Spending or transferring more than the wallet's available balance.
- FinWallet usage: Atomic SQL balance checks/locking prevent it, including under concurrent requests.
- Why / problem solved: It prevents customers from spending nonexistent value and protects ledger/balance solvency.
- Note / trade-off: A simple application-side `if(balance >= amount)` is not race-safe.

### Parent Transaction
- Definition: A reference showing which previous transaction a correction/child transaction was derived from.
- FinWallet usage: Refund/Reversal history can expose ParentTransactionId.
- Why / problem solved: It makes the original-to-compensating transaction chain visible.
- Note / trade-off: Parent linkage does not replace accounting entries; each journal must still balance.

### Payable
- Definition: A liability representing an amount the company owes to a merchant or third party.
- FinWallet usage: A purchase increases `MERCHANT-PAYABLE:<merchant>` with a credit.
- Why / problem solved: It converts customer spending into a settlement obligation owed to the merchant.
- Note / trade-off: Actual bank transfer to the merchant may be a separate settlement process.

### Posting
- Definition: Persisting the financial effect of a business event into wallet state and the double-entry ledger.
- FinWallet usage: Transfer posting writes source/destination balances, transaction, journal, entries and idempotency result.
- Why / problem solved: It converts a business decision into durable accounting state.
- Note / trade-off: Fraud/provider external I/O is not performed while the posting transaction is open.

### Processing Date
- Definition: The business date on which a bank operation is processed.
- FinWallet usage: FakeCutoff can derive it from country, currency, transaction type and request time.
- Why / problem solved: It separates request timestamp from banking business-day processing.
- Note / trade-off: It is not the same as a UTC timestamp; it carries business-calendar semantics.

### Provider Deposit / Withdrawal Direction
- Definition: Directions defined from the FakeBank account's perspective.
- FinWallet usage: FakeBank `Deposit` increases the bank account and `Withdrawal` decreases it. A FinWallet BankDeposit may therefore invoke the opposite provider direction.
- Why / problem solved: It clarifies that the word 'deposit' depends on system perspective.
- Note / trade-off: The adapter/ACL isolates this semantic difference so provider enums do not leak into Domain.

### Purchase
- Definition: A financial transaction where the customer uses wallet balance to pay a merchant.
- FinWallet usage: After Fraud and Campaign evaluation, wallet liability decreases, merchant payable increases and sponsorship may add an expense/netting entry.
- Why / problem solved: It converts wallet spending into an obligation owed to the merchant.
- Note / trade-off: The external bank account does not need to move for every purchase; this is an internal ledger-based model.

### Reconciliation
- Definition: The process of comparing expected and observed financial records across two or more sources and reporting differences.
- FinWallet usage: FinWallet has Wallet<->Ledger, BankTransaction<->SettlementLedger and FinWallet<->FakeBankStatement scopes.
- Why / problem solved: It detects silent drift, bugs, missing callbacks and provider mismatches.
- Note / trade-off: FinWallet reconciliation never silently changes balances; it records issues without mutating financial history.

### Reconciliation Issue
- Definition: A durable record representing a mismatch discovered during reconciliation.
- FinWallet usage: It can store expected/actual amounts, transaction/wallet/bank references and non-PII details.
- Why / problem solved: It makes the discrepancy visible and traceable.
- Note / trade-off: Resolution may require manual investigation/correction; the system does not silently overwrite data.

### Reconciliation Run
- Definition: A single reconciliation execution for a defined scope.
- FinWallet usage: RunId, scope, status, timestamps and issue count are stored durably.
- Why / problem solved: It lets operations audit when and what reconciliation was performed.
- Note / trade-off: A reconciliation run does not automatically imply a correction.

### Refund
- Definition: A correction that returns all or a defined portion of a completed Purchase to the customer.
- FinWallet usage: FinWallet v1 performs full purchase refund by creating a new FinancialTransaction and opposite journal without deleting the original.
- Why / problem solved: It restores customer wallet liability and reverses merchant/campaign effects while preserving audit history.
- Note / trade-off: Refund does not mutate the original purchase and is linked through parent transaction identity.

### Reversal
- Definition: A new transaction that reverses the economic effect of a completed transaction.
- FinWallet usage: Public reversal safely handles internal WalletTransfer by moving value destination->source with an opposite ledger journal.
- Why / problem solved: It corrects money movement while preserving immutable history.
- Note / trade-off: External BankDeposit/Withdrawal is not reversed through this endpoint; provider compensation is required.

### Safeguarding
- Definition: The practice of protecting customer funds by separating them legally/operationally from the company's own operating funds.
- FinWallet usage: FinWallet v1 does not implement a full regulatory safeguarding model; settlement-asset/liability separation provides the conceptual accounting basis.
- Why / problem solved: It makes clear that wallet balances are not company revenue and must be backed/protected.
- Note / trade-off: A real product requires jurisdiction-specific regulation, licensing, bank-partner and legal-accounting design.

### Settlement
- Definition: The process of finally discharging the economic obligation between parties in a financial transaction.
- FinWallet usage: Bank-movement processing/settlement dates and `BANK-SETTLEMENT` ledger accounts use this concept.
- Why / problem solved: It separates internal book entries from real external-bank movement.
- Note / trade-off: Internal posting and external settlement do not have to occur at the same instant, so reconciliation is needed.

### Settlement Date
- Definition: The business date on which a bank/financial operation is expected to reach final settlement.
- FinWallet usage: The Cutoff provider returns processing/settlement dates that can be stored in transaction details.
- Why / problem solved: It explains when a scheduled/pending operation is expected to settle.
- Note / trade-off: Actual provider callback time can differ from the settlement date; reconciliation is still required.

### Sponsor
- Definition: The party economically funding a campaign discount.
- FinWallet usage: Posting differs between platform-sponsored and merchant-sponsored discounts.
- Why / problem solved: It determines which accounting account bears the discount.
- Note / trade-off: Sponsor is not just a UI label; it is an accounting input.

### Wallet Transfer
- Definition: An internal same-currency movement from one FinWallet wallet to another.
- FinWallet usage: After fraud approval, source liability is debited, destination liability credited and balances updated atomically.
- Why / problem solved: It provides internal book transfer without a bank call.
- Note / trade-off: Source/destination currency, overspend and idempotency rules must hold.

### Wallet-Ledger Reconciliation
- Definition: Comparing the current Wallet table balance with the economic balance derivable from the ledger.
- FinWallet usage: It is one of the reconciliation scopes.
- Why / problem solved: It detects projection drift or incorrect posting.
- Note / trade-off: A mismatch does not cause automatic wallet overwrite; an issue is created.

## 7. Fraud and Risk (17 terms)

### Allow
- Definition: The fraud decision allowing the operation to continue.
- FinWallet usage: When durable fraud state is Allow/Approved, transfer/purchase proceeds to atomic posting.
- Why / problem solved: It lets the business flow continue after risk checks.
- Note / trade-off: Allow does not prove absence of fraud; it means risk is acceptable under current signals.

### Deny
- Definition: The fraud decision preventing the operation from being executed.
- FinWallet usage: Internal fraud can deny directly or the combined policy can produce a final Deny.
- Why / problem solved: It prevents risky operations from reaching wallet/ledger posting.
- Note / trade-off: Reason codes should be safely recorded for audit/operations without exposing sensitive model details to clients.

### External Fraud Provider
- Definition: An integration boundary obtaining part of the fraud decision from an external service.
- FinWallet usage: FakeFraud simulates Allow/Review/Deny, score and reason codes.
- Why / problem solved: It keeps FinWallet's internal fraud logic independent from an external risk engine.
- Note / trade-off: Protected flows fail closed when the provider is unavailable.

### Fraud
- Definition: The problem domain of detecting and preventing unauthorized, malicious or risky financial behavior.
- FinWallet usage: FinWallet evaluates internal and external fraud before WalletTransfer and Purchase.
- Why / problem solved: It places risk decisions before money movement.
- Note / trade-off: Current v1 BankDeposit does not call fraud; documentation intentionally does not invent such a step.

### Fraud Decision
- Definition: The Allow, Review or Deny outcome of a fraud assessment.
- FinWallet usage: Internal and external decisions are combined into a final decision by policy.
- Why / problem solved: It explicitly determines whether financial posting can proceed.
- Note / trade-off: The decision is not the same as HTTP status; for example Review can map to 202.

### Fraud Decision Policy
- Definition: A business policy combining internal and external fraud decisions into one final decision.
- FinWallet usage: For example, any Deny may produce final Deny, Review combinations can produce Review, and only safe combinations Allow.
- Why / problem solved: It avoids scattering decision-merging conditionals through handlers.
- Note / trade-off: Changing the policy changes risk appetite and requires testing/audit.

### Fraud Score
- Definition: A numeric provider/model output representing risk level.
- FinWallet usage: FakeFraud can return a score; FinWallet does not blindly equate score with the final decision.
- Why / problem solved: It provides an input for threshold/policy decisions.
- Note / trade-off: Score calibration is model/provider-specific and can change over time.

### FraudEvent
- Definition: A durable record of fraud evaluation identity, internal/external/final decision, reason codes and review state.
- FinWallet usage: WalletTransfer/Purchase use it to replay fraud state for the same idempotent request.
- Why / problem solved: It prevents repeated provider calls and lost decisions while supporting manual-review audit.
- Note / trade-off: Raw sensitive payloads/hashes/PII are not unnecessarily exposed through internal responses.

### Internal Fraud Engine
- Definition: FinWallet's deterministic/rule-based risk component using server-side signals.
- FinWallet usage: It evaluates amount, velocity, new-device and beneficiary/merchant familiarity signals.
- Why / problem solved: It catches obvious risks before external provider calls and adds domain-specific controls.
- Note / trade-off: It is not a complete production ML fraud system and requires real-data tuning.

### Known Beneficiary
- Definition: A signal indicating whether the transfer destination is previously known to the customer.
- FinWallet usage: It can be used in WalletTransfer risk context.
- Why / problem solved: It helps distinguish risky combinations such as large transfers to a new beneficiary.
- Note / trade-off: Familiarity must be derived server-side from history rather than a client-supplied boolean.

### Manual Fraud Review
- Definition: A pending FraudEvent being approved or denied by an internal operator/reviewer.
- FinWallet usage: `/api/v1/internal/fraud-reviews` runs behind the Gateway InternalService policy.
- Why / problem solved: It provides human-in-the-loop risk decisions without introducing a normal customer/admin user type.
- Note / trade-off: Reviewer identity and review timestamp must be audited and the decision should be final once made.

### Merchant Familiarity
- Definition: A risk signal indicating whether the purchase merchant is familiar from customer history.
- FinWallet usage: Purchase fraud can use merchant familiarity instead of beneficiary familiarity.
- Why / problem solved: It helps assess patterns such as a new merchant combined with a high amount.
- Note / trade-off: It is not proof of fraud by itself and is combined with other signals.

### New Device
- Definition: A risk signal indicating a device reference not previously seen for the session/customer.
- FinWallet usage: Fraud logic can treat a new-device transaction as higher risk.
- Why / problem solved: It helps expose activity after credential theft from a different device.
- Note / trade-off: Device fingerprints have privacy/spoofing limitations and should not be the sole deny reason.

### Reason Code
- Definition: A short machine-readable code explaining why a fraud or business decision was made.
- FinWallet usage: Internal/external fraud reason codes are normalized and stored on FraudEvent.
- Why / problem solved: It supports operations, analytics and testing without parsing free-form text.
- Note / trade-off: Clients should not receive enough rule detail to facilitate model/rule bypass.

### Review
- Definition: A fraud decision requiring manual review instead of automatic allow or deny.
- FinWallet usage: It creates a durable pending FraudEvent and the public request can return 202 Accepted.
- Why / problem solved: It pauses suspicious-but-not-certainly-invalid operations before money movement.
- Note / trade-off: Review state is durable; replaying the same idempotent request should not call external fraud again.

### Risk Signal
- Definition: Server-side behavioral/context data used in fraud assessment.
- FinWallet usage: Examples include transaction count, 24-hour amount, country, device, known beneficiary and merchant familiarity.
- Why / problem solved: It builds risk context on the server rather than trusting clients to declare themselves safe.
- Note / trade-off: Poor signal quality increases false positives and false negatives.

### Velocity
- Definition: A fraud signal measuring transaction frequency or amount intensity over a time window.
- FinWallet usage: FinWallet can use transaction count over five minutes and amount over 24 hours.
- Why / problem solved: It helps detect rapid account takeover or automated abuse patterns.
- Note / trade-off: Redis counters can be transient; they are not financial truth.

## 8. Performance and Observability (13 terms)

### Correlation / Traceability
- Definition: The ability to connect logs, provider calls and database records belonging to one business flow.
- FinWallet usage: CorrelationId, TransactionId, ExternalTransactionId, FraudReference and Outbox/Inbox message IDs are used together.
- Why / problem solved: It simplifies finding where a distributed flow failed.
- Note / trade-off: One universal ID should not be overloaded with every semantic meaning.

### Health Endpoint
- Definition: An HTTP endpoint used by orchestrators, proxies and operators to check service health.
- FinWallet usage: Docker smoke tests and YARP health mechanisms use `/health/*` endpoints.
- Why / problem solved: It provides an availability signal for startup and routing automation.
- Note / trade-off: Health endpoints should not expose sensitive dependency or secret details.

### Latency
- Definition: The elapsed time from the start of an operation/request to its result.
- FinWallet usage: Gateway, provider HTTP, MSSQL and Redis all contribute to total API latency.
- Why / problem solved: It is a primary metric for user experience and timeout design.
- Note / trade-off: Average latency is insufficient; p95/p99 tail latency should also be observed.

### Liveness
- Definition: A health concept indicating whether the process itself is alive and not irrecoverably stuck.
- FinWallet usage: Gateway/API live endpoints can signal whether a container should be restarted.
- Why / problem solved: It avoids restarting a healthy process merely because a dependency is temporarily unavailable.
- Note / trade-off: Separating it from readiness improves production orchestration.

### Log Rotation
- Definition: Rotating logs by size/count so they do not grow without bound.
- FinWallet usage: Docker json-file logging is configured with rotation limits.
- Why / problem solved: It reduces the risk of host/service failure due to full disks.
- Note / trade-off: It is not a substitute for centralized log retention; it is local disk safety.

### Logical Reads
- Definition: A measure of how many database pages a SQL query reads from the buffer cache.
- FinWallet usage: FinWallet considers it alongside elapsed time for query/index tuning.
- Why / problem solved: It reveals unnecessary scans even on a fast test machine.
- Note / trade-off: It should be interpreted with CPU, waits, row counts and execution plans.

### Observability
- Definition: The ability to understand internal system behavior through logs, metrics, traces and health signals.
- FinWallet usage: FinWallet provides a foundation with correlation IDs, structured logs, health endpoints and durable transaction/reconciliation records.
- Why / problem solved: It helps answer why incidents or performance changes occurred.
- Note / trade-off: A full production setup still needs centralized log, metrics and tracing backends.

### p95 / p99
- Definition: Latency percentiles below which 95% or 99% of requests complete.
- FinWallet usage: Performance review uses them for hot endpoints, providers and SQL paths.
- Why / problem solved: They expose slow-tail behavior hidden by averages.
- Note / trade-off: Percentiles require sufficient samples and an appropriate time window.

### Query Plan
- Definition: The SQL Server execution plan describing indexes, joins and access methods used for a query.
- FinWallet usage: FinWallet performance tuning checks plans and logical reads before adding indexes.
- Why / problem solved: It supports evidence-based tuning instead of guesswork.
- Note / trade-off: Plans vary with data distribution and environment; one small test database is not representative.

### Query Store
- Definition: A SQL Server feature collecting query history, plans and runtime statistics.
- FinWallet usage: It is recommended for finding regressions and expensive queries in production-like environments.
- Why / problem solved: It makes the effect of query/index changes measurable.
- Note / trade-off: Storage/retention settings require management and Query Store does not replace application logging.

### Readiness
- Definition: A health concept indicating whether a service is ready to receive new traffic.
- FinWallet usage: It can prevent API traffic before MSSQL/Redis/schema initialization is ready.
- Why / problem solved: It reduces startup races and dependency-related request failures.
- Note / trade-off: Not every optional dependency should necessarily make the entire service unready.

### Structured Logging
- Definition: Logging using named fields rather than only free-form message text.
- FinWallet usage: CorrelationId, TransactionId, path, status and safe error codes can be logged as structured fields.
- Why / problem solved: It improves search, aggregation and incident investigation.
- Note / trade-off: Passwords, OTPs, tokens, connection strings and unnecessary PII must not be logged as structured fields.

### Throughput
- Definition: The number of requests or transactions a system can process over a period.
- FinWallet usage: Gateway, API, SQL pools and provider capacity all bound FinWallet throughput.
- Why / problem solved: It is used for scaling and capacity planning.
- Note / trade-off: Throughput is never increased by weakening financial correctness, locking or idempotency invariants.

## 9. Testing and Quality (15 terms)

### Chaos Test
- Definition: A test that deliberately injects failures such as dependency delays, outages or restarts to validate recovery behavior.
- FinWallet usage: Fake-provider fail/delay/timeout modes, container restarts and communication outages provide the basis.
- Why / problem solved: It validates fail-closed behavior, Outbox retry, blocked-fund release and reconciliation.
- Note / trade-off: Production chaos testing requires controlled blast radius and strong observability.

### Concurrency Test
- Definition: A test verifying that concurrent requests on the same resource do not violate invariants.
- FinWallet usage: For example, with a 1000 balance only one of two parallel 600 transfers should complete.
- Why / problem solved: It proves overspend, idempotency-race and locking behavior on the real database.
- Note / trade-off: It should use real parallel requests and durable-state assertions rather than artificial sleeps.

### Definition of Done
- Definition: The set of criteria required before a feature is considered complete.
- FinWallet usage: FinWallet requires code plus affected API, DB, security, tests, docs, configuration and runtime validation to be updated.
- Why / problem solved: It reduces cases where code is merged while tests or documentation remain stale.
- Note / trade-off: The checklist is a living standard and can evolve as the project grows.

### End-to-End (E2E) Test
- Definition: A test covering a critical flow from the client entry point to final durable outcome using real components.
- FinWallet usage: Register -> login -> wallet -> bank movement -> transfer/purchase paths can be E2E tests.
- Why / problem solved: It validates that the system actually works from the user's perspective.
- Note / trade-off: Failures are harder to localize than unit tests, so E2E suites should focus on critical scenarios.

### Fixture
- Definition: Pre-arranged data or environment state used by a test.
- FinWallet usage: Integration/E2E tests may seed customers, wallets, bank accounts and balances as fixtures.
- Why / problem solved: Fixtures provide repeatable scenarios.
- Note / trade-off: Financial fixtures should preserve ledger consistency instead of directly editing wallet balances.

### Integration Test
- Definition: A test exercising multiple real components/dependencies together.
- FinWallet usage: FinWallet targets Docker-based integration across Gateway, FinWallet.Api, MSSQL, Redis and fake providers.
- Why / problem solved: It catches DI, configuration, serialization, schema and network-boundary failures.
- Note / trade-off: It is slower and more environment-dependent than unit testing.

### Load Test
- Definition: A test that applies controlled high traffic to measure throughput, latency and resource behavior.
- FinWallet usage: It is needed to validate Gateway/API/SQL/Redis pool and rate-limit values before production.
- Why / problem solved: It makes configuration tuning evidence-based.
- Note / trade-off: Financial test data and side effects must remain isolated in a safe environment.

### Mock
- Definition: A test double whose behavior is controlled by the test instead of using the real dependency.
- FinWallet usage: Moq can mock `IBankProvider`, stores or external dependencies in unit tests.
- Why / problem solved: It makes failure and edge-case behavior fast and deterministic.
- Note / trade-off: Mocks do not prove real provider/SQL behavior and excessive mocking can couple tests to implementation details.

### Moq
- Definition: A .NET mocking library used to create interface/class test doubles.
- FinWallet usage: FinWallet tests use it to isolate external/store dependencies.
- Why / problem solved: Setup/Verify makes expected-interaction testing straightforward.
- Note / trade-off: It is not a substitute for real integration testing.

### Smoke Test
- Definition: A quick post-build/deploy check that the system starts and key dependencies are reachable.
- FinWallet usage: Docker CI starts all services and validates Gateway health, MSSQL schema and Redis connectivity.
- Why / problem solved: It catches runtime DI/config/startup issues that static build validation misses.
- Note / trade-off: It is not a deep business-correctness test.

### Strict Mock
- Definition: A mock mode that fails when an unconfigured dependency call occurs.
- FinWallet usage: Some handler tests use Moq Strict to catch unexpected provider/store calls.
- Why / problem solved: It can prove, for example, that the bank provider is never called after an early validation failure.
- Note / trade-off: Overly strict tests can become refactor-fragile, so it is best used for meaningful business interactions.

### Test Double
- Definition: A general term for a fake/mock/stub-like object replacing a real dependency in tests.
- FinWallet usage: FinWallet uses test doubles to control provider/store behavior in unit tests.
- Why / problem solved: They make tests fast and deterministic.
- Note / trade-off: The chosen double style should keep the test's intent clear.

### Unit Test
- Definition: A test focusing on one class or small business behavior with external dependencies isolated.
- FinWallet usage: FinWallet.Application.Tests validates handler behavior using mocks/test doubles.
- Why / problem solved: It provides fast deterministic feedback for branches and error handling.
- Note / trade-off: It does not prove real SQL locking, Redis atomicity or Gateway routing.

### Warnings as Errors
- Definition: Treating compiler warnings as build failures.
- FinWallet usage: FinWallet Release CI builds with `--warnaserror`.
- Why / problem solved: It prevents new warnings from accumulating silently.
- Note / trade-off: Third-party or generated warnings may require targeted, justified suppression.

### xUnit
- Definition: A .NET unit/integration testing framework.
- FinWallet usage: FinWallet.Application.Tests uses xUnit v3.
- Why / problem solved: It provides fact/theory-based testing and CI integration.
- Note / trade-off: A test framework is not a testing strategy by itself; scenario selection and coverage still require design.

## 10. Docker, CI/CD and Configuration (25 terms)

### .env
- Definition: A local file holding environment-variable values for Docker Compose/development.
- FinWallet usage: `.env.example` is versioned while the real `.env` is gitignored.
- Why / problem solved: It makes local configuration easy to bootstrap.
- Note / trade-off: It is not a production secret-management mechanism.

### appsettings.json
- Definition: .NET's base JSON configuration file.
- FinWallet usage: FinWallet defines platform limits, provider URLs/timeouts, Redis/SQL tuning, Swagger and Gateway configuration here.
- Why / problem solved: It separates operational configuration from code.
- Note / trade-off: Financial and cryptographic invariants are not exposed as casual runtime switches.

### appsettings.Production.json
- Definition: The standard .NET configuration file overriding base settings in the Production environment.
- FinWallet usage: It can disable Swagger by default and provide production-oriented settings with empty secret placeholders.
- Why / problem solved: It separates development and production behavior without hardcoding environment branches.
- Note / trade-off: Actual secrets are not committed and must be injected externally.

### BuildKit Cache
- Definition: A Docker build cache mechanism that reuses layers/restore output across builds.
- FinWallet usage: FinWallet .NET images use NuGet restore cache mounts and can lock sharing to avoid parallel cache corruption.
- Why / problem solved: It reduces CI build time.
- Note / trade-off: Cache is never more important than correctness; clean builds remain useful when cache corruption is suspected.

### CI (Continuous Integration)
- Definition: The practice of automatically building/testing/validating changes before they reach the main branch.
- FinWallet usage: FinWallet CI runs Release builds with warnings-as-errors and tests; Docker workflows validate runtime behavior.
- Why / problem solved: It catches compile, runtime and schema regressions early.
- Note / trade-off: Green CI does not guarantee all production correctness; critical E2E, security and load testing still matter.

### Compose Overlay
- Definition: An additional YAML file that overrides the base Compose configuration for a specific environment.
- FinWallet usage: `compose.debug.yml` exposes localhost debug ports and `compose.production.yml` adds production-like overrides.
- Why / problem solved: It allows environment variation without cluttering one file with every conditional.
- Note / trade-off: Overlay order matters and incorrect combinations can expose unintended ports/settings.

### Configuration Precedence
- Definition: The order determining which value wins when the same configuration key appears in multiple sources.
- FinWallet usage: FinWallet follows the usual appsettings -> environment-specific appsettings -> environment variables/secret injection layering.
- Why / problem solved: It enables the same image to be configured per environment.
- Note / trade-off: Resolved configuration can contain secrets and should not be logged.

### Container
- Definition: A running instance of an image with isolated process, filesystem and networking.
- FinWallet usage: Each FinWallet HTTP service plus MSSQL/Redis runs in its own container.
- Why / problem solved: It provides dependency/version isolation and fast recreation.
- Note / trade-off: Application containers are designed stateless; durable data lives in volumes/databases.

### Docker
- Definition: A platform for running applications and dependencies as isolated container images/runtimes.
- FinWallet usage: Gateway, FinWallet.Api, fake providers, MSSQL and Redis can be started with Docker Compose.
- Why / problem solved: It makes local/integration environments reproducible.
- Note / trade-off: Containers do not automatically solve production orchestration or HA.

### Docker Compose
- Definition: A tool for defining and running multiple container services, networks and volumes together.
- FinWallet usage: `compose.yml` defines the complete FinWallet stack.
- Why / problem solved: It provides one-command local full-stack startup, dependency ordering and configuration.
- Note / trade-off: It does not have to be the final production orchestrator.

### Docker DNS / Service Discovery
- Definition: Automatic hostname resolution of Compose service names inside a Docker network.
- FinWallet usage: Gateway destinations can use names such as `finwallet-api` and `fake-bank`.
- Why / problem solved: It removes the need for hardcoded container IPs.
- Note / trade-off: Service names are environment-specific and should be managed through configuration.

### Docker Network
- Definition: An isolated virtual network where containers can reach each other by service/DNS name.
- FinWallet usage: `finwallet-backend` connects HTTP services while `finwallet-data` connects FinWallet.Api to MSSQL/Redis.
- Why / problem solved: It provides connectivity without publishing databases to the host.
- Note / trade-off: Network segmentation does not replace credentials/authentication.

### Dockerfile
- Definition: A declarative file describing how a container image is built.
- FinWallet usage: `docker/Dockerfile.webapi` provides the shared multi-stage build for Gateway/API/fake services.
- Why / problem solved: It makes build steps reproducible and version-controlled.
- Note / trade-off: Runtime images should remain small and non-root where practical.

### Environment Variable
- Definition: A method of supplying runtime configuration through the process environment.
- FinWallet usage: Connection strings, passwords, JWT/service keys and tuning settings can override appsettings.
- Why / problem solved: It enables environment-specific configuration without changing the image/source.
- Note / trade-off: Secret environment variables still have exposure risks; production secret-store/orchestrator integration is preferable.

### GitHub Actions
- Definition: GitHub's automation platform for running CI/workflows on repository events.
- FinWallet usage: FinWallet uses it for restore/build/test, Docker validation/smoke and PDF generation.
- Why / problem solved: It provides reproducible quality gates and artifacts for pull requests.
- Note / trade-off: Workflow permissions and secret access should follow least privilege.

### Image
- Definition: An immutable filesystem/runtime package used to create containers.
- FinWallet usage: .NET service images are built through a multi-stage Dockerfile.
- Why / problem solved: It makes the same binary/runtime package consistent across environments.
- Note / trade-off: Secrets and mutable runtime data are not baked into the image.

### Multi-Stage Build
- Definition: A technique separating build tooling and final runtime into different Docker stages.
- FinWallet usage: The SDK stage restores/builds/publishes while the final ASP.NET runtime stage contains only published output.
- Why / problem solved: It reduces final image size and attack surface.
- Note / trade-off: Poor cache/copy ordering can slow CI builds.

### Named Volume
- Definition: Docker-managed persistent storage whose lifecycle is independent of an individual container.
- FinWallet usage: `finwallet_mssql_data`, `finwallet_mssql_backup` and `finwallet_redis_data` are named volumes.
- Why / problem solved: They preserve data across container recreation.
- Note / trade-off: `docker compose down -v` deletes them and is documented as destructive.

### Non-Root Container
- Definition: Running the application process inside the container without root privileges.
- FinWallet usage: FinWallet .NET runtime images use a `USER app` style configuration.
- Why / problem solved: It reduces privilege impact if the process/container is compromised.
- Note / trade-off: Filesystem and port permissions must be planned accordingly.

### Read-Only Filesystem
- Definition: A hardening approach where the application container root filesystem is not writable at runtime.
- FinWallet usage: Stateless FinWallet API containers can use a read-only root filesystem where practical.
- Why / problem solved: It reduces malware/config tampering and accidental local-state risks.
- Note / trade-off: Temporary/runtime write paths may need explicit tmpfs or volumes.

### Resource Limit
- Definition: A cap on container CPU, memory or PID consumption.
- FinWallet usage: Compose can define local safety-baseline limits for FinWallet application containers.
- Why / problem solved: It limits one faulty service from consuming the entire host.
- Note / trade-off: Production limits should be based on load testing and capacity measurement.

### SBOM (Software Bill of Materials)
- Definition: An inventory listing software components and versions contained in a build/artifact.
- FinWallet usage: FinWallet documentation considers it a supply-chain improvement rather than a core runtime component.
- Why / problem solved: It helps quickly identify affected artifacts during vulnerability incidents.
- Note / trade-off: Generating an SBOM does not remediate vulnerabilities by itself; scanning and remediation workflows are required.

### Supply Chain Security
- Definition: Protecting the build/package/dependency chain from malicious or vulnerable components.
- FinWallet usage: Central NuGet versions, explicit dependencies, CI restore/build/test and planned vulnerability/SBOM controls belong here.
- Why / problem solved: It reduces risk originating from third-party components even when application code is correct.
- Note / trade-off: Pinning packages alone is not enough; provenance, vulnerability and update processes are needed.

### Vulnerability Scan
- Definition: Automated checking of dependencies, container images or code for known security vulnerabilities.
- FinWallet usage: It can serve as a supply-chain quality gate in FinWallet CI hardening.
- Why / problem solved: It makes known-CVE risk visible before merge/deployment.
- Note / trade-off: Human review may still be needed for false positives and exploitability context.

### Workflow Artifact
- Definition: A downloadable file package produced during a CI workflow run.
- FinWallet usage: Generated TR/EN PDF sets are also uploaded as GitHub Actions artifacts.
- Why / problem solved: It makes binary output easy to inspect/render before merge.
- Note / trade-off: Artifact retention is temporary; final versioned PDFs are also committed to the repository.

## 11. .NET and Application Coding (15 terms)

### .NET 8
- Definition: The target runtime/framework version for FinWallet.
- FinWallet usage: All main APIs and fake-provider projects build on .NET 8.
- Why / problem solved: It provides modern ASP.NET Core, built-in DI, rate limiting, HttpClientFactory and container support.
- Note / trade-off: Framework upgrades require separate compatibility and testing work.

### ASP.NET Core
- Definition: The .NET framework used to build HTTP APIs and web applications.
- FinWallet usage: FinWallet.Gateway, FinWallet.Api and simulator APIs use ASP.NET Core.
- Why / problem solved: It provides Kestrel, middleware, authentication, controllers, DI and configuration.
- Note / trade-off: The business domain is kept independent of framework APIs.

### async/await
- Definition: .NET's asynchronous programming model for waiting on I/O without blocking a thread.
- FinWallet usage: HTTP, SQL, Redis and background-worker I/O paths are asynchronous.
- Why / problem solved: It reduces thread-pool starvation under load.
- Note / trade-off: Async does not automatically make work parallel or transactional.

### Built-in DI Container
- Definition: ASP.NET Core's built-in dependency injection container.
- FinWallet usage: FinWallet uses `IServiceCollection` registration instead of a third-party DI container.
- Why / problem solved: It manages the dependency graph with standard framework mechanisms.
- Note / trade-off: No extra container is introduced without a real need for advanced interception or scoping features.

### CancellationToken
- Definition: A .NET cancellation signal propagated through asynchronous operations.
- FinWallet usage: FinWallet passes it from controllers through handlers to SQL/Redis/HttpClient calls.
- Why / problem solved: It reduces unnecessary I/O after a client disconnects or cancels.
- Note / trade-off: It does not undo an already committed financial transaction; cancellation only stops work that has not durably committed.

### Central Package Management
- Definition: Managing NuGet package versions from one central file instead of each csproj.
- FinWallet usage: A Directory.Packages.props-style setup pins xUnit, Moq, YARP, Swashbuckle and other versions centrally.
- Why / problem solved: It reduces version drift and duplicated version declarations across the solution.
- Note / trade-off: Major upgrades still require compatibility review; central pinning is not automatic security.

### Controller-Based API
- Definition: Defining ASP.NET Core endpoints with Controller classes and attribute routing.
- FinWallet usage: FinWallet uses controllers instead of Minimal APIs.
- Why / problem solved: It keeps authorization, response metadata and organization explicit for a larger API surface.
- Note / trade-off: It can add more boilerplate for very small endpoints.

### Exception Mapping
- Definition: Converting application/domain exceptions into controlled HTTP status and machine-code responses.
- FinWallet usage: Expected failures such as insufficient balance, fraud deny/review, idempotency conflict and provider outage are mapped to ServiceResult responses.
- Why / problem solved: It prevents business errors from surfacing as generic 500s.
- Note / trade-off: Unexpected stack traces are not exposed to clients; they are logged and mapped to a safe 500.

### Immutable Model
- Definition: A model where key financial-history records are not modified after creation.
- FinWallet usage: Ledger journals/entries and completed history are corrected without overwriting originals.
- Why / problem solved: It preserves auditability and reasoning over historical events.
- Note / trade-off: Current-state projections such as Wallet balance can be mutable; the distinction between history and projection matters.

### Machine-Readable Error Code
- Definition: A stable string code allowing clients/operations to identify an error without parsing free-form text.
- FinWallet usage: Examples include `IDEMPOTENCY_CONFLICT`, `FRAUD_REVIEW_REQUIRED` and `RATE_LIMITED`.
- Why / problem solved: Client behavior remains stable even if human-readable messages change or are localized.
- Note / trade-off: A code should not carry conflicting semantics across endpoints; this is why the error-code catalog exists.

### Middleware
- Definition: A cross-cutting component that runs before/after controllers in the HTTP request pipeline.
- FinWallet usage: Shared.Web applies security headers, method/content checks, correlation and rate-limit behavior.
- Why / problem solved: It avoids repeating platform controls in every controller.
- Note / trade-off: Business use-case logic is not moved into middleware.

### NuGet
- Definition: .NET's package/dependency distribution ecosystem.
- FinWallet usage: YARP, Swashbuckle, Moq, xUnit and SQL/Redis client libraries are obtained through NuGet.
- Why / problem solved: It simplifies reusable library/tooling integration.
- Note / trade-off: Package count, licensing and vulnerabilities require review because dependencies create supply-chain risk.

### TimeProvider
- Definition: A .NET abstraction for obtaining time instead of calling `DateTime.UtcNow` directly.
- FinWallet usage: FinWallet uses it for Application logic such as fraud evaluation/review timestamps.
- Why / problem solved: It makes time-dependent tests deterministic.
- Note / trade-off: Differences between application time and DB/provider authoritative time still require consideration.

### Typed HttpClient
- Definition: A .NET pattern combining HttpClient configuration and provider-specific calls in a strongly typed client class.
- FinWallet usage: Bank/Fraud/Cutoff/Campaign/Communication provider clients are registered as typed HttpClients.
- Why / problem solved: It keeps base URL, timeout, handler and provider API calls inside one boundary.
- Note / trade-off: The client class should not contain domain business orchestration.

### XML Documentation Comments
- Definition: C# `/// <summary>` comments used for IDE, Swagger and code documentation.
- FinWallet usage: FinWallet uses a TR/EN documentation standard on public classes/interfaces/methods.
- Why / problem solved: It helps new engineers understand code intent quickly.
- Note / trade-off: Stale comments become misinformation, so documentation updates are part of Definition of Done.

## 12. Terms Deliberately Avoided or Replaced (11 terms)

### ASP.NET Core Identity
- Definition: Microsoft's built-in framework for user, password, token, role and authentication persistence.
- FinWallet usage: FinWallet does not use it because the project implements custom auth/session/refresh behavior.
- Why / problem solved: This gives explicit control over banking-style sessions, refresh rotation, OTP and schema behavior.
- Note / trade-off: In production, a battle-tested identity solution can reduce risk; custom authentication carries more security responsibility.

### Blind Retry
- Definition: Automatically retrying an operation without understanding its side effects or idempotency semantics.
- FinWallet usage: FinWallet avoids it for financial provider POSTs.
- Why / problem solved: If the first request succeeds at the provider but the response is lost, a blind retry can duplicate money movement.
- Note / trade-off: Retry becomes safe only with stable provider idempotency keys and explicit operation-status semantics.

### CQRS
- Definition: An approach separating command/write models from query/read models.
- FinWallet usage: FinWallet does not implement a full CQRS framework, though write handlers and history read handlers are naturally separated.
- Why / problem solved: V1 prefers simple Application handlers instead of extra mediator/read-store complexity.
- Note / trade-off: CQRS may become useful later if read scaling or model divergence grows significantly.

### Direct Balance UPDATE
- Definition: Manually changing a wallet balance column without a matching transaction/ledger entry.
- FinWallet usage: FinWallet documentation explicitly warns against it.
- Why / problem solved: It breaks the relationship between Wallet projection and Ledger, creating unexplained money and reconciliation mismatches.
- Note / trade-off: Even test funding should use a balanced provider/ledger fixture or an official financial posting flow.

### Distributed Transaction
- Definition: The problem of coordinating multiple databases/services as if they committed atomically.
- FinWallet usage: FinWallet does not keep external HTTP inside MSSQL transactions or implement 2PC-style distributed transactions.
- Why / problem solved: It uses local ACID, idempotency, compensation and reconciliation to reduce locking and operational complexity.
- Note / trade-off: Explicit eventual consistency is accepted instead of synchronous cross-system atomicity.

### Event Sourcing
- Definition: An architecture where domain events are the source of truth and current state is rebuilt from them.
- FinWallet usage: FinWallet does not use Event Sourcing; it has immutable double-entry Ledger history but the entire domain is not event-sourced.
- Why / problem solved: Ledger provides accounting auditability while Wallet current state and relational tables keep operations/querying practical.
- Note / trade-off: A ledger is not the same thing as Event Sourcing.

### Generic Repository
- Definition: A generic repository abstraction exposing the same CRUD interface for every entity.
- FinWallet usage: FinWallet avoids it on financial paths and uses use-case-specific boundaries such as `IWalletTransferPostingStore` and `IFraudEventStore`.
- Why / problem solved: This prevents financial transaction, locking and idempotency semantics from being hidden behind generic CRUD.
- Note / trade-off: Generic abstractions can suit simple CRUD, but they were not chosen for critical financial paths.

### MediatR
- Definition: A popular .NET mediator library for dispatching requests to handlers.
- FinWallet usage: FinWallet deliberately does not use it; controllers invoke handlers directly through DI.
- Why / problem solved: This keeps call paths visible and avoids an unnecessary abstraction/package layer.
- Note / trade-off: A mediator can be reconsidered if cross-cutting handler pipelines become genuinely complex.

### Microservices
- Definition: A distributed architecture where services can be deployed and scaled independently.
- FinWallet usage: FinWallet deliberately avoids microservices for the v1 financial core while keeping fake external providers as separate HTTP services.
- Why / problem solved: Avoiding premature separation of Wallet/Ledger/Transaction reduces distributed-consistency cost for atomic money movement.
- Note / trade-off: Boundaries can be reconsidered if independent scaling or team ownership becomes a real requirement.

### Redis as Financial Source of Truth
- Definition: Using Redis as the sole authoritative store for wallet/ledger financial state.
- FinWallet usage: FinWallet deliberately avoids this.
- Why / problem solved: MSSQL remains authoritative so Redis restart, eviction or operational behavior cannot define money correctness.
- Note / trade-off: Redis is a performance/transient-support layer, not financial truth or audit history.

### Two-Phase Commit (2PC)
- Definition: A protocol coordinating multiple resource managers through prepare and commit phases.
- FinWallet usage: FinWallet v1 does not use it for bank/provider integrations.
- Why / problem solved: Local transaction patterns are preferred because providers typically do not support 2PC and the protocol adds availability/complexity costs.
- Note / trade-off: Most real HTTP banking providers are not distributed XA participants.
