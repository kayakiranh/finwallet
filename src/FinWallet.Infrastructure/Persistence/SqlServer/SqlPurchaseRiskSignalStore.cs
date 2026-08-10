using System.Data;
using System.Security.Cryptography;
using System.Text;
using FinWallet.Application.Purchases;
using FinWallet.Application.Transfers;
using FinWallet.Domain.Customers;
using FinWallet.Domain.Shared;
using FinWallet.Domain.Transactions;
using FinWallet.Domain.Wallets;
using Microsoft.Data.SqlClient;

namespace FinWallet.Infrastructure.Persistence.SqlServer;

/// <summary>
/// TR: Purchase fraud için active session/device, customer country, customer-owned wallet, merchant familiarity ve completed purchase geçmişinden risk sinyalleri üretir; client fraud flag'lerine güvenmez.
/// EN: Derives purchase-fraud risk signals from active session/device, customer country, customer-owned wallet, merchant familiarity and completed purchase history without trusting client fraud flags.
/// </summary>
public sealed class SqlPurchaseRiskSignalStore : IPurchaseRiskSignalStore
{
    private readonly SqlConnectionFactory _connectionFactory;

    /// <summary>TR: Pooled SQL connection factory ile purchase risk store oluşturur. EN: Creates purchase-risk store with pooled SQL connection factory.</summary>
    /// <param name="connectionFactory">TR: SQL connection factory. EN: SQL connection factory.</param>
    public SqlPurchaseRiskSignalStore(SqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    /// <inheritdoc />
    public async Task<PurchaseRiskSignals> GetAsync(Guid customerId, Guid sessionId, Guid walletId, string merchantId, DateTimeOffset evaluatedAt, CancellationToken cancellationToken)
    {
        if (customerId == Guid.Empty || sessionId == Guid.Empty || walletId == Guid.Empty) throw new PurchaseSessionInvalidException();
        ArgumentException.ThrowIfNullOrWhiteSpace(merchantId);

        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        var identity = await LoadIdentityAsync(connection, customerId, sessionId, walletId, merchantId.Trim(), evaluatedAt, cancellationToken);
        var history = await LoadHistoryAsync(connection, customerId, merchantId.Trim(), identity.Currency, evaluatedAt, cancellationToken);
        return new PurchaseRiskSignals(
            identity.Currency,
            identity.CountryCode,
            HashDeviceReference(identity.DeviceId),
            identity.DeviceFirstSeenAt >= evaluatedAt - WalletTransferRiskPolicy.NewDeviceWindow,
            history.CountLastFiveMinutes,
            history.AmountLastTwentyFourHours,
            history.IsKnownMerchant);
    }

    private static async Task<IdentitySignals> LoadIdentityAsync(SqlConnection connection, Guid customerId, Guid sessionId, Guid walletId, string merchantId, DateTimeOffset evaluatedAt, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                c.CountryCode,
                s.DeviceId,
                (
                    SELECT MIN(s2.CreatedAt)
                    FROM dbo.CustomerSessions s2
                    WHERE s2.CustomerId=s.CustomerId AND s2.DeviceId=s.DeviceId
                ) DeviceFirstSeenAt,
                w.Currency
            FROM dbo.Customers c
            INNER JOIN dbo.CustomerSessions s
                ON s.Id=@SessionId
               AND s.CustomerId=c.Id
               AND s.RevokedAt IS NULL
               AND s.ExpiresAt>@EvaluatedAt
            INNER JOIN dbo.Wallets w
                ON w.Id=@WalletId
               AND w.CustomerId=c.Id
               AND w.Status=@WalletActive
            INNER JOIN dbo.Merchants m
                ON m.Id=@MerchantId
               AND m.Status=1
            WHERE c.Id=@CustomerId AND c.Status=@CustomerActive;
            """;
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@SessionId", SqlDbType.UniqueIdentifier).Value = sessionId;
        command.Parameters.Add("@EvaluatedAt", SqlDbType.DateTimeOffset).Value = evaluatedAt;
        command.Parameters.Add("@WalletId", SqlDbType.UniqueIdentifier).Value = walletId;
        command.Parameters.Add("@MerchantId", SqlDbType.NVarChar, 64).Value = merchantId;
        command.Parameters.Add("@CustomerId", SqlDbType.UniqueIdentifier).Value = customerId;
        command.Parameters.Add("@WalletActive", SqlDbType.TinyInt).Value = (byte)WalletStatus.Active;
        command.Parameters.Add("@CustomerActive", SqlDbType.TinyInt).Value = (byte)CustomerStatus.Active;
        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) throw new PurchaseSessionInvalidException();
        var firstSeenOrdinal = reader.GetOrdinal("DeviceFirstSeenAt");
        if (reader.IsDBNull(firstSeenOrdinal)) throw new PurchaseSessionInvalidException();
        return new IdentitySignals(
            reader.GetString(reader.GetOrdinal("CountryCode")),
            reader.GetString(reader.GetOrdinal("DeviceId")),
            reader.GetFieldValue<DateTimeOffset>(firstSeenOrdinal),
            (CurrencyCode)reader.GetByte(reader.GetOrdinal("Currency")));
    }

    private static async Task<HistorySignals> LoadHistoryAsync(SqlConnection connection, Guid customerId, string merchantId, CurrencyCode currency, DateTimeOffset evaluatedAt, CancellationToken cancellationToken)
    {
        var velocityFrom = evaluatedAt - WalletTransferRiskPolicy.VelocityWindow;
        var aggregateFrom = evaluatedAt - WalletTransferRiskPolicy.AggregateAmountWindow;
        const string sql = """
            SELECT
                COUNT_BIG(CASE WHEN t.CreatedAt>=@VelocityFrom THEN 1 END) CountLastFiveMinutes,
                CASE
                    WHEN COALESCE(SUM(CASE WHEN t.Currency=@Currency THEN t.Amount ELSE CAST(0 AS DECIMAL(19,4)) END),0)>@MaximumAmount THEN @MaximumAmount
                    ELSE CONVERT(DECIMAL(19,4),COALESCE(SUM(CASE WHEN t.Currency=@Currency THEN t.Amount ELSE CAST(0 AS DECIMAL(19,4)) END),0))
                END AmountLastTwentyFourHours,
                CASE WHEN EXISTS
                (
                    SELECT 1
                    FROM dbo.FinancialTransactions prior
                    INNER JOIN dbo.FinancialTransactionDetails pd ON pd.FinancialTransactionId=prior.Id
                    WHERE prior.CustomerId=@CustomerId
                      AND prior.Type=@PurchaseType
                      AND prior.Status=@Completed
                      AND pd.MerchantId=@MerchantId
                ) THEN CAST(1 AS BIT) ELSE CAST(0 AS BIT) END IsKnownMerchant
            FROM dbo.FinancialTransactions t
            WHERE t.CustomerId=@CustomerId
              AND t.Type=@PurchaseType
              AND t.Status=@Completed
              AND t.CreatedAt>=@AggregateFrom;
            """;
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@VelocityFrom", SqlDbType.DateTimeOffset).Value = velocityFrom;
        command.Parameters.Add("@AggregateFrom", SqlDbType.DateTimeOffset).Value = aggregateFrom;
        command.Parameters.Add("@Currency", SqlDbType.TinyInt).Value = (byte)currency;
        var maximum = command.Parameters.Add("@MaximumAmount", SqlDbType.Decimal);
        maximum.Precision = 19; maximum.Scale = 4; maximum.Value = FinancialAmountRules.MaximumAbsoluteAmount;
        command.Parameters.Add("@CustomerId", SqlDbType.UniqueIdentifier).Value = customerId;
        command.Parameters.Add("@PurchaseType", SqlDbType.TinyInt).Value = (byte)FinancialTransactionType.Purchase;
        command.Parameters.Add("@Completed", SqlDbType.TinyInt).Value = (byte)FinancialTransactionStatus.Completed;
        command.Parameters.Add("@MerchantId", SqlDbType.NVarChar, 64).Value = merchantId;
        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return new HistorySignals(0, 0m, false);
        var count = reader.GetInt64(reader.GetOrdinal("CountLastFiveMinutes"));
        return new HistorySignals(count > int.MaxValue ? int.MaxValue : (int)count, reader.GetDecimal(reader.GetOrdinal("AmountLastTwentyFourHours")), reader.GetBoolean(reader.GetOrdinal("IsKnownMerchant")));
    }

    private static string HashDeviceReference(string deviceId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(deviceId)));
    }

    private sealed record IdentitySignals(string CountryCode, string DeviceId, DateTimeOffset DeviceFirstSeenAt, CurrencyCode Currency);
    private sealed record HistorySignals(int CountLastFiveMinutes, decimal AmountLastTwentyFourHours, bool IsKnownMerchant);
}
