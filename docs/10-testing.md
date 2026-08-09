# Test Stratejisi / Testing Strategy

## Türkçe

### Güncel durum
Gateway/platform hardening öncesinde solution'da test projesi ve Moq kullanımı yoktu. Artık `tests/FinWallet.Application.Tests` vardır ve:
- xUnit v3;
- Moq;
- .NET 8 / Microsoft Testing Platform
kullanır.

İlk strict-mock test `OpenBankAccountHandler` üzerinde `IWalletStore`, `IBankAccountStore` ve `IBankProvider` bağımlılıklarını mock eder; owned wallet bulunmazsa external bank provider'ın hiç çağrılmadığını doğrular.

### Neyi mock etmeliyiz?
Application orchestration testlerinde davranışı testin konusu olmayan boundary'ler mock edilebilir:
- `IBankProvider`;
- `IExternalFraudProvider`;
- `ICommunicationGateway`;
- persistence interface'leri;
- zaman bağımlılığı için `TimeProvider`.

Financial orchestration'da strict mock tercih edilir; beklenmeyen provider/DB çağrısı önemli bug olabilir.

### Mock ile kanıtlanamayacaklar
Aşağıdakiler gerçek infrastructure integration/concurrency test ister:
- MSSQL rollback;
- Serializable / `UPDLOCK` / `HOLDLOCK`;
- unique-key race;
- refresh-token compare-and-set;
- deterministic wallet lock ordering;
- idempotency range locking;
- persisted Debit=Credit aggregate check;
- connection pooling behavior;
- Redis Lua atomicity/reconnect/fail-closed;
- YARP route/auth/health/load balancing;
- Gateway direct-bypass rejection.

### Gerekli integration suite
En az:
1. concurrent duplicate registration;
2. concurrent refresh rotation;
3. aynı Idempotency-Key ile 10 transfer;
4. same key + different payload;
5. tek source wallet'tan 100 concurrent debit;
6. A->B ve B->A opposite-direction deadlock testi;
7. OTP verify sırasında Redis unavailable;
8. transfer öncesi FakeFraud timeout;
9. Gateway JWT yok;
10. backend downstream key yok;
11. rate-limit rejection;
12. ledger imbalance injection -> rollback.

### Finansal invariant testi
Load test yalnız HTTP success count'a güvenmez:
```text
Initial balances
+ durable credits
- durable debits
= final balances

SUM(Ledger Debit) = SUM(Ledger Credit)
```
Ayrıca successful FinancialTransactions, IdempotencyRecords ve LedgerJournals reconcile etmelidir.

### CI policy
CI:
```text
restore
-> Release build --warnaserror
-> dotnet test --no-build
```
çalıştırır. Yeni project `FinWallet.sln` içine eklenmeden feature tamamlanmış sayılmaz.

### Mevcut coverage yorumu
Unit-test altyapısı artık vardır ancak coverage halen başlangıç seviyesindedir. “Moq eklendi” gerçek MSSQL/Redis/YARP doğruluğu kanıtlandı anlamına gelmez.

---

## English

### Current status
Before Gateway/platform hardening, the solution had no test project and no Moq usage. It now contains `tests/FinWallet.Application.Tests` using:
- xUnit v3;
- Moq;
- .NET 8 / Microsoft Testing Platform.

The first strict-mock test targets `OpenBankAccountHandler`, mocks `IWalletStore`, `IBankAccountStore` and `IBankProvider`, and proves that the external bank provider is not called when the owned wallet is missing.

### What should be mocked?
Application orchestration tests may mock boundaries whose behavior is not the subject of the test:
- `IBankProvider`;
- `IExternalFraudProvider`;
- `ICommunicationGateway`;
- persistence interfaces;
- `TimeProvider` for time-dependent behavior.

Strict mocks are preferred for financial orchestration because an unexpected provider/database call may be a meaningful defect.

### What cannot be proven with mocks?
The following require real infrastructure integration/concurrency tests:
- MSSQL rollback;
- Serializable / `UPDLOCK` / `HOLDLOCK`;
- unique-key races;
- refresh-token compare-and-set;
- deterministic wallet lock ordering;
- idempotency range locking;
- persisted Debit=Credit aggregate validation;
- connection-pooling behavior;
- Redis Lua atomicity/reconnect/fail-closed behavior;
- YARP routing/auth/health/load balancing;
- Gateway direct-bypass rejection.

### Required integration suite
At minimum:
1. concurrent duplicate registration;
2. concurrent refresh rotation;
3. ten transfers with the same Idempotency-Key;
4. same key + different payload;
5. one hundred concurrent debits from one source wallet;
6. opposite-direction A->B and B->A deadlock test;
7. Redis unavailable during OTP verify;
8. FakeFraud timeout before transfer posting;
9. Gateway request without JWT;
10. backend request without downstream key;
11. rate-limit rejection;
12. ledger imbalance injection -> rollback.

### Financial invariant test
Load tests must not trust HTTP success counts alone:
```text
Initial balances
+ durable credits
- durable debits
= final balances

SUM(Ledger Debit) = SUM(Ledger Credit)
```
Successful FinancialTransactions, IdempotencyRecords and LedgerJournals must also reconcile.

### CI policy
CI runs:
```text
restore
-> Release build --warnaserror
-> dotnet test --no-build
```
A new project is not considered complete until it is included in `FinWallet.sln`.

### Current coverage assessment
Unit-test infrastructure now exists, but coverage is still at an early stage. “Moq is present” does not mean real MSSQL/Redis/YARP correctness has been proven.
