# Wallet Transfer Akışı / Wallet Transfer Flow

## Türkçe

### Endpoint
`POST /api/v1/transfers`

Gereksinimler:
- valid JWT;
- JWT `sid` ile eşleşen active durable session;
- `Idempotency-Key`;
- JWT `sub` customer'a ait source wallet;
- distinct destination wallet;
- positive `DECIMAL(19,4)` uyumlu amount;
- source/destination currency eşitliği;
- final fraud `Allow`;
- yeterli source balance.

```json
{
  "sourceWalletId": "11111111-1111-1111-1111-111111111111",
  "destinationWalletId": "22222222-2222-2222-2222-222222222222",
  "amount": 125.50
}
```
Currency client'tan trusted alınmaz; wallet state'ten türetilir.

### Idempotency sırası
Completed replay fraud'dan **önce** kontrol edilir:
```text
request
-> completed replay lookup
   -> same key + same immutable payload: original result
   -> same key + different payload: conflict
-> durable session/risk
-> internal fraud
-> external fraud
-> atomic posting
```

Bu precheck semantic/performance guard'dır. Final correctness yine atomic posting store'un Serializable idempotency locking'ine aittir.

### Session validation
Valid JWT tek başına para hareketi için yeterli değildir. `sid`:
- aynı customer'a ait olmalı;
- revoked olmamalı;
- expired olmamalı;
- Active customer session olmalıdır.

### Server-derived fraud signals
Client risk flag göndermez. `SqlWalletTransferRiskSignalStore` server state'ten:
- customer country;
- wallet currency;
- device reference/history;
- new-device;
- 5-minute transfer velocity;
- 24-hour same-currency amount;
- known-beneficiary
üretir.

Raw DeviceId provider'a gönderilmez; stable SHA-256 reference kullanılır.

### Fraud flow
Internal `Deny` external `Allow` ile override edilemez. Internal `Allow/Review` sonrası FakeFraud gerekir. External timeout/network/malformed response fail-closed'dur.

```text
Internal + External -> FraudDecisionPolicy -> Allow / Review / Deny
```
- Allow -> posting;
- Review -> no money movement;
- Deny -> no money movement;
- provider unavailable -> no money movement.

Durable manual-review queue henüz yoktur.

### Atomic posting
Final Allow sonrası tek MSSQL transaction:
- IdempotencyRecord;
- source debit;
- destination credit;
- FinancialTransaction;
- Wallet Liability LedgerAccounts;
- balanced LedgerJournal + LedgerEntries;
- persisted SQL Debit/Credit equality.

External HTTP bu transaction içinde çalışmaz.

### Double-entry
```text
Debit   source wallet liability       amount
Credit  destination wallet liability  amount
```
Domain ve SQL seviyesinde journal balance kontrol edilir.

### Replay response
Replay yalnız immutable alanları döndürür:
- FinancialTransaction ID;
- source/destination wallet IDs;
- amount/currency;
- original completion time;
- replay flag.

Current wallet balance replay body'ye konmaz; daha sonra değişmiş olabilir.

---

## English

### Endpoint
`POST /api/v1/transfers`

Requirements:
- valid JWT;
- active durable session matching JWT `sid`;
- `Idempotency-Key`;
- source wallet owned by JWT `sub` customer;
- distinct destination wallet;
- positive `DECIMAL(19,4)`-compatible amount;
- matching source/destination currency;
- final fraud `Allow`;
- sufficient source balance.

```json
{
  "sourceWalletId": "11111111-1111-1111-1111-111111111111",
  "destinationWalletId": "22222222-2222-2222-2222-222222222222",
  "amount": 125.50
}
```
Currency is not trusted from the client; it is derived from wallet state.

### Idempotency order
Completed replay is checked **before** fraud:
```text
request
-> completed replay lookup
   -> same key + same immutable payload: original result
   -> same key + different payload: conflict
-> durable session/risk
-> internal fraud
-> external fraud
-> atomic posting
```

This precheck is a semantic/performance guard. Final correctness still belongs to Serializable idempotency locking inside the atomic posting store.

### Session validation
A valid JWT alone is not sufficient for money movement. `sid` must:
- belong to the same customer;
- not be revoked;
- not be expired;
- belong to an Active customer session.

### Server-derived fraud signals
The client does not submit risk flags. `SqlWalletTransferRiskSignalStore` derives from server state:
- customer country;
- wallet currency;
- device reference/history;
- new-device flag;
- five-minute transfer velocity;
- 24-hour same-currency amount;
- known beneficiary.

Raw DeviceId is not sent to the provider; a stable SHA-256 reference is used.

### Fraud flow
Internal `Deny` cannot be overridden by external `Allow`. FakeFraud is required after internal `Allow/Review`. External timeout/network/malformed response fails closed.

```text
Internal + External -> FraudDecisionPolicy -> Allow / Review / Deny
```
- Allow -> posting;
- Review -> no money movement;
- Deny -> no money movement;
- provider unavailable -> no money movement.

A durable manual-review queue is not yet implemented.

### Atomic posting
After final Allow, one MSSQL transaction commits:
- IdempotencyRecord;
- source debit;
- destination credit;
- FinancialTransaction;
- Wallet Liability LedgerAccounts;
- balanced LedgerJournal + LedgerEntries;
- persisted SQL Debit/Credit equality validation.

External HTTP never runs inside this transaction.

### Double-entry
```text
Debit   source wallet liability       amount
Credit  destination wallet liability  amount
```
Journal balance is validated at both Domain and SQL levels.

### Replay response
Replay contains only immutable fields:
- FinancialTransaction ID;
- source/destination wallet IDs;
- amount/currency;
- original completion time;
- replay flag.

Current wallet balances are excluded because they may have changed since the original transaction.
