using System.Data;
using System.Security.Cryptography;
using System.Text;
using FinWallet.Application.Inbox;
using Microsoft.Data.SqlClient;

namespace FinWallet.Infrastructure.Persistence.SqlServer;

/// <summary>
/// TR: FakeBank callback'larını Source+MessageId unique anahtarı ve canonical payload hash'iyle MSSQL Inbox tablosunda dedupe eder; processed olmayan duplicate crash-recovery için yeniden işlenebilir.
/// EN: Deduplicates FakeBank callbacks in MSSQL Inbox using Source+MessageId uniqueness plus canonical payload hash; unprocessed duplicates remain replayable for crash recovery.
/// </summary>
public sealed class SqlBankCallbackInboxStore : IBankCallbackInboxStore
{
    private readonly SqlConnectionFactory _connectionFactory;

    /// <summary>TR: Pooled SQL connection factory ile Inbox store oluşturur. EN: Creates Inbox store with pooled SQL connection factory.</summary>
    /// <param name="connectionFactory">TR: SQL connection factory. EN: SQL connection factory.</param>
    public SqlBankCallbackInboxStore(SqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    /// <inheritdoc />
    public async Task<InboxBeginResult> BeginAsync(string source, string messageId, string payloadHash, DateTimeOffset receivedAt, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
        ArgumentException.ThrowIfNullOrWhiteSpace(payloadHash);
        if (source.Trim().Length > 64 || messageId.Trim().Length > 128 || payloadHash.Trim().Length != 64) throw new ArgumentOutOfRangeException(nameof(messageId));

        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        const string selectSql = "SELECT Id,PayloadHash,ProcessedAt FROM dbo.InboxMessages WITH (UPDLOCK,HOLDLOCK) WHERE Source=@Source AND MessageId=@MessageId;";
        await using (var command = new SqlCommand(selectSql, connection, transaction))
        {
            command.Parameters.Add("@Source", SqlDbType.NVarChar, 64).Value = source.Trim();
            command.Parameters.Add("@MessageId", SqlDbType.NVarChar, 128).Value = messageId.Trim();
            await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                var existingHash = reader.GetString(reader.GetOrdinal("PayloadHash"));
                if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(existingHash), Encoding.ASCII.GetBytes(payloadHash.Trim())))
                {
                    throw new InboxMessageConflictException();
                }

                var processed = !reader.IsDBNull(reader.GetOrdinal("ProcessedAt"));
                var result = new InboxBeginResult(reader.GetGuid(reader.GetOrdinal("Id")), processed);
                await reader.DisposeAsync();
                await transaction.CommitAsync(cancellationToken);
                return result;
            }
        }

        var id = Guid.NewGuid();
        const string insertSql = "INSERT INTO dbo.InboxMessages (Id,Source,MessageId,PayloadHash,ReceivedAt,ProcessedAt) VALUES (@Id,@Source,@MessageId,@PayloadHash,@ReceivedAt,NULL);";
        await using (var command = new SqlCommand(insertSql, connection, transaction))
        {
            command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = id;
            command.Parameters.Add("@Source", SqlDbType.NVarChar, 64).Value = source.Trim();
            command.Parameters.Add("@MessageId", SqlDbType.NVarChar, 128).Value = messageId.Trim();
            command.Parameters.Add("@PayloadHash", SqlDbType.Char, 64).Value = payloadHash.Trim();
            command.Parameters.Add("@ReceivedAt", SqlDbType.DateTimeOffset).Value = receivedAt;
            if (await command.ExecuteNonQueryAsync(cancellationToken) != 1) throw new InvalidOperationException("Inbox insert did not affect exactly one row.");
        }

        await transaction.CommitAsync(cancellationToken);
        return new InboxBeginResult(id, alreadyProcessed: false);
    }

    /// <inheritdoc />
    public async Task<Guid?> FindInternalTransactionIdAsync(Guid externalTransactionId, CancellationToken cancellationToken)
    {
        if (externalTransactionId == Guid.Empty) return null;
        const string sql = "SELECT FinancialTransactionId FROM dbo.FinancialTransactionDetails WHERE ExternalTransactionId=@ExternalTransactionId;";
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@ExternalTransactionId", SqlDbType.UniqueIdentifier).Value = externalTransactionId;
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is Guid transactionId ? transactionId : null;
    }

    /// <inheritdoc />
    public async Task MarkProcessedAsync(Guid inboxId, DateTimeOffset processedAt, CancellationToken cancellationToken)
    {
        if (inboxId == Guid.Empty) throw new ArgumentException("Inbox identifier cannot be empty.", nameof(inboxId));
        const string sql = "UPDATE dbo.InboxMessages SET ProcessedAt=COALESCE(ProcessedAt,@ProcessedAt) WHERE Id=@Id;";
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@ProcessedAt", SqlDbType.DateTimeOffset).Value = processedAt;
        command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = inboxId;
        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1) throw new InvalidOperationException("Inbox completion did not affect exactly one row.");
    }
}
