# MSSQL, Redis, HTTP ve Gateway Performans İncelemesi / MSSQL, Redis, HTTP and Gateway Performance Review

## Türkçe

### Özet
Performans değişiklikleri financial correctness'i zayıflatmadan uygulanmalıdır. Operasyonel tuning değerleri mümkün olduğunda configuration-driven'dır; accounting/locking invariant'ları benchmark uğruna gevşetilmez.

### MSSQL connection pooling
SqlClient pooling connection string ile açıktır. Development baseline:
- `Pooling=True`;
- `Min Pool Size=5`;
- `Max Pool Size=100`;
- bounded connect timeout;
- `Load Balance Timeout`;
- application name.

`SqlConnectionFactory` short-lived logical connection üretir; dispose fiziksel connection'ı pool'a döndürür. Production pool size ölçülmüş DB kapasitesine göre ayarlanmalıdır.

### Financial transaction performansı
Wallet transfer aynı transaction içinde source/destination balances, idempotency, FinancialTransaction ve Ledger'ı commit eder. Lock duration önemlidir ama correctness'ten daha önemli değildir. Wallet row'ları deterministic GUID order ile lock edilir; opposite-direction deadlock riskini azaltır.

Isolation level yalnız throughput artırmak için zayıflatılmaz.

### Index yaklaşımı
Mevcut unique/index yapıları hot path'lerin önemli bölümünü destekler. Financial write-heavy tablolara speculative index eklemek insert/update/log maliyetini artırabilir. Yeni index Query Store, execution plan, logical read ve measured p95/p99 kanıtına göre eklenmelidir.

İzlenecek DB metrikleri:
- pool exhaustion;
- login/transfer DB p95/p99;
- deadlock;
- lock wait;
- top logical reads;
- Query Store regression;
- transaction-log growth;
- index usage/fragmentation.

### Redis
Tek `ConnectionMultiplexer` singleton tutulur. Per-request multiplexer socket/performance regression olur.

Configurable değerler:
- `AbortOnConnectFail`;
- connect retry;
- connect timeout;
- sync timeout;
- keep-alive;
- client name.

OTP security fail-closed davranışı performans için gevşetilmez. Redis financial authority değildir.

İzlenecekler: reconnect, latency, timeout, CPU, memory, eviction, network, Lua latency.

### HttpClient
Provider client'ları HttpClientFactory + `SocketsHttpHandler` kullanır:
- pooled connection lifetime;
- pooled idle timeout;
- max connections/server;
- GZip/Deflate/Brotli;
- provider-specific timeout;
- cookies disabled.

Provider çağrıları YARP Gateway'e gider. Financial POST'lara endpoint-specific idempotency kanıtı olmadan otomatik retry eklenmez.

### YARP
Clusterlar `PowerOfTwoChoices` kullanır. Production destination replica sayısı config ile artırılabilir. Performance controls:
- load balancing;
- active health;
- main cluster passive health;
- max destination connections;
- activity timeout;
- HTTP version policy;
- response buffering policy;
- pre-proxy request-size limit.

### Rate limiting
Gateway public limit backend'den daha sıkıdır. Backend ikinci katman bypass/misrouting/internal runaway korumasıdır. Queue default 0; overload sırasında request biriktirmek yerine 429 ile hızlı reddetmek tail latency/memory baskısını azaltır.

### Bilinçli yapılmayan optimizasyonlar
- Wallet balance'ı Redis source of truth yapmak yok.
- Financial SQL isolation'ı körlemesine düşürmek yok.
- Arbitrary financial POST retry yok.
- Authenticated financial response caching yok.
- Unbounded DB/HTTP connection pool yok.
- Fraud check'i latency için bypass etmek yok.
- Reconciliation'ı bozacak ledger denormalization yok.

