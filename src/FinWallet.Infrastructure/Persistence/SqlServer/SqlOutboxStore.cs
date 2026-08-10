using System.Data;
using FinWallet.Application.Outbox;
using Microsoft.Data.SqlClient;

namespace FinWallet.Infrastructure.Persistence.SqlServer;

/// <summary>
/// TR: Transactional Outbox kayıtlarını SQL `UPDLOCK/READPAST` claim yaklaşımıyla çoklu FinWallet instance'ında yarışa dayanıklı şekilde dağıtır; dış provider çağrısı hiçbir SQL transaction içinde yapılmaz.
/// EN: Distributes Transactional Outbox records safely across multiple FinWallet instances using SQL `UPDLOCK/READPAST` claiming; external-provider calls are never made inside a SQL transaction.
/// </summary>
public sealed class SqlOutboxStore : IOutboxStore
{
    private readonly SqlConnectionFactory _connectionFactory;

    /// <summary>TR: Pooled SQL connection factory ile Outbox store oluşturur. EN: Creates the Outbox store with a pooled SQL connection factory.</summary>
    /// <param name="connectionFactory">TR: SQL connection factory. EN: SQL connection factory.</param>
    public SqlOutboxStore(SqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<OutboxMessage>> ClaimPendingAsync(DateTimeOffset now, DateTimeOffset leaseUntil, int take, CancellationToken cancellationToken)
    {
        if (leaseUntil <= now) throw new ArgumentException("Outbox lease must expire after claim time.", nameof(leaseUntil));
        if (take < 1 || take > 100) throw new ArgumentOutOfRangeException(nameof(take));

        const string sql = """
            ;WITH Candidate AS
            (
                SELECT TOP (@Take)
                    Id, MessageType, AggregateId, PayloadJson, CorrelationId,
                    CreatedAt, AvailableAt, ProcessedAt, AttemptCount, LastErrorCode
                FROM dbo.OutboxMessages WITH (UPDLOCK, READPAST, ROWLOCK)
                WHERE ProcessedAt IS NULL
                  AND AvailableAt <= @Now
                ORDER BY CreatedAt, Id
            )
            UPDATE Candidate
            SET AvailableAt = @LeaseUntil,
                AttemptCount = AttemptCount + 1,
                LastErrorCode = NULL
            OUTPUT inserted.Id,
                   inserted.MessageType,
                   inserted.AggregateId,
                   inserted.PayloadJson,
                   inserted.CorrelationId,
                   inserted.AttemptCount;
            """;

        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@Take", SqlDbType.Int).Value = take;
        command.Parameters.Add("@Now", SqlDbType.DateTimeOffset).Value = now;
        command.Parameters.Add("@LeaseUntil", SqlDbType.DateTimeOffset).Value = leaseUntil;

        var messages = new List<OutboxMessage>(take);
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                var aggregateOrdinal = reader.GetOrdinal("AggregateId");
                var correlationOrdinal = reader.GetOrdinal("CorrelationId");
                messages.Add(new OutboxMessage(
                    reader.GetGuid(reader.GetOrdinal("Id")),
                    reader.GetString(reader.GetOrdinal("MessageType")),
                    reader.IsDBNull(aggregateOrdinal) ? null : reader.GetGuid(aggregateOrdinal),
                    reader.GetString(reader.GetOrdinal("PayloadJson")),
                    reader.IsDBNull(correlationOrdinal) ? null : reader.GetString(correlationOrdinal),
                    reader.GetInt32(reader.GetOrdinal("AttemptCount"))));
            }
        }

        await transaction.CommitAsync(cancellationToken);
        return messages;
    }

    /// <inheritdoc />
    public async Task MarkProcessedAsync(Guid messageId, DateTimeOffset processedAt, CancellationToken cancellationToken)
    {
        if (messageId == Guid.Empty) throw new ArgumentException("Outbox message identifier cannot be empty.", nameof(messageId));
        const string sql = "UPDATE dbo.OutboxMessages SET ProcessedAt=@ProcessedAt,LastErrorCode=NULL WHERE Id=@Id AND ProcessedAt IS NULL;";
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@ProcessedAt", SqlDbType.DateTimeOffset).Value = processedAt;
        command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = messageId;
        var affected = await command.ExecuteNonQueryAsync(cancellationToken);
        if (affected is < 0 or > 1) throw new InvalidOperationException("Outbox completion affected an unexpected number of rows.");
    }

    /// <inheritdoc />
    public async Task RescheduleAsync(Guid messageId, DateTimeOffset availableAt, string errorCode, CancellationToken cancellationToken)
    {
        if (messageId == Guid.Empty) throw new ArgumentException("Outbox message identifier cannot be empty.", nameof(messageId));
        ArgumentException.ThrowIfNullOrWhiteSpace(errorCode);
        var safeCode = errorCode.Trim();
        if (safeCode.Length > 128) safeCode = safeCode[..128];
        const string sql = "UPDATE dbo.OutboxMessages SET AvailableAt=@AvailableAt,LastErrorCode=@ErrorCode WHERE Id=@Id AND ProcessedAt IS NULL;";
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@AvailableAt", SqlDbType.DateTimeOffset).Value = availableAt;
        command.Parameters.Add("@ErrorCode", SqlDbType.NVarChar, 128).Value = safeCode;
        command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = messageId;
        var affected = await command.ExecuteNonQueryAsync(cancellationToken);
        if (affected is < 0 or > 1) throw new InvalidOperationException("Outbox reschedule affected an unexpected number of rows.");
    }

    /// <inheritdoc />
    public async Task<string?> FindCustomerPhoneAsync(Guid customerId, CancellationToken cancellationToken)
    {
        if (customerId == Guid.Empty) return null;
        const string sql = "SELECT PhoneNumber FROM dbo.Customers WHERE Id=@CustomerId AND Status=2;";
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@CustomerId", SqlDbType.UniqueIdentifier).Value = customerId;
        return await command.ExecuteScalarAsync(cancellationToken) as string;
    }
}
