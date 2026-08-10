using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FinWallet.Application.Corrections;
using FinWallet.Domain.Ledger;
using FinWallet.Domain.Shared;
using FinWallet.Domain.Transactions;
using FinWallet.Domain.Wallets;
using Microsoft.Data.SqlClient;

namespace FinWallet.Infrastructure.Persistence.SqlServer;

/// <summary>TR: Purchase Refund ve internal WalletTransfer Reversal işlemlerini original ledger'a dokunmadan ters journal, wallet mutation, durable idempotency ve outbox ile atomik MSSQL transaction içinde uygular. EN: Atomically applies Purchase Refund and internal WalletTransfer Reversal using opposite journals, wallet mutations, durable idempotency and outbox without modifying original ledger history.</summary>
public sealed class SqlTransactionCorrectionStore : ITransactionCorrectionStore
{
    private const byte TransactionCompleted = 2;
    private const byte TransactionReversed = 4;
    private const byte IdempotencyCompleted = 2;
    private readonly SqlConnectionFactory _connectionFactory;
    private readonly TimeProvider _timeProvider;

    /// <summary>TR: SQL connection factory ve UTC zaman kaynağıyla correction store'u oluşturur. EN: Creates correction store with SQL connection factory and UTC time source.</summary>
    /// <param name="connectionFactory">TR: Pooled SQL connection factory. EN: Pooled SQL connection factory.</param>
    /// <param name="timeProvider">TR: Correction posting UTC zaman kaynağı. EN: Correction posting UTC time source.</param>
    public SqlTransactionCorrectionStore(SqlConnectionFactory connectionFactory, TimeProvider timeProvider)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <inheritdoc />
    public async Task<TransactionCorrectionResult> CorrectAsync(TransactionCorrectionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var scope = command.Type == TransactionCorrectionType.Refund ? "REFUND" : "REVERSAL";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{command.Type}|{command.OriginalTransactionId:N}")));
        var now = _timeProvider.GetUtcNow();

        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var replayResource = await FindIdempotencyAsync(connection, transaction, scope, command.CustomerId, command.IdempotencyKey, hash, cancellationToken);
        if (replayResource.HasValue)
        {
            var replay = await LoadCorrectionResultAsync(connection, transaction, replayResource.Value, command.Type, wasReplay: true, cancellationToken)
                ?? throw new InvalidOperationException("Correction idempotency resource is missing.");
            await transaction.CommitAsync(cancellationToken);
            return replay;
        }

        var original = await LoadOriginalAsync(connection, transaction, command.CustomerId, command.OriginalTransactionId, cancellationToken)
            ?? throw new CorrectionTransactionNotFoundException();
        if (original.Status != TransactionCompleted) throw new CorrectionNotAllowedException();
        if (command.Type == TransactionCorrectionType.Refund && original.Type != FinancialTransactionType.Purchase) throw new CorrectionNotAllowedException();
        if (command.Type == TransactionCorrectionType.Reversal && original.Type != FinancialTransactionType.WalletTransfer) throw new CorrectionNotAllowedException();

        var newTransactionId = Guid.NewGuid();
        if (command.Type == TransactionCorrectionType.Refund)
        {
            if (!original.SourceWalletId.HasValue) throw new InvalidOperationException("Purchase source wallet is missing.");
            var wallet = await LoadWalletForUpdateAsync(connection, transaction, original.SourceWalletId.Value, cancellationToken) ?? throw new InvalidOperationException("Purchase wallet is missing.");
            wallet.Credit(original.Amount);
            await UpdateWalletAsync(connection, transaction, wallet, cancellationToken);
            await InsertCorrectionTransactionAsync(connection, transaction, newTransactionId, command.CustomerId, FinancialTransactionType.Refund, sourceWalletId: null, destinationWalletId: wallet.Id, original.Amount, now, cancellationToken);
        }
        else
        {
            if (!original.SourceWalletId.HasValue || !original.DestinationWalletId.HasValue) throw new InvalidOperationException("Wallet-transfer endpoints are missing.");
            var wallets = await LoadWalletPairForUpdateAsync(connection, transaction, original.SourceWalletId.Value, original.DestinationWalletId.Value, cancellationToken);
            if (!wallets.TryGetValue(original.SourceWalletId.Value, out var originalSource) || !wallets.TryGetValue(original.DestinationWalletId.Value, out var originalDestination)) throw new InvalidOperationException("Wallet-transfer wallets are missing.");
            originalDestination.Debit(original.Amount);
            originalSource.Credit(original.Amount);
            await UpdateWalletAsync(connection, transaction, originalDestination, cancellationToken);
            await UpdateWalletAsync(connection, transaction, originalSource, cancellationToken);
            await InsertCorrectionTransactionAsync(connection, transaction, newTransactionId, command.CustomerId, FinancialTransactionType.Reversal, originalDestination.Id, originalSource.Id, original.Amount, now, cancellationToken);
        }

        await InsertCorrectionDetailsAsync(connection, transaction, newTransactionId, original.Id, command.CorrelationId, now, cancellationToken);
        var originalJournal = await LoadOriginalJournalAsync(connection, transaction, original.Id, cancellationToken) ?? throw new InvalidOperationException("Original posted journal is missing.");
        await InsertOppositeJournalAsync(connection, transaction, newTransactionId, originalJournal, original.Amount.Currency, now, cancellationToken);
        await MarkOriginalReversedAsync(connection, transaction, original.Id, now, cancellationToken);
        await InsertIdempotencyAsync(connection, transaction, scope, command, hash, newTransactionId, now, cancellationToken);
        await InsertOutboxAsync(connection, transaction, newTransactionId, command.CustomerId, command.Type, original.Id, original.Amount, now, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new TransactionCorrectionResult(newTransactionId, original.Id, command.Type, original.Amount, now, wasReplay: false);
    }

    private static async Task<Guid?> FindIdempotencyAsync(SqlConnection connection, SqlTransaction transaction, string scope, Guid customerId, string key, string hash, CancellationToken cancellationToken)
    {
        const string sql = "SELECT RequestHash,ResourceId,Status FROM dbo.IdempotencyRecords WITH (UPDLOCK,HOLDLOCK) WHERE Scope=@Scope AND CustomerId=@CustomerId AND IdempotencyKey=@Key;";
        await using var sqlCommand = new SqlCommand(sql, connection, transaction);
        sqlCommand.Parameters.Add("@Scope", SqlDbType.NVarChar, 64).Value = scope;
        sqlCommand.Parameters.Add("@CustomerId", SqlDbType.UniqueIdentifier).Value = customerId;
        sqlCommand.Parameters.Add("@Key", SqlDbType.NVarChar, 128).Value = key;
        await using var reader = await sqlCommand.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(reader.GetString(0)), Encoding.ASCII.GetBytes(hash))) throw new CorrectionIdempotencyConflictException();
        if (reader.GetByte(2) != IdempotencyCompleted || reader.IsDBNull(1)) throw new InvalidOperationException("Correction idempotency state is not replayable.");
        return reader.GetGuid(1);
    }

    private static async Task<OriginalTransaction?> LoadOriginalAsync(SqlConnection connection, SqlTransaction transaction, Guid customerId, Guid originalId, CancellationToken cancellationToken)
    {
        const string sql = "SELECT Id,Type,Status,SourceWalletId,DestinationWalletId,Currency,Amount FROM dbo.FinancialTransactions WITH (UPDLOCK,ROWLOCK) WHERE Id=@Id AND CustomerId=@CustomerId;";
        await using var sqlCommand = new SqlCommand(sql, connection, transaction);
        sqlCommand.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = originalId;
        sqlCommand.Parameters.Add("@CustomerId", SqlDbType.UniqueIdentifier).Value = customerId;
        await using var reader = await sqlCommand.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        var source = reader.GetOrdinal("SourceWalletId"); var destination = reader.GetOrdinal("DestinationWalletId");
        return new OriginalTransaction(
            reader.GetGuid(0),
            (FinancialTransactionType)reader.GetByte(1),
            reader.GetByte(2),
            reader.IsDBNull(source) ? null : reader.GetGuid(source),
            reader.IsDBNull(destination) ? null : reader.GetGuid(destination),
            new Money(reader.GetDecimal(reader.GetOrdinal("Amount")), (CurrencyCode)reader.GetByte(reader.GetOrdinal("Currency"))));
    }

    private static async Task<Wallet?> LoadWalletForUpdateAsync(SqlConnection connection, SqlTransaction transaction, Guid walletId, CancellationToken cancellationToken)
    {
        const string sql = "SELECT Id,CustomerId,Currency,AvailableBalance,BlockedBalance,Status,CreatedAt FROM dbo.Wallets WITH (UPDLOCK,ROWLOCK) WHERE Id=@Id;";
        await using var sqlCommand = new SqlCommand(sql, connection, transaction);
        sqlCommand.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = walletId;
        await using var reader = await sqlCommand.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return Wallet.Restore(reader.GetGuid(0), reader.GetGuid(1), (CurrencyCode)reader.GetByte(2), reader.GetDecimal(3), reader.GetDecimal(4), (WalletStatus)reader.GetByte(5), reader.GetFieldValue<DateTimeOffset>(6));
    }

    private static async Task<IReadOnlyDictionary<Guid, Wallet>> LoadWalletPairForUpdateAsync(SqlConnection connection, SqlTransaction transaction, Guid first, Guid second, CancellationToken cancellationToken)
    {
        var ids = new[] { first, second }; Array.Sort(ids);
        var result = new Dictionary<Guid, Wallet>(2);
        foreach (var id in ids)
        {
            var wallet = await LoadWalletForUpdateAsync(connection, transaction, id, cancellationToken);
            if (wallet is not null) result[id] = wallet;
        }
        return result;
    }

    private static async Task UpdateWalletAsync(SqlConnection connection, SqlTransaction transaction, Wallet wallet, CancellationToken cancellationToken)
    {
        FinancialAmountRules.EnsureStorageCompatible(wallet.AvailableBalance, nameof(wallet.AvailableBalance));
        FinancialAmountRules.EnsureStorageCompatible(wallet.BlockedBalance, nameof(wallet.BlockedBalance));
        const string sql = "UPDATE dbo.Wallets SET AvailableBalance=@Available,BlockedBalance=@Blocked WHERE Id=@Id;";
        await using var sqlCommand = new SqlCommand(sql, connection, transaction);
        AddMoney(sqlCommand, "@Available", wallet.AvailableBalance); AddMoney(sqlCommand, "@Blocked", wallet.BlockedBalance);
        sqlCommand.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = wallet.Id;
        EnsureSingleRow(await sqlCommand.ExecuteNonQueryAsync(cancellationToken), "Correction wallet update");
    }

    private static async Task InsertCorrectionTransactionAsync(SqlConnection connection, SqlTransaction transaction, Guid id, Guid customerId, FinancialTransactionType type, Guid? sourceWalletId, Guid? destinationWalletId, Money amount, DateTimeOffset now, CancellationToken cancellationToken)
    {
        const string sql = "INSERT INTO dbo.FinancialTransactions (Id,CustomerId,Type,Status,SourceWalletId,DestinationWalletId,Currency,Amount,CreatedAt,FinalizedAt,ReversedAt,FailureCode) VALUES (@Id,@CustomerId,@Type,@Status,@Source,@Destination,@Currency,@Amount,@Now,@Now,NULL,NULL);";
        await using var sqlCommand = new SqlCommand(sql, connection, transaction);
        sqlCommand.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = id;
        sqlCommand.Parameters.Add("@CustomerId", SqlDbType.UniqueIdentifier).Value = customerId;
        sqlCommand.Parameters.Add("@Type", SqlDbType.TinyInt).Value = (byte)type;
        sqlCommand.Parameters.Add("@Status", SqlDbType.TinyInt).Value = TransactionCompleted;
        sqlCommand.Parameters.Add("@Source", SqlDbType.UniqueIdentifier).Value = (object?)sourceWalletId ?? DBNull.Value;
        sqlCommand.Parameters.Add("@Destination", SqlDbType.UniqueIdentifier).Value = (object?)destinationWalletId ?? DBNull.Value;
        sqlCommand.Parameters.Add("@Currency", SqlDbType.TinyInt).Value = (byte)amount.Currency;
        AddMoney(sqlCommand, "@Amount", amount.Amount);
        sqlCommand.Parameters.Add("@Now", SqlDbType.DateTimeOffset).Value = now;
        EnsureSingleRow(await sqlCommand.ExecuteNonQueryAsync(cancellationToken), "Correction transaction insert");
    }

    private static async Task InsertCorrectionDetailsAsync(SqlConnection connection, SqlTransaction transaction, Guid transactionId, Guid parentTransactionId, string correlationId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        const string sql = "INSERT INTO dbo.FinancialTransactionDetails (FinancialTransactionId,ParentTransactionId,CorrelationId,CreatedAt) VALUES (@Id,@Parent,@CorrelationId,@Now);";
        await using var sqlCommand = new SqlCommand(sql, connection, transaction);
        sqlCommand.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = transactionId;
        sqlCommand.Parameters.Add("@Parent", SqlDbType.UniqueIdentifier).Value = parentTransactionId;
        sqlCommand.Parameters.Add("@CorrelationId", SqlDbType.NVarChar, 64).Value = correlationId;
        sqlCommand.Parameters.Add("@Now", SqlDbType.DateTimeOffset).Value = now;
        EnsureSingleRow(await sqlCommand.ExecuteNonQueryAsync(cancellationToken), "Correction detail insert");
    }

    private static async Task<OriginalJournal?> LoadOriginalJournalAsync(SqlConnection connection, SqlTransaction transaction, Guid transactionReference, CancellationToken cancellationToken)
    {
        const string journalSql = "SELECT Id FROM dbo.LedgerJournals WITH (UPDLOCK,HOLDLOCK) WHERE TransactionReference=@Reference AND Status=2;";
        Guid journalId;
        await using (var sqlCommand = new SqlCommand(journalSql, connection, transaction))
        {
            sqlCommand.Parameters.Add("@Reference", SqlDbType.UniqueIdentifier).Value = transactionReference;
            var value = await sqlCommand.ExecuteScalarAsync(cancellationToken);
            if (value is not Guid found) return null;
            journalId = found;
        }
        const string entrySql = "SELECT AccountId,Side,Amount FROM dbo.LedgerEntries WHERE JournalId=@JournalId ORDER BY SequenceNumber;";
        var entries = new List<OriginalEntry>();
        await using (var sqlCommand = new SqlCommand(entrySql, connection, transaction))
        {
            sqlCommand.Parameters.Add("@JournalId", SqlDbType.UniqueIdentifier).Value = journalId;
            await using var reader = await sqlCommand.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken)) entries.Add(new OriginalEntry(reader.GetGuid(0), reader.GetByte(1), reader.GetDecimal(2)));
        }
        if (entries.Count < 2) throw new InvalidOperationException("Original journal has insufficient entries.");
        return new OriginalJournal(journalId, entries);
    }

    private static async Task InsertOppositeJournalAsync(SqlConnection connection, SqlTransaction transaction, Guid correctionTransactionId, OriginalJournal original, CurrencyCode currency, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var journalId = Guid.NewGuid();
        const string journalSql = "INSERT INTO dbo.LedgerJournals (Id,TransactionReference,Currency,Status,CreatedAt,PostedAt,ReversesJournalId) VALUES (@Id,@Reference,@Currency,2,@Now,@Now,@OriginalJournalId);";
        await using (var sqlCommand = new SqlCommand(journalSql, connection, transaction))
        {
            sqlCommand.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = journalId;
            sqlCommand.Parameters.Add("@Reference", SqlDbType.UniqueIdentifier).Value = correctionTransactionId;
            sqlCommand.Parameters.Add("@Currency", SqlDbType.TinyInt).Value = (byte)currency;
            sqlCommand.Parameters.Add("@Now", SqlDbType.DateTimeOffset).Value = now;
            sqlCommand.Parameters.Add("@OriginalJournalId", SqlDbType.UniqueIdentifier).Value = original.JournalId;
            EnsureSingleRow(await sqlCommand.ExecuteNonQueryAsync(cancellationToken), "Correction journal insert");
        }
        const string entrySql = "INSERT INTO dbo.LedgerEntries (Id,JournalId,SequenceNumber,AccountId,Side,Amount,Currency) VALUES (@Id,@JournalId,@Sequence,@AccountId,@Side,@Amount,@Currency);";
        short sequence = 0;
        foreach (var entry in original.Entries)
        {
            await using var sqlCommand = new SqlCommand(entrySql, connection, transaction);
            sqlCommand.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = Guid.NewGuid();
            sqlCommand.Parameters.Add("@JournalId", SqlDbType.UniqueIdentifier).Value = journalId;
            sqlCommand.Parameters.Add("@Sequence", SqlDbType.SmallInt).Value = ++sequence;
            sqlCommand.Parameters.Add("@AccountId", SqlDbType.UniqueIdentifier).Value = entry.AccountId;
            sqlCommand.Parameters.Add("@Side", SqlDbType.TinyInt).Value = entry.Side == 1 ? (byte)2 : (byte)1;
            AddMoney(sqlCommand, "@Amount", entry.Amount);
            sqlCommand.Parameters.Add("@Currency", SqlDbType.TinyInt).Value = (byte)currency;
            EnsureSingleRow(await sqlCommand.ExecuteNonQueryAsync(cancellationToken), "Correction ledger entry insert");
        }
        const string verifySql = "SELECT COALESCE(SUM(CASE WHEN Side=1 THEN Amount ELSE 0 END),0),COALESCE(SUM(CASE WHEN Side=2 THEN Amount ELSE 0 END),0) FROM dbo.LedgerEntries WHERE JournalId=@Id;";
        await using var verify = new SqlCommand(verifySql, connection, transaction);
        verify.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = journalId;
        await using var reader = await verify.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) throw new InvalidOperationException("Correction journal verification failed.");
        var debit = reader.GetDecimal(0); var credit = reader.GetDecimal(1);
        if (debit <= 0m || debit != credit) throw new UnbalancedLedgerJournalException(debit, credit);
    }

    private static async Task MarkOriginalReversedAsync(SqlConnection connection, SqlTransaction transaction, Guid originalId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        const string sql = "UPDATE dbo.FinancialTransactions SET Status=@Reversed,ReversedAt=@Now WHERE Id=@Id AND Status=@Completed;";
        await using var sqlCommand = new SqlCommand(sql, connection, transaction);
        sqlCommand.Parameters.Add("@Reversed", SqlDbType.TinyInt).Value = TransactionReversed;
        sqlCommand.Parameters.Add("@Now", SqlDbType.DateTimeOffset).Value = now;
        sqlCommand.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = originalId;
        sqlCommand.Parameters.Add("@Completed", SqlDbType.TinyInt).Value = TransactionCompleted;
        EnsureSingleRow(await sqlCommand.ExecuteNonQueryAsync(cancellationToken), "Original transaction reversal marker");
    }

    private static async Task InsertIdempotencyAsync(SqlConnection connection, SqlTransaction transaction, string scope, TransactionCorrectionCommand command, string hash, Guid resourceId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        const string sql = "INSERT INTO dbo.IdempotencyRecords (Id,Scope,CustomerId,IdempotencyKey,RequestHash,ResourceId,Status,ResultCode,CreatedAt,UpdatedAt) VALUES (@Id,@Scope,@CustomerId,@Key,@Hash,@ResourceId,@Status,@Code,@Now,@Now);";
        await using var sqlCommand = new SqlCommand(sql, connection, transaction);
        sqlCommand.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = Guid.NewGuid();
        sqlCommand.Parameters.Add("@Scope", SqlDbType.NVarChar, 64).Value = scope;
        sqlCommand.Parameters.Add("@CustomerId", SqlDbType.UniqueIdentifier).Value = command.CustomerId;
        sqlCommand.Parameters.Add("@Key", SqlDbType.NVarChar, 128).Value = command.IdempotencyKey;
        sqlCommand.Parameters.Add("@Hash", SqlDbType.Char, 64).Value = hash;
        sqlCommand.Parameters.Add("@ResourceId", SqlDbType.UniqueIdentifier).Value = resourceId;
        sqlCommand.Parameters.Add("@Status", SqlDbType.TinyInt).Value = IdempotencyCompleted;
        sqlCommand.Parameters.Add("@Code", SqlDbType.NVarChar, 64).Value = command.Type == TransactionCorrectionType.Refund ? "REFUND_COMPLETED" : "REVERSAL_COMPLETED";
        sqlCommand.Parameters.Add("@Now", SqlDbType.DateTimeOffset).Value = now;
        EnsureSingleRow(await sqlCommand.ExecuteNonQueryAsync(cancellationToken), "Correction idempotency insert");
    }

    private static async Task InsertOutboxAsync(SqlConnection connection, SqlTransaction transaction, Guid transactionId, Guid customerId, TransactionCorrectionType type, Guid originalId, Money amount, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(new { TransactionId = transactionId, OriginalTransactionId = originalId, CustomerId = customerId, Type = type.ToString(), Amount = amount.Amount, Currency = amount.Currency.ToString() });
        const string sql = "INSERT INTO dbo.OutboxMessages (Id,MessageType,AggregateId,PayloadJson,CreatedAt,AvailableAt,AttemptCount) VALUES (@Id,@Type,@AggregateId,@Payload,@Now,@Now,0);";
        await using var sqlCommand = new SqlCommand(sql, connection, transaction);
        sqlCommand.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = Guid.NewGuid();
        sqlCommand.Parameters.Add("@Type", SqlDbType.NVarChar, 128).Value = type == TransactionCorrectionType.Refund ? "REFUND_COMPLETED" : "REVERSAL_COMPLETED";
        sqlCommand.Parameters.Add("@AggregateId", SqlDbType.UniqueIdentifier).Value = transactionId;
        sqlCommand.Parameters.Add("@Payload", SqlDbType.NVarChar, -1).Value = payload;
        sqlCommand.Parameters.Add("@Now", SqlDbType.DateTimeOffset).Value = now;
        EnsureSingleRow(await sqlCommand.ExecuteNonQueryAsync(cancellationToken), "Correction outbox insert");
    }

    private static async Task<TransactionCorrectionResult?> LoadCorrectionResultAsync(SqlConnection connection, SqlTransaction? transaction, Guid transactionId, TransactionCorrectionType type, bool wasReplay, CancellationToken cancellationToken)
    {
        const string sql = "SELECT t.Id,t.Currency,t.Amount,t.FinalizedAt,d.ParentTransactionId FROM dbo.FinancialTransactions t INNER JOIN dbo.FinancialTransactionDetails d ON d.FinancialTransactionId=t.Id WHERE t.Id=@Id AND t.Status=@Completed;";
        await using var sqlCommand = new SqlCommand(sql, connection, transaction);
        sqlCommand.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = transactionId;
        sqlCommand.Parameters.Add("@Completed", SqlDbType.TinyInt).Value = TransactionCompleted;
        await using var reader = await sqlCommand.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return new TransactionCorrectionResult(reader.GetGuid(0), reader.GetGuid(reader.GetOrdinal("ParentTransactionId")), type, new Money(reader.GetDecimal(reader.GetOrdinal("Amount")), (CurrencyCode)reader.GetByte(reader.GetOrdinal("Currency"))), reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("FinalizedAt")), wasReplay);
    }

    private static void AddMoney(SqlCommand command, string name, decimal value)
    {
        var parameter = command.Parameters.Add(name, SqlDbType.Decimal); parameter.Precision = 19; parameter.Scale = 4; parameter.Value = value;
    }

    private static void EnsureSingleRow(int count, string operation)
    {
        if (count != 1) throw new InvalidOperationException($"{operation} expected one affected row but affected {count}.");
    }

    private sealed record OriginalTransaction(Guid Id, FinancialTransactionType Type, byte Status, Guid? SourceWalletId, Guid? DestinationWalletId, Money Amount);
    private sealed record OriginalEntry(Guid AccountId, byte Side, decimal Amount);
    private sealed record OriginalJournal(Guid JournalId, IReadOnlyCollection<OriginalEntry> Entries);
}
