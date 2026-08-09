using System.Data;
using System.Text.Json;
using FinWallet.Application.Fraud;
using Microsoft.Data.SqlClient;

namespace FinWallet.Infrastructure.Persistence.SqlServer;

/// <summary>
/// TR: PII-free fraud evaluation audit snapshot'larını append-only FraudEvents tablosuna explicit parametreli MSSQL komutuyla yazar.
/// EN: Writes PII-free fraud-evaluation audit snapshots to the append-only FraudEvents table using an explicit parameterized MSSQL command.
/// </summary>
public sealed class SqlFraudEventStore : IFraudEventStore
{
    private const int MaximumReasonCodesJsonLength = 2000;
    private readonly SqlConnectionFactory _connectionFactory;

    /// <summary>TR: SQL connection factory bağımlılığıyla fraud event store'u oluşturur. EN: Creates the fraud-event store with its SQL connection-factory dependency.</summary>
    /// <param name="connectionFactory">TR: Pooled SQL connection factory. EN: Pooled SQL connection factory.</param>
    public SqlFraudEventStore(SqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    /// <inheritdoc />
    public async Task InsertAsync(FraudEvaluationAuditRecord record, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);
        var reasonCodesJson = SerializeReasonCodes(record.ExternalReasonCodes);

        const string sql = """
            INSERT INTO dbo.FraudEvents
            (
                Id, CustomerId, SessionId, TransactionType, SourceWalletId, DestinationWalletId,
                Currency, Amount, CountryCode, DeviceReference, IsNewDevice,
                TransactionCountLastFiveMinutes, AmountLastTwentyFourHours, IsKnownBeneficiary,
                InternalDecision, ExternalEvaluationStatus, ExternalDecision, FinalDecision,
                ExternalProviderReference, ExternalRiskScore, ExternalReasonCodes, ExternalFailureCode, CreatedAt
            )
            VALUES
            (
                @Id, @CustomerId, @SessionId, @TransactionType, @SourceWalletId, @DestinationWalletId,
                @Currency, @Amount, @CountryCode, @DeviceReference, @IsNewDevice,
                @TransactionCountLastFiveMinutes, @AmountLastTwentyFourHours, @IsKnownBeneficiary,
                @InternalDecision, @ExternalEvaluationStatus, @ExternalDecision, @FinalDecision,
                @ExternalProviderReference, @ExternalRiskScore, @ExternalReasonCodes, @ExternalFailureCode, @CreatedAt
            );
            """;

        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = record.Id;
        command.Parameters.Add("@CustomerId", SqlDbType.UniqueIdentifier).Value = record.CustomerId;
        command.Parameters.Add("@SessionId", SqlDbType.UniqueIdentifier).Value = record.SessionId;
        command.Parameters.Add("@TransactionType", SqlDbType.TinyInt).Value = (byte)record.TransactionType;
        command.Parameters.Add("@SourceWalletId", SqlDbType.UniqueIdentifier).Value = (object?)record.SourceWalletId ?? DBNull.Value;
        command.Parameters.Add("@DestinationWalletId", SqlDbType.UniqueIdentifier).Value = (object?)record.DestinationWalletId ?? DBNull.Value;
        command.Parameters.Add("@Currency", SqlDbType.TinyInt).Value = (byte)record.Amount.Currency;
        AddMoneyParameter(command, "@Amount", record.Amount.Amount);
        command.Parameters.Add("@CountryCode", SqlDbType.VarChar, 3).Value = record.CountryCode;
        command.Parameters.Add("@DeviceReference", SqlDbType.Char, 64).Value = record.DeviceReference;
        command.Parameters.Add("@IsNewDevice", SqlDbType.Bit).Value = record.IsNewDevice;
        command.Parameters.Add("@TransactionCountLastFiveMinutes", SqlDbType.Int).Value = record.TransactionCountLastFiveMinutes;
        AddMoneyParameter(command, "@AmountLastTwentyFourHours", record.AmountLastTwentyFourHours);
        command.Parameters.Add("@IsKnownBeneficiary", SqlDbType.Bit).Value = record.IsKnownBeneficiary;
        command.Parameters.Add("@InternalDecision", SqlDbType.TinyInt).Value = (byte)record.InternalDecision;
        command.Parameters.Add("@ExternalEvaluationStatus", SqlDbType.TinyInt).Value = (byte)record.ExternalEvaluationStatus;
        command.Parameters.Add("@ExternalDecision", SqlDbType.TinyInt).Value = record.ExternalDecision.HasValue ? (object)(byte)record.ExternalDecision.Value : DBNull.Value;
        command.Parameters.Add("@FinalDecision", SqlDbType.TinyInt).Value = record.FinalDecision.HasValue ? (object)(byte)record.FinalDecision.Value : DBNull.Value;
        command.Parameters.Add("@ExternalProviderReference", SqlDbType.UniqueIdentifier).Value = (object?)record.ExternalProviderReference ?? DBNull.Value;
        command.Parameters.Add("@ExternalRiskScore", SqlDbType.SmallInt).Value = (object?)record.ExternalRiskScore ?? DBNull.Value;
        command.Parameters.Add("@ExternalReasonCodes", SqlDbType.NVarChar, MaximumReasonCodesJsonLength).Value = (object?)reasonCodesJson ?? DBNull.Value;
        command.Parameters.Add("@ExternalFailureCode", SqlDbType.NVarChar, 64).Value = (object?)record.ExternalFailureCode ?? DBNull.Value;
        command.Parameters.Add("@CreatedAt", SqlDbType.DateTimeOffset).Value = record.CreatedAt;

        var affectedRows = await command.ExecuteNonQueryAsync(cancellationToken);
        if (affectedRows != 1)
        {
            throw new InvalidOperationException("FraudEvent insert did not affect exactly one row.");
        }
    }

    /// <summary>TR: External reason-code koleksiyonunu JSON array olarak serialize eder ve DB kolon sınırını doğrular. EN: Serializes external reason-code collection as a JSON array and validates the DB-column limit.</summary>
    /// <param name="reasonCodes">TR: Provider reason-code koleksiyonu; uygulanmıyorsa null. EN: Provider reason-code collection, or null when not applicable.</param>
    /// <returns>TR: JSON array metni veya null döndürür. EN: Returns JSON-array text or null.</returns>
    private static string? SerializeReasonCodes(IReadOnlyCollection<string>? reasonCodes)
    {
        if (reasonCodes is null)
        {
            return null;
        }

        var json = JsonSerializer.Serialize(reasonCodes);
        if (json.Length > MaximumReasonCodesJsonLength)
        {
            throw new InvalidOperationException("External fraud reason codes exceed the durable audit storage limit.");
        }

        return json;
    }

    /// <summary>TR: DECIMAL(19,4) finansal SQL parametresi ekler. EN: Adds a DECIMAL(19,4) financial SQL parameter.</summary>
    /// <param name="command">TR: Parametre eklenecek SQL komutu. EN: SQL command receiving the parameter.</param>
    /// <param name="name">TR: SQL parametre adı. EN: SQL parameter name.</param>
    /// <param name="value">TR: Finansal decimal değer. EN: Financial decimal value.</param>
    private static void AddMoneyParameter(SqlCommand command, string name, decimal value)
    {
        var parameter = command.Parameters.Add(name, SqlDbType.Decimal);
        parameter.Precision = 19;
        parameter.Scale = 4;
        parameter.Value = value;
    }
}
