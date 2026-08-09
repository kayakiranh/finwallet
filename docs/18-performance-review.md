# MSSQL, Redis, HTTP and Gateway Performance Review

## Summary

The platform review found several useful optimizations that do not weaken financial correctness. These are now configuration-driven where appropriate.

## MSSQL

### Connection pooling

`Microsoft.Data.SqlClient` pooling is explicitly enabled through the connection string. Development defaults include:

- `Pooling=True`;
- `Min Pool Size=5`;
- `Max Pool Size=100`;
- bounded connect timeout;
- `Load Balance Timeout`;
- application name.

Production must provide the real connection string through secrets/environment. Pool size should be tuned from measured database capacity, not increased blindly.

The existing `SqlConnectionFactory` creates short-lived logical `SqlConnection` objects. This is compatible with connection pooling: callers dispose logical connections while the provider reuses physical connections.

### Transactions

Financial correctness takes priority over reducing lock duration. Wallet-transfer posting intentionally uses a strong transactional boundary because the following values must commit together:

- source/destination balances;
- durable idempotency state;
- FinancialTransaction;
- LedgerJournal;
- LedgerEntries.

Row locks use deterministic wallet ordering to reduce opposite-direction deadlock risk. Relaxing isolation merely for benchmark throughput is not acceptable without proving equivalent invariants.

### Existing index strategy

The existing schemas already provide indexes/unique constraints for primary hot paths, including:

- normalized customer phone uniqueness;
- session/customer expiration lookup;
- refresh-token hash/session lookup;
- one wallet per customer/currency;
- one bank account per wallet;
- financial transaction customer/time lookup;
- durable idempotency scope/customer/key;
- ledger account/journal relationships.

No speculative index was added during this review. Extra indexes increase write cost on exactly the tables that financial posting updates. New indexes should be driven by Query Store/execution-plan evidence.

### Recommended production checks

Measure:

- pool exhaustion;
- login/transfer p95 and p99 DB duration;
- deadlock count;
- lock wait time;
- top logical-read queries;
- Query Store regressions;
- transaction-log growth;
- index usage and fragmentation.

## Redis

A single shared `ConnectionMultiplexer` remains registered as a singleton. Creating a new multiplexer per request would be a performance and socket-management regression.

The Redis connection is now built from `ConfigurationOptions` with appsettings-driven:

- `AbortOnConnectFail`;
- connect retry count;
- connect timeout;
- synchronous timeout;
- keep-alive;
- client name.

OTP verification still fails closed. Performance tuning must never make Redis authoritative for customer activation or financial state.

Recommended production monitoring:

- reconnect count;
- command latency;
- timeout count;
- server CPU;
- used memory;
- evictions;
- network bytes;
- Lua execution latency.

## HttpClient

Provider clients use the .NET HttpClient factory and `SocketsHttpHandler` with configurable:

- pooled connection lifetime;
- pooled idle timeout;
- max connections per server;
- GZip/Deflate/Brotli decompression;
- provider-specific overall timeout;
- cookies disabled.

All current provider calls point to YARP rather than directly to simulator hosts. This centralizes routing/health/load balancing while HttpClientFactory still manages caller-side connection reuse.

Financial POST requests are not blindly retried. A retry policy without endpoint-specific idempotency can duplicate external side effects.

## YARP

Gateway clusters use `PowerOfTwoChoices`. The default development config has one destination, while production can add replicas entirely through configuration.

Performance controls include:

- cluster load balancing;
- active health checks;
- passive health for the main FinWallet cluster;
- max destination connections;
- activity timeout;
- HTTP version policy;
- response buffering disabled for the main API;
- request-size limits before proxying.

## Rate limiting

Gateway rate limiting is intentionally stricter than backend limits. The gateway absorbs public L7 abuse first; backend limits provide a second boundary for bypass/misrouting/internal runaway traffic.

Queue length defaults to zero. For financial APIs it is generally safer to reject overload quickly with 429 than to accumulate a large in-memory request queue and increase tail latency.

## Performance changes deliberately not made

- No caching of wallet balances in Redis as source of truth.
- No weaker SQL isolation for money movement.
- No automatic retry for arbitrary financial POSTs.
- No response caching for authenticated financial APIs.
- No unbounded connection pool.
- No removal of fraud checks for latency.
- No ledger denormalization that could break reconciliation.

## Next benchmark plan

Use a controlled environment and report at least:

1. registration requests/second;
2. login p50/p95/p99;
3. wallet list p50/p95/p99;
4. transfer p50/p95/p99;
5. concurrent same-wallet transfer throughput;
6. gateway overhead versus direct internal baseline;
7. Redis OTP latency;
8. SQL lock/deadlock counters;
9. CPU, memory, GC and socket counts;
10. reconciliation success after load.

A throughput result is invalid if final wallet balances, idempotency rows and ledger entries do not reconcile.
