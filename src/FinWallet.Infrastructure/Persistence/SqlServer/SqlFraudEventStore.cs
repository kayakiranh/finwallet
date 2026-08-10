using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FinWallet.Application.Fraud;
using FinWallet.Domain.Fraud;
using Microsoft.Data.SqlClient;

namespace FinWallet.Infrastructure.Persistence.SqlServer;

/// <summary>
/// TR: Internal/external fraud kararlarını ve manual review audit state'ini `FraudEvents` tablosunda durable/idempotent biçimde saklar; aynı financial command'ın tekrarında fraud provider'ın gereksiz yeniden çağrılmasını önler.
/// EN: Stores internal/external fraud decisions and manual-review audit state durably/idempotently in `FraudEvents`, preventing unnecessary fraud-provider reevaluation on replay of the same financial command.
/// </summary>
public sealed class SqlFraudEventStore : IFraudEventStore
{
    private readonly SqlConnectionFactory _connectionFactory;

    /// <summary>TR: Pooled SQL connection factory ile fraud event store oluşturur. EN: Creates fraud-event store with pooled SQL connection factory.</summary>
    /// <param name="connectionFactory">TR: SQL connection factory. EN: SQL connection factory.</param>
    public SqlFraudEventStore(SqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    /// <inheritdoc />
    public async Task<FraudEventRecord?> FindAsync(string operation, Guid customerId, string idempotencyKey, string requestHash, CancellationToken cancellationToken)
    {
        ValidateIdentity(operation, customerId, idempotencyKey, requestHash);
        const string sql = "SELECT Id,CustomerId,Operation,IdempotencyKey,RequestHash,InternalDecision,ExternalDecision,FinalDecision,ReasonCodes,ReviewStatus,CreatedAt,ReviewedAt,ReviewedBy FROM dbo.FraudEvents WHERE Operation=@Operation AND CustomerId=@CustomerId AND IdempotencyKey=@Key;";
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = CreateIdentityCommand(sql, connection, transaction: null, operation, customerId, idempotencyKey);
        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        EnsureRequestHash(reader.GetString(reader.GetOrdinal("RequestHash")), requestHash);
        return ReadRecord(reader);
    }

    /// <inheritdoc />
    public async Task<FraudEventRecord> SaveAsync(string operation, Guid customerId, string idempotencyKey, string requestHash, FraudDecision internalDecision, FraudDecision? externalDecision, FraudDecision finalDecision, IReadOnlyCollection<string> reasonCodes, DateTimeOffset createdAt, CancellationToken cancellationToken)
    {
        ValidateIdentity(operation, customerId, idempotencyKey, requestHash);
        ArgumentNullException.ThrowIfNull(reasonCodes);
        var reasonsJson = SerializeReasons(reasonCodes);
        var reviewState = finalDecision == FraudDecision.Review ? FraudReviewState.Pending : FraudReviewState.NotRequired;

        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        const string selectSql = "SELECT Id,CustomerId,Operation,IdempotencyKey,RequestHash,InternalDecision,ExternalDecision,FinalDecision,ReasonCodes,ReviewStatus,CreatedAt,ReviewedAt,ReviewedBy FROM dbo.FraudEvents WITH (UPDLOCK,HOLDLOCK) WHERE Operation=@Operation AND CustomerId=@CustomerId AND IdempotencyKey=@Key;";
        await using (var select = CreateIdentityCommand(selectSql, connection, transaction, operation, customerId, idempotencyKey))
        await using (var reader = await select.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken))
        {
            if (await reader.ReadAsync(cancellationToken))
            {
                EnsureRequestHash(reader.GetString(reader.GetOrdinal("RequestHash")), requestHash);
                var existing = ReadRecord(reader);
                await reader.DisposeAsync();
                await transaction.CommitAsync(cancellationToken);
                return existing;
            }
        }

        var id = Guid.NewGuid();
        const string insertSql = """
            INSERT INTO dbo.FraudEvents
                (Id,CustomerId,Operation,IdempotencyKey,RequestHash,TransactionId,InternalDecision,ExternalDecision,FinalDecision,ReasonCodes,ReviewStatus,CreatedAt,ReviewedAt,ReviewedBy)
            VALUES
                (@Id,@CustomerId,@Operation,@Key,@RequestHash,NULL,@InternalDecision,@ExternalDecision,@FinalDecision,@ReasonCodes,@ReviewStatus,@CreatedAt,NULL,NULL);
            """;
        await using (var insert = new SqlCommand(insertSql, connection, transaction))
        {
            insert.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = id;
            insert.Parameters.Add("@CustomerId", SqlDbType.UniqueIdentifier).Value = customerId;
            insert.Parameters.Add("@Operation", SqlDbType.NVarChar, 64).Value = operation.Trim();
            insert.Parameters.Add("@Key", SqlDbType.NVarChar, 128).Value = idempotencyKey.Trim();
            insert.Parameters.Add("@RequestHash", SqlDbType.Char, 64).Value = requestHash.Trim();
            insert.Parameters.Add("@InternalDecision", SqlDbType.TinyInt).Value = (byte)internalDecision;
            insert.Parameters.Add("@ExternalDecision", SqlDbType.TinyInt).Value = externalDecision.HasValue ? (byte)externalDecision.Value : DBNull.Value;
            insert.Parameters.Add("@FinalDecision", SqlDbType.TinyInt).Value = (byte)finalDecision;
            insert.Parameters.Add("@ReasonCodes", SqlDbType.NVarChar, 1024).Value = reasonsJson;
            insert.Parameters.Add("@ReviewStatus", SqlDbType.TinyInt).Value = (byte)reviewState;
            insert.Parameters.Add("@CreatedAt", SqlDbType.DateTimeOffset).Value = createdAt;
            if (await insert.ExecuteNonQueryAsync(cancellationToken) != 1) throw new InvalidOperationException("Fraud event insert did not affect exactly one row.");
        }

        await transaction.CommitAsync(cancellationToken);
        return new FraudEventRecord(id, customerId, operation.Trim(), idempotencyKey.Trim(), requestHash.Trim(), internalDecision, externalDecision, finalDecision, ParseReasons(reasonsJson), reviewState, createdAt, null, null);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<FraudEventRecord>> ListPendingAsync(int take, CancellationToken cancellationToken)
    {
        if (take < 1 || take > 100) throw new ArgumentOutOfRangeException(nameof(take));
        const string sql = "SELECT TOP (@Take) Id,CustomerId,Operation,IdempotencyKey,RequestHash,InternalDecision,ExternalDecision,FinalDecision,ReasonCodes,ReviewStatus,CreatedAt,ReviewedAt,ReviewedBy FROM dbo.FraudEvents WHERE ReviewStatus=@Pending ORDER BY CreatedAt,Id;";
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@Take", SqlDbType.Int).Value = take;
        command.Parameters.Add("@Pending", SqlDbType.TinyInt).Value = (byte)FraudReviewState.Pending;
        var result = new List<FraudEventRecord>(take);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) result.Add(ReadRecord(reader));
        return result;
    }

