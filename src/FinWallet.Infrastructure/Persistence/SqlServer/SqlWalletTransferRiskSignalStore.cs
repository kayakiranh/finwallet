using System.Data;
using System.Security.Cryptography;
using System.Text;
using FinWallet.Application.Transfers;
using FinWallet.Domain.Customers;
using FinWallet.Domain.Shared;
using FinWallet.Domain.Transactions;
using FinWallet.Domain.Wallets;
using Microsoft.Data.SqlClient;

namespace FinWallet.Infrastructure.Persistence.SqlServer;

/// <summary>
/// TR: Wallet transfer fraud değerlendirmesi için aktif session, wallet metadata ve geçmiş transaction verisinden server-side risk sinyalleri üreten MSSQL read implementasyonudur.
/// EN: MSSQL read implementation deriving server-side wallet-transfer risk signals from active session, wallet metadata and historical transaction data.
/// </summary>
public sealed class SqlWalletTransferRiskSignalStore : IWalletTransferRiskSignalStore
{
    private readonly SqlConnectionFactory _connectionFactory;

    /// <summary>TR: SQL connection factory bağımlılığıyla risk-signal store'u oluşturur. EN: Creates the risk-signal store with its SQL connection-factory dependency.</summary>
    /// <param name="connectionFactory">TR: Pooled SQL connection factory. EN: Pooled SQL connection factory.</param>
    public SqlWalletTransferRiskSignalStore(SqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    /// <inheritdoc />
    public async Task<WalletTransferRiskSignals> GetAsync(
        Guid customerId,
        Guid sessionId,
        Guid sourceWalletId,
        Guid destinationWalletId,
        DateTimeOffset evaluatedAt,
        CancellationToken cancellationToken)
    {
        if (customerId == Guid.Empty) throw new ArgumentException("Customer identifier cannot be empty.", nameof(customerId));
        if (sessionId == Guid.Empty) throw new ArgumentException("Session identifier cannot be empty.", nameof(sessionId));
        if (sourceWalletId == Guid.Empty) throw new ArgumentException("Source wallet identifier cannot be empty.", nameof(sourceWalletId));
        if (destinationWalletId == Guid.Empty) throw new ArgumentException("Destination wallet identifier cannot be empty.", nameof(destinationWalletId));
        if (sourceWalletId == destinationWalletId) throw new ArgumentException("Source and destination wallets must differ.");

        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        var identity = await LoadIdentityAndWalletSignalsAsync(
            connection,
            customerId,
            sessionId,
            sourceWalletId,
            destinationWalletId,
            evaluatedAt,
            cancellationToken);

        var history = await LoadHistoricalSignalsAsync(
            connection,
            customerId,
            destinationWalletId,
            identity.Currency,
            evaluatedAt,
            cancellationToken);

        var firstSeenThreshold = evaluatedAt - WalletTransferRiskPolicy.NewDeviceWindow;
        return new WalletTransferRiskSignals(
            identity.Currency,
            identity.CountryCode,
            HashDeviceReference(identity.DeviceId),
            identity.DeviceFirstSeenAt >= firstSeenThreshold,
            history.TransactionCountLastFiveMinutes,
            history.AmountLastTwentyFourHours,
            history.IsKnownBeneficiary);
    }

    /// <summary>
    /// TR: Customer/session ve iki wallet metadata'sını tek sorguda yükler; JWT session'ın aktif/revoke edilmemiş olduğunu ve source ownership'i doğrular.
    /// EN: Loads customer/session and both wallet metadata in one query, validating that the JWT session is active/not revoked and source ownership is valid.
    /// </summary>
    /// <param name="connection">TR: Açık SQL connection. EN: Open SQL connection.</param>
    /// <param name="customerId">TR: Authenticated customer kimliği. EN: Authenticated customer identifier.</param>
    /// <param name="sessionId">TR: JWT sid session kimliği. EN: JWT sid session identifier.</param>
    /// <param name="sourceWalletId">TR: Source wallet kimliği. EN: Source-wallet identifier.</param>
    /// <param name="destinationWalletId">TR: Destination wallet kimliği. EN: Destination-wallet identifier.</param>
    /// <param name="evaluatedAt">TR: Session expiry ve risk evaluation UTC zamanı. EN: UTC time used for session expiry and risk evaluation.</param>
    /// <param name="cancellationToken">TR: SQL sorgu iptal sinyali. EN: SQL-query cancellation signal.</param>
    /// <returns>TR: Kimlik/device/wallet risk metadata sonucunu döndürür. EN: Returns identity/device/wallet risk metadata.</returns>
    private static async Task<IdentityWalletSignals> LoadIdentityAndWalletSignalsAsync(
        SqlConnection connection,
        Guid customerId,
        Guid sessionId,
        Guid sourceWalletId,
        Guid destinationWalletId,
        DateTimeOffset evaluatedAt,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                C.CountryCode,
                S.DeviceId,
                (
                    SELECT MIN(S2.CreatedAt)
                    FROM dbo.CustomerSessions AS S2
                    WHERE S2.CustomerId = S.CustomerId
                      AND S2.DeviceId = S.DeviceId
                ) AS DeviceFirstSeenAt,
                SW.Id AS SourceWalletId,
                SW.Currency AS SourceCurrency,
                SW.Status AS SourceStatus,
                DW.Id AS DestinationWalletId,
                DW.Currency AS DestinationCurrency,
                DW.Status AS DestinationStatus
            FROM dbo.Customers AS C
            INNER JOIN dbo.CustomerSessions AS S
                ON S.Id = @SessionId
               AND S.CustomerId = C.Id
               AND S.RevokedAt IS NULL
               AND S.ExpiresAt > @EvaluatedAt
            LEFT JOIN dbo.Wallets AS SW
                ON SW.Id = @SourceWalletId
               AND SW.CustomerId = C.Id
            LEFT JOIN dbo.Wallets AS DW
                ON DW.Id = @DestinationWalletId
            WHERE C.Id = @CustomerId
              AND C.Status = @ActiveCustomerStatus;
            """;

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@SessionId", SqlDbType.UniqueIdentifier).Value = sessionId;
        command.Parameters.Add("@EvaluatedAt", SqlDbType.DateTimeOffset).Value = evaluatedAt;
        command.Parameters.Add("@SourceWalletId", SqlDbType.UniqueIdentifier).Value = sourceWalletId;
        command.Parameters.Add("@DestinationWalletId", SqlDbType.UniqueIdentifier).Value = destinationWalletId;
        command.Parameters.Add("@CustomerId", SqlDbType.UniqueIdentifier).Value = customerId;
        command.Parameters.Add("@ActiveCustomerStatus", SqlDbType.TinyInt).Value = (byte)CustomerStatus.Active;

        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new WalletTransferSessionInvalidException();
        }

        var sourceIdOrdinal = reader.GetOrdinal("SourceWalletId");
        if (reader.IsDBNull(sourceIdOrdinal))
        {
            throw new WalletTransferSourceNotFoundException();
        }

        var destinationIdOrdinal = reader.GetOrdinal("DestinationWalletId");
        if (reader.IsDBNull(destinationIdOrdinal))
        {
            throw new WalletTransferDestinationNotFoundException();
        }

        var sourceCurrency = (CurrencyCode)reader.GetByte(reader.GetOrdinal("SourceCurrency"));
        var destinationCurrency = (CurrencyCode)reader.GetByte(reader.GetOrdinal("DestinationCurrency"));
        if (sourceCurrency != destinationCurrency)
        {
            throw new CurrencyMismatchException(sourceCurrency, destinationCurrency);
        }

        var sourceStatus = (WalletStatus)reader.GetByte(reader.GetOrdinal("SourceStatus"));
        var destinationStatus = (WalletStatus)reader.GetByte(reader.GetOrdinal("DestinationStatus"));
        if (sourceStatus != WalletStatus.Active || destinationStatus != WalletStatus.Active)
        {
            throw new WalletTransferUnavailableException();
        }

        var firstSeenOrdinal = reader.GetOrdinal("DeviceFirstSeenAt");
        if (reader.IsDBNull(firstSeenOrdinal))
        {
            throw new InvalidOperationException("Active session device history is missing.");
        }

        return new IdentityWalletSignals(
            reader.GetString(reader.GetOrdinal("CountryCode")),
            reader.GetString(reader.GetOrdinal("DeviceId")),
            reader.GetFieldValue<DateTimeOffset>(firstSeenOrdinal),
            sourceCurrency);
    }

    /// <summary>
    /// TR: Başarılı geçmiş WalletTransfer kayıtlarından velocity, 24 saat amount ve known-beneficiary sinyallerini üretir.
    /// EN: Derives velocity, 24-hour amount and known-beneficiary signals from successful historical WalletTransfer records.
    /// </summary>
    /// <param name="connection">TR: Açık SQL connection. EN: Open SQL connection.</param>
    /// <param name="customerId">TR: Source customer kimliği. EN: Source-customer identifier.</param>
    /// <param name="destinationWalletId">TR: Beneficiary olarak değerlendirilecek destination wallet kimliği. EN: Destination-wallet identifier evaluated as beneficiary.</param>
    /// <param name="currency">TR: Aggregate amount için transfer currency değeri. EN: Transfer currency used for aggregate amount.</param>
    /// <param name="evaluatedAt">TR: Risk window bitiş UTC zamanı. EN: UTC end time of risk windows.</param>
    /// <param name="cancellationToken">TR: SQL sorgu iptal sinyali. EN: SQL-query cancellation signal.</param>
    /// <returns>TR: Historical fraud signal setini döndürür. EN: Returns historical fraud-signal set.</returns>
    private static async Task<HistoricalSignals> LoadHistoricalSignalsAsync(
        SqlConnection connection,
        Guid customerId,
        Guid destinationWalletId,
        CurrencyCode currency,
        DateTimeOffset evaluatedAt,
        CancellationToken cancellationToken)
    {
        var velocityFrom = evaluatedAt - WalletTransferRiskPolicy.VelocityWindow;
        var aggregateFrom = evaluatedAt - WalletTransferRiskPolicy.AggregateAmountWindow;

        const string sql = """
            SELECT
                COUNT_BIG(CASE WHEN CreatedAt >= @VelocityFrom THEN 1 END) AS TransactionCountLastFiveMinutes,
                CASE
                    WHEN COALESCE(SUM(CASE WHEN CreatedAt >= @AggregateFrom AND Currency = @Currency THEN Amount ELSE CAST(0 AS DECIMAL(19,4)) END), 0) > @MaximumAmount
                        THEN @MaximumAmount
                    ELSE CONVERT(DECIMAL(19,4), COALESCE(SUM(CASE WHEN CreatedAt >= @AggregateFrom AND Currency = @Currency THEN Amount ELSE CAST(0 AS DECIMAL(19,4)) END), 0))
                END AS AmountLastTwentyFourHours,
                CASE WHEN EXISTS
                (
                    SELECT 1
                    FROM dbo.FinancialTransactions AS Prior
                    WHERE Prior.CustomerId = @CustomerId
                      AND Prior.Type = @WalletTransferType
                      AND Prior.Status = @CompletedStatus
                      AND Prior.DestinationWalletId = @DestinationWalletId
                ) THEN CAST(1 AS BIT) ELSE CAST(0 AS BIT) END AS IsKnownBeneficiary
            FROM dbo.FinancialTransactions
            WHERE CustomerId = @CustomerId
              AND Type = @WalletTransferType
              AND Status = @CompletedStatus
              AND CreatedAt >= @AggregateFrom;
            """;

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@VelocityFrom", SqlDbType.DateTimeOffset).Value = velocityFrom;
        command.Parameters.Add("@AggregateFrom", SqlDbType.DateTimeOffset).Value = aggregateFrom;
        command.Parameters.Add("@Currency", SqlDbType.TinyInt).Value = (byte)currency;
        var maximumParameter = command.Parameters.Add("@MaximumAmount", SqlDbType.Decimal);
        maximumParameter.Precision = 19;
        maximumParameter.Scale = 4;
        maximumParameter.Value = FinancialAmountRules.MaximumAbsoluteAmount;
        command.Parameters.Add("@CustomerId", SqlDbType.UniqueIdentifier).Value = customerId;
        command.Parameters.Add("@WalletTransferType", SqlDbType.TinyInt).Value = (byte)FinancialTransactionType.WalletTransfer;
        command.Parameters.Add("@CompletedStatus", SqlDbType.TinyInt).Value = (byte)FinancialTransactionStatus.Completed;
        command.Parameters.Add("@DestinationWalletId", SqlDbType.UniqueIdentifier).Value = destinationWalletId;

        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return new HistoricalSignals(0, 0m, false);
        }

        var count = reader.GetInt64(reader.GetOrdinal("TransactionCountLastFiveMinutes"));
        return new HistoricalSignals(
            count > int.MaxValue ? int.MaxValue : (int)count,
            reader.GetDecimal(reader.GetOrdinal("AmountLastTwentyFourHours")),
            reader.GetBoolean(reader.GetOrdinal("IsKnownBeneficiary")));
    }

    /// <summary>TR: Raw DeviceId değerini dış fraud provider'a PII/secrets taşımayan stabil SHA-256 reference'a dönüştürür. EN: Converts raw DeviceId into a stable SHA-256 reference that avoids sending PII/secrets to the external fraud provider.</summary>
    /// <param name="deviceId">TR: Server-side session'dan okunan raw device kimliği. EN: Raw device identifier read from the server-side session.</param>
    /// <returns>TR: 64 karakter uppercase hexadecimal device reference döndürür. EN: Returns a 64-character uppercase hexadecimal device reference.</returns>
    private static string HashDeviceReference(string deviceId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(deviceId)));
    }

    /// <summary>TR: Customer/session/wallet metadata read sonucunu taşır. EN: Carries customer/session/wallet metadata read result.</summary>
    /// <param name="CountryCode">TR: Customer ülke kodu. EN: Customer country code.</param>
    /// <param name="DeviceId">TR: Raw server-side device kimliği. EN: Raw server-side device identifier.</param>
    /// <param name="DeviceFirstSeenAt">TR: Aynı customer/device için ilk session UTC zamanı. EN: First session UTC time for the same customer/device.</param>
    /// <param name="Currency">TR: Ortak wallet currency değeri. EN: Shared wallet currency.</param>
    private sealed record IdentityWalletSignals(string CountryCode, string DeviceId, DateTimeOffset DeviceFirstSeenAt, CurrencyCode Currency);

    /// <summary>TR: Historical transaction risk sinyallerini taşır. EN: Carries historical transaction risk signals.</summary>
    /// <param name="TransactionCountLastFiveMinutes">TR: Son 5 dakika successful transfer sayısı. EN: Successful transfer count over the previous five minutes.</param>
    /// <param name="AmountLastTwentyFourHours">TR: Son 24 saat same-currency successful transfer toplamı. EN: Same-currency successful transfer total over the previous twenty-four hours.</param>
    /// <param name="IsKnownBeneficiary">TR: Destination wallet daha önce successful beneficiary olduysa true. EN: True when the destination wallet has previously been a successful beneficiary.</param>
    private sealed record HistoricalSignals(int TransactionCountLastFiveMinutes, decimal AmountLastTwentyFourHours, bool IsKnownBeneficiary);
}