### Benchmark planı
Ölç:
1. registration RPS;
2. login p50/p95/p99;
3. wallet-list p50/p95/p99;
4. transfer p50/p95/p99;
5. same-wallet concurrent transfer throughput;
6. Gateway overhead vs controlled direct internal baseline;
7. Redis OTP latency;
8. SQL locks/deadlocks;
9. CPU/memory/GC/socket;
10. load sonrası ledger/balance/idempotency reconciliation.

Final balances/ledger reconcile etmeyen benchmark sonucu geçersizdir.

---

## English

### Summary
Performance changes must not weaken financial correctness. Operational tuning is configuration-driven where appropriate; accounting and locking invariants are not relaxed merely for benchmark numbers.

### MSSQL connection pooling
SqlClient pooling is enabled through the connection string. Development baseline:
- `Pooling=True`;
- `Min Pool Size=5`;
- `Max Pool Size=100`;
- bounded connect timeout;
- `Load Balance Timeout`;
- application name.

`SqlConnectionFactory` creates short-lived logical connections; disposal returns physical connections to the pool. Production pool size should be tuned from measured database capacity.

### Financial-transaction performance
Wallet transfer commits source/destination balances, idempotency, FinancialTransaction and Ledger in the same transaction. Lock duration matters, but not more than correctness. Wallet rows are locked in deterministic GUID order to reduce opposite-direction deadlock risk.

Isolation is not weakened solely to improve throughput.

### Index approach
Existing unique/index structures support important hot paths. Speculative indexes on write-heavy financial tables may increase insert/update/log cost. New indexes should be driven by Query Store, execution plans, logical reads and measured p95/p99 evidence.

Monitor:
- pool exhaustion;
- login/transfer DB p95/p99;
- deadlocks;
- lock waits;
- top logical reads;
- Query Store regressions;
- transaction-log growth;
- index usage/fragmentation.

### Redis
A single `ConnectionMultiplexer` remains singleton. Creating a multiplexer per request would be a socket/performance regression.

Configurable values:
- `AbortOnConnectFail`;
- connect retry;
- connect timeout;
- sync timeout;
- keep-alive;
- client name.

OTP fail-closed security is not weakened for performance. Redis is not a financial authority.

Monitor reconnects, latency, timeouts, CPU, memory, evictions, network and Lua latency.

### HttpClient
Provider clients use HttpClientFactory + `SocketsHttpHandler` with:
- pooled connection lifetime;
- pooled idle timeout;
- max connections/server;
- GZip/Deflate/Brotli;
- provider-specific timeout;
- cookies disabled.

Provider calls go through YARP Gateway. Financial POSTs are not automatically retried without endpoint-specific idempotency guarantees.

### YARP
Clusters use `PowerOfTwoChoices`. Production destination replicas can be added through configuration. Performance controls include:
- load balancing;
- active health;
- passive health for the main cluster;
- max destination connections;
- activity timeout;
- HTTP version policy;
- response-buffering policy;
- request-size limits before proxying.

### Rate limiting
Gateway public limits are stricter than backend limits. Backend limits provide a second layer against bypass, misrouting and internal runaway traffic. Queue defaults to zero; fast 429 rejection during overload reduces memory pressure and tail latency compared with building a large in-memory queue.

### Optimizations deliberately not made
- No Redis wallet balance as source of truth.
- No blind weakening of financial SQL isolation.
- No automatic retry for arbitrary financial POSTs.
- No authenticated financial-response caching.
- No unbounded DB/HTTP connection pool.
- No bypassing fraud checks for latency.
- No ledger denormalization that would break reconciliation.

### Benchmark plan
Measure:
1. registration RPS;
2. login p50/p95/p99;
3. wallet-list p50/p95/p99;
4. transfer p50/p95/p99;
5. same-wallet concurrent transfer throughput;
6. Gateway overhead versus a controlled direct internal baseline;
7. Redis OTP latency;
8. SQL locks/deadlocks;
9. CPU/memory/GC/socket counts;
10. ledger/balance/idempotency reconciliation after load.

A benchmark result is invalid if final balances and ledger state do not reconcile.
