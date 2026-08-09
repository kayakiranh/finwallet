using System.Data;
using FinWallet.Application.Transfers;
using FinWallet.Domain.Shared;
using FinWallet.Domain.Transactions;
using Microsoft.Data.SqlClient;

namespace FinWallet.Infrastructure.Persistence.SqlServer;

/// <summary>
/// TR: Completed wallet-transfer idempotency sonucunu read-only MSSQL sorgularıyla çözümler; replay öncesi fraud provider'ın tekrar çağrılmasını engeller.
/// EN: Resolves completed wallet-transfer idempotency results with read-only MSSQL queries and prevents re-calling the fraud provider before a replay.
/// </summary>
public sealed class SqlWalletTransferReplayStore : IWalletTransferReplayStore
{
    private const string IdempotencyScope = "WALLET_TRANSFER";
    private const byte IdempotencyProcessing = 1;
    private const byte IdempotencyCompleted = 2;
    private readonly SqlConnectionFactory _connectionFactory;

    /// <summary>TR: SQL connection factory bağımlılığıyla replay store'u oluşturur. EN: Creates the replay store with its SQL connection-factory dependency.</summary>
    /// <param name="connectionFactory">TR: Pooled SQL connection factory. EN: Pooled SQL connection factory.</param>
    public SqlWalletTransferReplayStore(SqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    /// <inheritdoc />
    public async Task<WalletTransferPostingResult?> TryGetCompletedAsync(
        WalletTransferPostingRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        var idempotency = await FindIdempotencyAsync(connection, request, cancellationToken);
        if (idempotency is null)
        {
            return null;
        }

        if (idempotency.Status == IdempotencyProcessing)
        {
            throw new WalletTransferInProgressException();
        }

        if (idempotency.Status != IdempotencyCompleted || idempotency.ResourceId is null)
        {
            throw new InvalidOperationException("Wallet-transfer idempotency state is not replayable.");
        }

        var result = await LoadCompletedTransferAsync(
            connection,
            idempotency.ResourceId.Value,
            request.CustomerId,
            cancellationToken);

        if (result.SourceWalletId != request.SourceWalletId ||
            result.DestinationWalletId != request.DestinationWalletId ||
            result.Amount.Amount != request.Amount)
        {
            throw new WalletTransferIdempotencyConflictException();
        }

        return result;
    }

    /// <summary>TR: Customer/scope/key ile durable idempotency state'i yükler. EN: Loads durable idempotency state by customer/scope/key.</summary>
    /// <param name="connection">TR: Açık SQL connection. EN: Open SQL connection.</param>
    /// <param name="request">TR: Idempotency lookup bilgilerini taşıyan transfer request. EN: Transfer request carrying idempotency lookup information.</param>
    /// <param name="cancellationToken">TR: SQL sorgu iptal sinyali. EN: SQL-query cancellation signal.</param>
    /// <returns>TR: Idempotency state; yoksa null. EN: Idempotency state, or null when absent.</returns>
    private static async Task<ReplayIdempotencyState?> FindIdempotencyAsync(
        SqlConnection connection,
        WalletTransferPostingRequest request,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT ResourceId, Status
            FROM dbo.IdempotencyRecords
            WHERE Scope = @Scope
              AND CustomerId = @CustomerId
              AND IdempotencyKey = @IdempotencyKey;
            """;

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@Scope", SqlDbType.NVarChar, 64).Value = IdempotencyScope;
        command.Parameters.Add("@CustomerId", SqlDbType.UniqueIdentifier).Value = request.CustomerId;
        command.Parameters.Add("@IdempotencyKey", SqlDbType.NVarChar, 128).Value = request.IdempotencyKey;
        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var resourceOrdinal = reader.GetOrdinal("ResourceId");
        return new ReplayIdempotencyState(
            reader.IsDBNull(resourceOrdinal) ? null : reader.GetGuid(resourceOrdinal),
            reader.GetByte(reader.GetOrdinal("Status")));
    }

    /// <summary>TR: Completed idempotency resource transaction'ını immutable replay sonucuna dönüştürür. EN: Converts the completed idempotency resource transaction into an immutable replay result.</summary>
    /// <param name="connection">TR: Açık SQL connection. EN: Open SQL connection.</param>
    /// <param name="transactionId">TR: FinancialTransaction kimliği. EN: FinancialTransaction identifier.</param>
    /// <param name="customerId">TR: Idempotency owner customer kimliği. EN: Idempotency owner-customer identifier.</param>
    /// <param name="cancellationToken">TR: SQL sorgu iptal sinyali. EN: SQL-query cancellation signal.</param>
    /// <returns>TR: WasReplay=true completed transfer sonucu döndürür. EN: Returns a completed transfer result with WasReplay=true.</returns>
    private static async Task<WalletTransferPostingResult> LoadCompletedTransferAsync(
        SqlConnection connection,
        Guid transactionId,
        Guid customerId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT Type, Status, SourceWalletId, DestinationWalletId, Currency, Amount, FinalizedAt
            FROM dbo.FinancialTransactions
            WHERE Id = @Id
              AND CustomerId = @CustomerId;
            """;

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = transactionId;
        command.Parameters.Add("@CustomerId", SqlDbType.UniqueIdentifier).Value = customerId;
        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException("Completed wallet-transfer idempotency resource was not found.");
        }

        var sourceOrdinal = reader.GetOrdinal("SourceWalletId");
        var destinationOrdinal = reader.GetOrdinal("DestinationWalletId");
        var finalizedOrdinal = reader.GetOrdinal("FinalizedAt");
        if ((FinancialTransactionType)reader.GetByte(reader.GetOrdinal("Type")) != FinancialTransactionType.WalletTransfer ||
            (FinancialTransactionStatus)reader.GetByte(reader.GetOrdinal("Status")) != FinancialTransactionStatus.Completed ||
            reader.IsDBNull(sourceOrdinal) || reader.IsDBNull(destinationOrdinal) || reader.IsDBNull(finalizedOrdinal))
        {
            throw new InvalidOperationException("Completed idempotency resource is not a valid completed wallet transfer.");
        }

        return new WalletTransferPostingResult(
            transactionId,
            reader.GetGuid(sourceOrdinal),
            reader.GetGuid(destinationOrdinal),
            new Money(
                reader.GetDecimal(reader.GetOrdinal("Amount")),
                (CurrencyCode)reader.GetByte(reader.GetOrdinal("Currency"))),
            reader.GetFieldValue<DateTimeOffset>(finalizedOrdinal),
            wasReplay: true);
    }

    /// <summary>TR: Replay için gereken minimal idempotency state'ini taşır. EN: Carries minimal idempotency state required for replay.</summary>
    /// <param name="ResourceId">TR: İsteğe bağlı FinancialTransaction kimliği. EN: Optional FinancialTransaction identifier.</param>
    /// <param name="Status">TR: Numeric idempotency lifecycle durumu. EN: Numeric idempotency lifecycle state.</param>
    private sealed record ReplayIdempotencyState(Guid? ResourceId, byte Status);
}