    /// <inheritdoc />
    public async Task<FraudEventRecord> ReviewAsync(Guid fraudEventId, bool approve, string reviewedBy, DateTimeOffset reviewedAt, CancellationToken cancellationToken)
    {
        if (fraudEventId == Guid.Empty) throw new ArgumentException("FraudEvent identifier cannot be empty.", nameof(fraudEventId));
        ArgumentException.ThrowIfNullOrWhiteSpace(reviewedBy);
        var reviewer = reviewedBy.Trim();
        if (reviewer.Length > 128) throw new ArgumentOutOfRangeException(nameof(reviewedBy));

        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        const string selectSql = "SELECT Id,CustomerId,Operation,IdempotencyKey,RequestHash,InternalDecision,ExternalDecision,FinalDecision,ReasonCodes,ReviewStatus,CreatedAt,ReviewedAt,ReviewedBy FROM dbo.FraudEvents WITH (UPDLOCK,ROWLOCK) WHERE Id=@Id;";
        FraudEventRecord current;
        await using (var select = new SqlCommand(selectSql, connection, transaction))
        {
            select.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = fraudEventId;
            await using var reader = await select.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);
            if (!await reader.ReadAsync(cancellationToken)) throw new FraudEventNotFoundException();
            current = ReadRecord(reader);
        }
        if (current.ReviewState != FraudReviewState.Pending) throw new FraudEventReviewConflictException();

        var reviewState = approve ? FraudReviewState.Approved : FraudReviewState.Denied;
        var finalDecision = approve ? FraudDecision.Allow : FraudDecision.Deny;
        const string updateSql = "UPDATE dbo.FraudEvents SET FinalDecision=@FinalDecision,ReviewStatus=@ReviewStatus,ReviewedAt=@ReviewedAt,ReviewedBy=@ReviewedBy WHERE Id=@Id AND ReviewStatus=@Pending;";
        await using (var update = new SqlCommand(updateSql, connection, transaction))
        {
            update.Parameters.Add("@FinalDecision", SqlDbType.TinyInt).Value = (byte)finalDecision;
            update.Parameters.Add("@ReviewStatus", SqlDbType.TinyInt).Value = (byte)reviewState;
            update.Parameters.Add("@ReviewedAt", SqlDbType.DateTimeOffset).Value = reviewedAt;
            update.Parameters.Add("@ReviewedBy", SqlDbType.NVarChar, 128).Value = reviewer;
            update.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = fraudEventId;
            update.Parameters.Add("@Pending", SqlDbType.TinyInt).Value = (byte)FraudReviewState.Pending;
            if (await update.ExecuteNonQueryAsync(cancellationToken) != 1) throw new FraudEventReviewConflictException();
        }

