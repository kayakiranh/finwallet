using System.Data;
using FinWallet.Application.Ledger;
using FinWallet.Domain.Ledger;
using FinWallet.Domain.Shared;
using Microsoft.Data.SqlClient;

namespace FinWallet.Infrastructure.Persistence.SqlServer;

/// <summary>
/// TR: LedgerAccounts tablosunda stabil account code üzerinden concurrency-safe get-or-create yapan explicit MSSQL persistence implementasyonudur.
/// EN: Explicit MSSQL persistence implementation providing concurrency-safe get-or-create by stable account code in LedgerAccounts.
/// </summary>
public sealed class SqlLedgerAccountStore : ILedgerAccountStore
{
    private readonly SqlConnectionFactory _connectionFactory;
    private readonly TimeProvider _timeProvider;

    /// <summary>TR: SQL connection factory ve test edilebilir zaman kaynağıyla store'u oluşturur. EN: Creates the store with SQL connection factory and testable time source.</summary>
    /// <param name="connectionFactory">TR: Pooled SQL connection factory. EN: Pooled SQL connection factory.</param>
    /// <param name="timeProvider">TR: Ledger account create zamanı için UTC zaman kaynağı. EN: UTC time source for ledger-account creation.</param>
    public SqlLedgerAccountStore(SqlConnectionFactory connectionFactory, TimeProvider timeProvider)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <inheritdoc />
    public async Task<LedgerAccount> GetOrCreateAsync(
        string code,
        CurrencyCode currency,
        LedgerAccountType type,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        var normalizedCode = code.Trim().ToUpperInvariant();

        var existing = await FindByCodeAsync(normalizedCode, cancellationToken);
        if (existing is not null)
        {
            EnsureCompatible(existing, currency, type);
            return existing;
        }

        var candidate = new LedgerAccount(Guid.NewGuid(), normalizedCode, currency.ToString(), type);
        var inserted = await TryInsertAsync(candidate, _timeProvider.GetUtcNow(), cancellationToken);
        if (inserted)
        {
            return candidate;
        }

        var winner = await FindByCodeAsync(normalizedCode, cancellationToken)
            ?? throw new InvalidOperationException("Concurrent ledger-account creation winner could not be reloaded.");
        EnsureCompatible(winner, currency, type);
        return winner;
    }

    /// <summary>TR: Ledger account koduyla mevcut kaydı yükler. EN: Loads an existing record by ledger-account code.</summary>
    /// <param name="code">TR: Normalize ledger account kodu. EN: Normalized ledger-account code.</param>
    /// <param name="cancellationToken">TR: SQL sorgu iptal sinyali. EN: SQL-query cancellation signal.</param>
    /// <returns>TR: Eşleşen LedgerAccount; yoksa null. EN: Matching LedgerAccount, or null when absent.</returns>
    private async Task<LedgerAccount?> FindByCodeAsync(string code, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT Id, Code, Currency, Type, Status
            FROM dbo.LedgerAccounts
            WHERE Code = @Code;
            """;

        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@Code", SqlDbType.NVarChar, 128).Value = code;

        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var currency = (CurrencyCode)reader.GetByte(reader.GetOrdinal("Currency"));
        return LedgerAccount.Restore(
            reader.GetGuid(reader.GetOrdinal("Id")),
            reader.GetString(reader.GetOrdinal("Code")),
            currency.ToString(),
            (LedgerAccountType)reader.GetByte(reader.GetOrdinal("Type")),
            (LedgerAccountStatus)reader.GetByte(reader.GetOrdinal("Status")));
    }

    /// <summary>TR: Yeni ledger account insert etmeyi dener ve duplicate code yarışını false sonucu ile bildirir. EN: Attempts to insert a ledger account and reports a duplicate-code race as false.</summary>
    /// <param name="account">TR: Eklenecek LedgerAccount. EN: LedgerAccount to insert.</param>
    /// <param name="createdAt">TR: Durable create UTC zamanı. EN: Durable UTC creation time.</param>
    /// <param name="cancellationToken">TR: SQL insert iptal sinyali. EN: SQL-insert cancellation signal.</param>
    /// <returns>TR: Insert başarılıysa true; unique yarışında false. EN: True when inserted; false on a unique-key race.</returns>
    private async Task<bool> TryInsertAsync(LedgerAccount account, DateTimeOffset createdAt, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO dbo.LedgerAccounts (Id, Code, Currency, Type, Status, CreatedAt)
            VALUES (@Id, @Code, @Currency, @Type, @Status, @CreatedAt);
            """;

        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = account.Id;
        command.Parameters.Add("@Code", SqlDbType.NVarChar, 128).Value = account.Code;
        command.Parameters.Add("@Currency", SqlDbType.TinyInt).Value = (byte)Enum.Parse<CurrencyCode>(account.Currency, ignoreCase: true);
        command.Parameters.Add("@Type", SqlDbType.TinyInt).Value = (byte)account.Type;
        command.Parameters.Add("@Status", SqlDbType.TinyInt).Value = (byte)account.Status;
        command.Parameters.Add("@CreatedAt", SqlDbType.DateTimeOffset).Value = createdAt;

        try
        {
            return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
        }
        catch (SqlException exception) when (exception.Number is 2601 or 2627)
        {
            return false;
        }
    }

    /// <summary>TR: Aynı code ile bulunan hesabın beklenen currency ve muhasebe type değerleriyle uyumlu olduğunu doğrular. EN: Validates that an account found by the same code matches expected currency and accounting type.</summary>
    /// <param name="account">TR: Mevcut durable LedgerAccount. EN: Existing durable LedgerAccount.</param>
    /// <param name="currency">TR: Beklenen currency. EN: Expected currency.</param>
    /// <param name="type">TR: Beklenen accounting type. EN: Expected accounting type.</param>
    private static void EnsureCompatible(LedgerAccount account, CurrencyCode currency, LedgerAccountType type)
    {
        if (!string.Equals(account.Currency, currency.ToString(), StringComparison.Ordinal) || account.Type != type)
        {
            throw new InvalidOperationException("Ledger account code is already bound to a different currency or accounting type.");
        }
    }
}
