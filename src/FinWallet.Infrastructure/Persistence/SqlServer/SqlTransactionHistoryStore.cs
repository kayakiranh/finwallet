using System.Data;
using FinWallet.Application.Transactions;
using FinWallet.Domain.Shared;
using FinWallet.Domain.Transactions;
using Microsoft.Data.SqlClient;

namespace FinWallet.Infrastructure.Persistence.SqlServer;

/// <summary>
/// TR: Customer financial history read-model'ini MSSQL FinancialTransactions + optional FinancialTransactionDetails üzerinden newest-first keyset pagination ile okur; ledger source-of-truth'u değiştirmez.
/// EN: Reads customer financial-history read model from MSSQL FinancialTransactions plus optional FinancialTransactionDetails using newest-first keyset pagination; it never changes the ledger source of truth.
/// </summary>
public sealed class SqlTransactionHistoryStore : ITransactionHistoryStore
{
    private readonly SqlConnectionFactory _connectionFactory;

    /// <summary>TR: Pooled SQL connection factory ile transaction history store oluşturur. EN: Creates transaction-history store with pooled SQL connection factory.</summary>
    /// <param name="connectionFactory">TR: SQL connection factory. EN: SQL connection factory.</param>
    public SqlTransactionHistoryStore(SqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<TransactionHistoryItem>> ListAsync(Guid customerId, Guid? beforeTransactionId, int take, CancellationToken cancellationToken)
    {
        const string sql = """
            DECLARE @CursorCreatedAt DATETIMEOFFSET(7) = NULL;
            IF @BeforeTransactionId IS NOT NULL
            BEGIN
                SELECT @CursorCreatedAt = CreatedAt
                FROM dbo.FinancialTransactions
                WHERE Id = @BeforeTransactionId AND CustomerId = @CustomerId;
            END;

            SELECT TOP (@Take)
                t.Id,
                t.Type,
                t.Status,
                t.SourceWalletId,
                t.DestinationWalletId,
                t.Currency,
                t.Amount,
                t.CreatedAt,
                t.FinalizedAt,
                t.ReversedAt,
                t.FailureCode,
                d.ParentTransactionId,
                d.BankAccountId,
                d.ExternalTransactionId,
                d.MerchantId,
                d.OriginalAmount,
                d.DiscountAmount,
                d.ProcessingDate,
                d.SettlementDate
            FROM dbo.FinancialTransactions t
            LEFT JOIN dbo.FinancialTransactionDetails d ON d.FinancialTransactionId = t.Id
            WHERE t.CustomerId = @CustomerId
              AND
              (
                  @BeforeTransactionId IS NULL
                  OR
                  (
                      @CursorCreatedAt IS NOT NULL
                      AND
                      (
                          t.CreatedAt < @CursorCreatedAt
                          OR (t.CreatedAt = @CursorCreatedAt AND t.Id < @BeforeTransactionId)
                      )
                  )
              )
            ORDER BY t.CreatedAt DESC, t.Id DESC;
            """;

        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@CustomerId", SqlDbType.UniqueIdentifier).Value = customerId;
        command.Parameters.Add("@BeforeTransactionId", SqlDbType.UniqueIdentifier).Value = (object?)beforeTransactionId ?? DBNull.Value;
        command.Parameters.Add("@Take", SqlDbType.Int).Value = take;

        var items = new List<TransactionHistoryItem>(take);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new TransactionHistoryItem(
                reader.GetGuid(reader.GetOrdinal("Id")),
                (FinancialTransactionType)reader.GetByte(reader.GetOrdinal("Type")),
                reader.GetByte(reader.GetOrdinal("Status")),
                GetNullableGuid(reader, "SourceWalletId"),
                GetNullableGuid(reader, "DestinationWalletId"),
                new Money(reader.GetDecimal(reader.GetOrdinal("Amount")), (CurrencyCode)reader.GetByte(reader.GetOrdinal("Currency"))),
                reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("CreatedAt")),
                GetNullableDateTimeOffset(reader, "FinalizedAt"),
                GetNullableDateTimeOffset(reader, "ReversedAt"),
                GetNullableString(reader, "FailureCode"),
                GetNullableGuid(reader, "ParentTransactionId"),
                GetNullableGuid(reader, "BankAccountId"),
                GetNullableGuid(reader, "ExternalTransactionId"),
                GetNullableString(reader, "MerchantId"),
                GetNullableDecimal(reader, "OriginalAmount"),
                GetNullableDecimal(reader, "DiscountAmount"),
                GetNullableDateOnly(reader, "ProcessingDate"),
                GetNullableDateOnly(reader, "SettlementDate")));
        }

        return items;
    }

    private static Guid? GetNullableGuid(SqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetGuid(ordinal);
    }

    private static string? GetNullableString(SqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private static decimal? GetNullableDecimal(SqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetDecimal(ordinal);
    }

    private static DateTimeOffset? GetNullableDateTimeOffset(SqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetFieldValue<DateTimeOffset>(ordinal);
    }

    private static DateOnly? GetNullableDateOnly(SqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : DateOnly.FromDateTime(reader.GetDateTime(ordinal));
    }
}