        await transaction.CommitAsync(cancellationToken);
        return new FraudEventRecord(current.Id, current.CustomerId, current.Operation, current.IdempotencyKey, current.RequestHash, current.InternalDecision, current.ExternalDecision, finalDecision, current.ReasonCodes, reviewState, current.CreatedAt, reviewedAt, reviewer);
    }

    private static SqlCommand CreateIdentityCommand(string sql, SqlConnection connection, SqlTransaction? transaction, string operation, Guid customerId, string idempotencyKey)
    {
        var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@Operation", SqlDbType.NVarChar, 64).Value = operation.Trim();
        command.Parameters.Add("@CustomerId", SqlDbType.UniqueIdentifier).Value = customerId;
        command.Parameters.Add("@Key", SqlDbType.NVarChar, 128).Value = idempotencyKey.Trim();
        return command;
    }

    private static FraudEventRecord ReadRecord(SqlDataReader reader)
    {
        var externalOrdinal = reader.GetOrdinal("ExternalDecision");
        var reviewedAtOrdinal = reader.GetOrdinal("ReviewedAt");
        var reviewedByOrdinal = reader.GetOrdinal("ReviewedBy");
        var reasonsOrdinal = reader.GetOrdinal("ReasonCodes");
        return new FraudEventRecord(
            reader.GetGuid(reader.GetOrdinal("Id")),
            reader.GetGuid(reader.GetOrdinal("CustomerId")),
            reader.GetString(reader.GetOrdinal("Operation")),
            reader.GetString(reader.GetOrdinal("IdempotencyKey")),
            reader.GetString(reader.GetOrdinal("RequestHash")),
            (FraudDecision)reader.GetByte(reader.GetOrdinal("InternalDecision")),
            reader.IsDBNull(externalOrdinal) ? null : (FraudDecision)reader.GetByte(externalOrdinal),
            (FraudDecision)reader.GetByte(reader.GetOrdinal("FinalDecision")),
            reader.IsDBNull(reasonsOrdinal) ? Array.Empty<string>() : ParseReasons(reader.GetString(reasonsOrdinal)),
            (FraudReviewState)reader.GetByte(reader.GetOrdinal("ReviewStatus")),
            reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("CreatedAt")),
            reader.IsDBNull(reviewedAtOrdinal) ? null : reader.GetFieldValue<DateTimeOffset>(reviewedAtOrdinal),
            reader.IsDBNull(reviewedByOrdinal) ? null : reader.GetString(reviewedByOrdinal));
    }

    private static void ValidateIdentity(string operation, Guid customerId, string idempotencyKey, string requestHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);
        if (operation.Trim().Length > 64) throw new ArgumentOutOfRangeException(nameof(operation));
        if (customerId == Guid.Empty) throw new ArgumentException("Customer identifier cannot be empty.", nameof(customerId));
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        if (idempotencyKey.Trim().Length > 128) throw new ArgumentOutOfRangeException(nameof(idempotencyKey));
        ArgumentException.ThrowIfNullOrWhiteSpace(requestHash);
        if (requestHash.Trim().Length != 64) throw new ArgumentOutOfRangeException(nameof(requestHash));
    }

    private static void EnsureRequestHash(string existingHash, string currentHash)
    {
        if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(existingHash), Encoding.ASCII.GetBytes(currentHash.Trim()))) throw new FraudEventIdempotencyConflictException();
    }

    private static string SerializeReasons(IReadOnlyCollection<string> reasonCodes)
    {
        var safeReasons = reasonCodes.Where(static value => !string.IsNullOrWhiteSpace(value)).Select(static value => value.Trim()).Take(32).ToArray();
        var json = JsonSerializer.Serialize(safeReasons);
        if (json.Length > 1024) throw new ArgumentOutOfRangeException(nameof(reasonCodes), "Fraud reason-code payload cannot exceed 1024 characters.");
        return json;
    }

    private static IReadOnlyCollection<string> ParseReasons(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<string[]>(json) ?? Array.Empty<string>();
        }
        catch (JsonException)
        {
            return Array.Empty<string>();
        }
    }
}
