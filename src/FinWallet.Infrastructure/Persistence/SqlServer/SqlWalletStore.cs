using System.Data;
using FinWallet.Application.Wallets;
using FinWallet.Domain.Shared;
using FinWallet.Domain.Wallets;
using Microsoft.Data.SqlClient;

namespace FinWallet.Infrastructure.Persistence.SqlServer;

/// <summary>
/// TR: Wallet durable state'ini explicit parametreli MSSQL komutlarıyla saklayan persistence implementasyonudur.
/// EN: Persistence implementation storing durable Wallet state with explicit parameterized MSSQL commands.
/// </summary>
public sealed class SqlWalletStore : IWalletStore
{
    private readonly SqlConnectionFactory _connectionFactory;

    /// <summary>
    /// TR: SQL connection factory bağımlılığıyla wallet store'u oluşturur.
    /// EN: Creates the wallet store with its SQL connection-factory dependency.
    /// </summary>
    /// <param name="connectionFactory">TR: Pooled SQL connection factory. EN: Pooled SQL connection factory.</param>
    public SqlWalletStore(SqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    /// <inheritdoc />
    public async Task<Wallet?> FindOwnedAsync(Guid walletId, Guid customerId, CancellationToken cancellationToken)
    {
        if (walletId == Guid.Empty) throw new ArgumentException("Wallet identifier cannot be empty.", nameof(walletId));
        ValidateCustomerId(customerId);

        const string sql = """
            SELECT Id, CustomerId, Currency, AvailableBalance, BlockedBalance, Status, CreatedAt
            FROM dbo.Wallets
            WHERE Id = @Id AND CustomerId = @CustomerId;
            """;

        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = walletId;
        command.Parameters.Add("@CustomerId", SqlDbType.UniqueIdentifier).Value = customerId;

        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadWallet(reader) : null;
    }

    /// <inheritdoc />
    public async Task<Wallet?> FindByCurrencyAsync(Guid customerId, CurrencyCode currency, CancellationToken cancellationToken)
    {
        ValidateCustomerId(customerId);

        const string sql = """
            SELECT Id, CustomerId, Currency, AvailableBalance, BlockedBalance, Status, CreatedAt
            FROM dbo.Wallets
            WHERE CustomerId = @CustomerId AND Currency = @Currency;
            """;

        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@CustomerId", SqlDbType.UniqueIdentifier).Value = customerId;
        command.Parameters.Add("@Currency", SqlDbType.TinyInt).Value = (byte)currency;

        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadWallet(reader) : null;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<Wallet>> ListOwnedAsync(Guid customerId, CancellationToken cancellationToken)
    {
        ValidateCustomerId(customerId);

        const string sql = """
            SELECT Id, CustomerId, Currency, AvailableBalance, BlockedBalance, Status, CreatedAt
            FROM dbo.Wallets
            WHERE CustomerId = @CustomerId
            ORDER BY Currency, CreatedAt, Id;
            """;

        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@CustomerId", SqlDbType.UniqueIdentifier).Value = customerId;

        var wallets = new List<Wallet>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            wallets.Add(ReadWallet(reader));
        }

        return wallets;
    }

    /// <inheritdoc />
    public async Task<bool> TryInsertAsync(Wallet wallet, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(wallet);

        const string sql = """
            INSERT INTO dbo.Wallets
                (Id, CustomerId, Currency, AvailableBalance, BlockedBalance, Status, CreatedAt)
            VALUES
                (@Id, @CustomerId, @Currency, @AvailableBalance, @BlockedBalance, @Status, @CreatedAt);
            """;

        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = wallet.Id;
        command.Parameters.Add("@CustomerId", SqlDbType.UniqueIdentifier).Value = wallet.CustomerId;
        command.Parameters.Add("@Currency", SqlDbType.TinyInt).Value = (byte)wallet.Currency;
        AddDecimalParameter(command, "@AvailableBalance", wallet.AvailableBalance);
        AddDecimalParameter(command, "@BlockedBalance", wallet.BlockedBalance);
        command.Parameters.Add("@Status", SqlDbType.TinyInt).Value = (byte)wallet.Status;
        command.Parameters.Add("@CreatedAt", SqlDbType.DateTimeOffset).Value = wallet.CreatedAt;

        try
        {
            return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
        }
        catch (SqlException exception) when (exception.Number is 2601 or 2627)
        {
            return false;
        }
    }

    /// <summary>
    /// TR: SQL reader satırını kontrollü Wallet.Restore factory'si üzerinden domain aggregate'ine dönüştürür.
    /// EN: Maps a SQL reader row into the domain aggregate through the controlled Wallet.Restore factory.
    /// </summary>
    /// <param name="reader">TR: Wallet satırında konumlanmış SQL reader. EN: SQL reader positioned on a wallet row.</param>
    /// <returns>TR: Rehydrate edilmiş Wallet aggregate'ini döndürür. EN: Returns the rehydrated Wallet aggregate.</returns>
    private static Wallet ReadWallet(SqlDataReader reader)
    {
        return Wallet.Restore(
            reader.GetGuid(reader.GetOrdinal("Id")),
            reader.GetGuid(reader.GetOrdinal("CustomerId")),
            (CurrencyCode)reader.GetByte(reader.GetOrdinal("Currency")),
            reader.GetDecimal(reader.GetOrdinal("AvailableBalance")),
            reader.GetDecimal(reader.GetOrdinal("BlockedBalance")),
            (WalletStatus)reader.GetByte(reader.GetOrdinal("Status")),
            reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("CreatedAt")));
    }

    /// <summary>TR: DECIMAL(19,4) finansal SQL parametresi ekler. EN: Adds a DECIMAL(19,4) financial SQL parameter.</summary>
    /// <param name="command">TR: Parametre eklenecek SQL komutu. EN: SQL command receiving the parameter.</param>
    /// <param name="name">TR: SQL parametre adı. EN: SQL parameter name.</param>
    /// <param name="value">TR: Decimal finansal değer. EN: Decimal financial value.</param>
    private static void AddDecimalParameter(SqlCommand command, string name, decimal value)
    {
        var parameter = command.Parameters.Add(name, SqlDbType.Decimal);
        parameter.Precision = 19;
        parameter.Scale = 4;
        parameter.Value = value;
    }

    /// <summary>TR: Customer kimliğinin boş olmadığını doğrular. EN: Validates that the customer identifier is not empty.</summary>
    /// <param name="customerId">TR: Doğrulanacak customer kimliği. EN: Customer identifier to validate.</param>
    private static void ValidateCustomerId(Guid customerId)
    {
        if (customerId == Guid.Empty) throw new ArgumentException("Customer identifier cannot be empty.", nameof(customerId));
    }
}
