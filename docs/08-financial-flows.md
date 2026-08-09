# Finansal Akışlar / Financial Flows

## Türkçe

### Genel kural
Hiçbir para hareketi Ledger'ı atlayamaz. Wallet current balance operasyonel state'tir; finansal hareketin açıklanabilir tarihi Ledger ve FinancialTransaction kayıtlarıdır.

### 1. Wallet-to-Wallet Transfer — uygulanmış
Endpoint: `POST /api/v1/transfers`.

Akış:
```text
Gateway JWT
-> API JWT + durable session
-> completed idempotency replay
-> server-side risk signals
-> internal fraud
-> external FakeFraud via Gateway
-> final Allow
-> one MSSQL financial transaction
   -> idempotency
   -> source debit
   -> destination credit
   -> FinancialTransaction
   -> LedgerJournal + LedgerEntries
-> commit
```

Muhasebe:
```text
Debit   Source Wallet Liability
Credit  Destination Wallet Liability
```

Aynı request/key replay edildiğinde para ikinci kez hareket etmez.

### 2. BankDeposit — planlanan sıradaki akış
Amaç external bank movement onaylandıktan sonra FinWallet wallet'ını fonlamaktır. Public endpoint henüz yoktur.

Önerilen lifecycle:
```text
Created
-> Fraud/Cutoff if required
-> BankPending
-> BankConfirmed
-> LedgerPost
-> Completed
```

External bank confirmation olmadan wallet'a doğrudan bakiye eklenmemelidir. Deposit posting, provider-side asset/clearing hesabı ile customer wallet liability arasında balanced journal oluşturmalıdır.

### 3. BankWithdrawal — planlanan
Withdrawal uzun external workflow olduğu için available balance'ın önce block edilmesi gerekir.

Önerilen state:
```text
Available -> Blocked -> external bank pending
                      -> success: settle
                      -> failure: release/compensate
```

SQL transaction external HTTP süresince açık tutulmaz.

### 4. Refund / Reversal — planlanan public flow
Refund business-level iadedir; reversal daha çok önceki finansal etkinin muhasebesel ters kaydıdır. Orijinal transaction/journal silinmez veya mutate edilmez. Yeni FinancialTransaction + ters journal oluşturulur.

### 5. Campaign / Merchant Purchase — planlanan
FakeCampaign eligibility/discount hesaplayabilir; fakat discount'ın ekonomik etkisinin customer/merchant/sponsor hesaplarına doğru yansıması FinWallet ledger sorumluluğudur.

### 6. Fraud Review
`Review` kararı bugün para hareketi oluşturmaz. Durable FraudEvents/manual-review queue henüz uygulanmamıştır.

### 7. Cutoff
FakeCutoff bank/currency/transaction type için processing/settlement uygunluğunu hesaplar. Bu entegrasyon özellikle BankDeposit/BankWithdrawal için planlanmıştır.

### 8. Notification
Financial commit tamamlandıktan sonraki notification başarısızlığı para transaction'ını geri almamalıdır. Bu nedenle güvenilir notification için Outbox yaklaşımı planlanmıştır.

### 9. Reconciliation
Planlanan kontroller:
- wallet current state vs ledger-derived balance;
- internal bank-related transactions vs FakeBank statement;
- idempotency/transaction/ledger orphan kayıtları.

Mismatch sessiz UPDATE ile düzeltilmez; issue olarak kaydedilir.

### 10. Bugünkü demo sınırı
Yeni wallet sıfır bakiye ile açılır. Public BankDeposit olmadığı için sadece public endpointlerle yeni customer oluşturup kaynak wallet'ı fonlayarak transfer yapmak henüz mümkün değildir. Integration fixture balance + FinancialTransaction + balanced LedgerJournal/Entries'i atomik üretmelidir; sadece `Wallets.AvailableBalance` update etmek geçersizdir.

---

## English

### General rule
No money movement may bypass the Ledger. Wallet current balance is operational state; Ledger and FinancialTransaction records provide the explainable financial history.

### 1. Wallet-to-Wallet Transfer — implemented
Endpoint: `POST /api/v1/transfers`.

Flow:
```text
Gateway JWT
-> API JWT + durable session
-> completed idempotency replay
-> server-side risk signals
-> internal fraud
-> external FakeFraud via Gateway
-> final Allow
-> one MSSQL financial transaction
   -> idempotency
   -> source debit
   -> destination credit
   -> FinancialTransaction
   -> LedgerJournal + LedgerEntries
-> commit
```

Accounting:
```text
Debit   Source Wallet Liability
Credit  Destination Wallet Liability
```

Replaying the same request/key never moves money a second time.

### 2. BankDeposit — planned next flow
The purpose is to fund a FinWallet wallet after an external bank movement has been confirmed. No public endpoint exists yet.

Proposed lifecycle:
```text
Created
-> Fraud/Cutoff if required
-> BankPending
-> BankConfirmed
-> LedgerPost
-> Completed
```

The wallet must not be credited before external confirmation. Deposit posting should create a balanced journal between a provider-side asset/clearing account and the customer wallet liability.

### 3. BankWithdrawal — planned
Withdrawal is a long-running external workflow, so available funds should first move into blocked state.

Proposed state:
```text
Available -> Blocked -> external bank pending
                      -> success: settle
                      -> failure: release/compensate
```

The SQL transaction must not remain open during external HTTP.

### 4. Refund / Reversal — planned public flow
Refund is a business-level return, while reversal is the accounting inverse of a previous financial effect. The original transaction/journal is not deleted or mutated. A new FinancialTransaction and reverse journal are created.

### 5. Campaign / Merchant Purchase — planned
FakeCampaign may calculate eligibility and discount, but FinWallet Ledger remains responsible for correctly allocating the economic effect across customer, merchant and sponsor accounts.

### 6. Fraud Review
A `Review` decision currently moves no money. Durable FraudEvents/manual-review queueing is not implemented yet.

### 7. Cutoff
FakeCutoff evaluates processing/settlement eligibility by bank, currency and transaction type. This integration is mainly planned for BankDeposit and BankWithdrawal.

### 8. Notification
Notification failure after a financial commit must not reverse the money transaction. Reliable post-commit notification is therefore planned around an Outbox pattern.

### 9. Reconciliation
Planned checks include:
- wallet current state vs ledger-derived balance;
- internal bank-related transactions vs FakeBank statement;
- orphaned idempotency/transaction/ledger records.

Mismatches are recorded and investigated rather than silently fixed with an UPDATE.

### 10. Current demo limitation
New wallets start at zero balance. Because public BankDeposit does not yet exist, a new customer cannot currently be fully funded and transferred using only public endpoints. An integration fixture must atomically create balance + FinancialTransaction + balanced LedgerJournal/Entries; directly updating `Wallets.AvailableBalance` is invalid.
