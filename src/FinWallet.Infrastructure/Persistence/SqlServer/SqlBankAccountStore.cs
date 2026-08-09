using System.Data;
using FinWallet.Application.Banking;
using FinWallet.Domain.BankAccounts;
using FinWallet.Domain.Shared;
using Microsoft.Data.SqlClient;

namespace FinWallet.Infrastructure.Persistence.SqlServer;

/// <summary>
/// TR: BankAccount durable lifecycle/provider state'ini explicit parametreli MSSQL komutları ve status compare-and-set güncellemeleriyle saklar.
/// EN: Stores durable BankAccount lifecycle/provider state with explicit parameterized MSSQL commands and status compare-and-set updates.
/// </summary>
public sealed class SqlBankAccountStore : IBankAccountStore
{
    private readonly SqlConnectionFactory _connectionFactory;

    /// <summary>
    /// TR: SQL connection factory bağımlılığıyla BankAccount store'u oluşturur.
    /// EN: Creates the BankAccount store with its SQL connection-factory dependency.
    /// </summary>
    /// <param name="connectionFactory">TR: Pooled SQL connection factory. EN: Pooled SQL connection factory.</param>
    public SqlBankAccountStore(SqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    /// <inheritdoc />
    public Task<BankAccount?> FindOwnedAsync(Guid bankAccountId, Guid customerId, CancellationToken cancellationToken)
    {
        if (bankAccountId == Guid.Empty) throw new ArgumentException("Bank-account identifier cannot be empty.", nameof(bankAccountId));
        if (customerId == Guid.Empty) throw new ArgumentException("Customer identifier cannot be empty.", nameof(customerId));
        return FindSingleAsync("Id = @LookupId AND CustomerId = @CustomerId", bankAccountId, customerId, cancellationToken);
    }

    /// <inheritdoc />
    public Task<BankAccount?> FindByWalletAsync(Guid walletId, Guid customerId, CancellationToken cancellationToken)
    {
        if (walletId == Guid.Empty) throw new ArgumentException("Wallet identifier cannot be empty.", nameof(walletId));
        if (customerId == Guid.Empty) throw new ArgumentException("Customer identifier cannot be empty.", nameof(customerId));
        return FindSingleAsync("WalletId = @LookupId AND CustomerId = @CustomerId", walletId, customerId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task InsertAsync(BankAccount bankAccount, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(bankAccount);

        const string sql = """
            INSERT INTO dbo.BankAccounts
                (Id, CustomerId, WalletId, Currency, ExternalAccountId, ExternalIban, Status, CreatedAt, UpdatedAt)
            VALUES
                (@Id, @CustomerId, @WalletId, @Currency, @ExternalAccountId, @ExternalIban, @Status, @CreatedAt, @UpdatedAt);
            """;

        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        AddIdentityParameters(command, bankAccount);
        AddMutableParameters(command, bankAccount);
        command.Parameters.Add("@CreatedAt", SqlDbType.DateTimeOffset).Value = bankAccount.CreatedAt;

        var affectedRows = await command.ExecuteNonQueryAsync(cancellationToken);
        if (affectedRows != 1) throw new InvalidOperationException("BankAccount insert did not affect exactly one row.");
    }

    /// <inheritdoc />
    public async Task<bool> TryUpdateAsync(BankAccount bankAccount, BankAccountStatus expectedStatus, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(bankAccount);

        const string sql = """
            UPDATE dbo.BankAccounts
            SET ExternalAccountId = @ExternalAccountId,
                ExternalIban = @ExternalIban,
                Status = @Status,
                UpdatedAt = @UpdatedAt
            WHERE Id = @Id
              AND CustomerId = @CustomerId
              AND WalletId = @WalletId
              AND Currency = @Currency
              AND Status = @ExpectedStatus;
            """;

        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        AddIdentityParameters(command, bankAccount);
        AddMutableParameters(command, bankAccount);
        command.Parameters.Add("@ExpectedStatus", SqlDbType.TinyInt).Value = (byte)expectedStatus;

        var affectedRows = await command.ExecuteNonQueryAsync(cancellationToken);
        return affectedRows == 1;
    }

    /// <summary>
    /// TR: ID veya Wallet ID lookup koşuluyla tek BankAccount satırı yükler ve ortak materialization uygular.
    /// EN: Loads one BankAccount row using either ID or Wallet-ID lookup and applies shared materialization.
    /// </summary>
    /// <param name="predicate">TR: Sabit, kod içinde tanımlanan WHERE predicate metni. EN: Fixed WHERE predicate text defined in code.</param>
    /// <param name="lookupId">TR: BankAccount veya Wallet lookup kimliği. EN: BankAccount or Wallet lookup identifier.</param>
    /// <param name="customerId">TR: Owner customer kimliği. EN: Owner-customer identifier.</param>
    /// <param name="cancellationToken">TR: SQL sorgu iptal sinyali. EN: SQL-query cancellation signal.</param>
    /// <returns>TR: Eşleşen BankAccount aggregate'ini; yoksa null döndürür. EN: Returns matching BankAccount aggregate, or null when absent.</returns>
    private async Task<BankAccount?> FindSingleAsync(string predicate, Guid lookupId, Guid customerId, CancellationToken cancellationToken)
    {
        var sql = $"""
            SELECT Id, CustomerId, WalletId, Currency, ExternalAccountId, ExternalIban, Status, CreatedAt, UpdatedAt
            FROM dbo.BankAccounts
            WHERE {predicate};
            """;

        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@LookupId", SqlDbType.UniqueIdentifier).Value = lookupId;
        command.Parameters.Add("@CustomerId", SqlDbType.UniqueIdentifier).Value = customerId;

        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadBankAccount(reader) : null;
    }

    /// <summary>TR: BankAccount immutable identity/ownership parametrelerini SQL komutuna ekler. EN: Adds immutable BankAccount identity/ownership parameters to a SQL command.</summary>
    /// <param name="command">TR: Parametre eklenecek SQL komutu. EN: SQL command receiving parameters.</param>
    /// <param name="bankAccount">TR: Parametre değerlerini sağlayan aggregate. EN: Aggregate supplying parameter values.</param>
    private static void AddIdentityParameters(SqlCommand command, BankAccount bankAccount)
    {
        command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = bankAccount.Id;
        command.Parameters.Add("@CustomerId", SqlDbType.UniqueIdentifier).Value = bankAccount.CustomerId;
        command.Parameters.Add("@WalletId", SqlDbType.UniqueIdentifier).Value = bankAccount.WalletId;
        command.Parameters.Add("@Currency", SqlDbType.TinyInt).Value = (byte)bankAccount.Currency;
    }

    /// <summary>TR: BankAccount provider/lifecycle mutable parametrelerini SQL komutuna ekler. EN: Adds mutable BankAccount provider/lifecycle parameters to a SQL command.</summary>
    /// <param name="command">TR: Parametre eklenecek SQL komutu. EN: SQL command receiving parameters.</param>
    /// <param name="bankAccount">TR: Parametre değerlerini sağlayan aggregate. EN: Aggregate supplying parameter values.</param>
    private static void AddMutableParameters(SqlCommand command, BankAccount bankAccount)
    {
        command.Parameters.Add("@ExternalAccountId", SqlDbType.UniqueIdentifier).Value = (object?)bankAccount.ExternalAccountId ?? DBNull.Value;
        command.Parameters.Add("@ExternalIban", SqlDbType.NVarChar, 64).Value = (object?)bankAccount.ExternalIban ?? DBNull.Value;
        command.Parameters.Add("@Status", SqlDbType.TinyInt).Value = (byte)bankAccount.Status;
        command.Parameters.Add("@UpdatedAt", SqlDbType.DateTimeOffset).Value = bankAccount.UpdatedAt;
    }

    /// <summary>TR: SQL satırını controlled BankAccount.Restore factory'si üzerinden aggregate'e dönüştürür. EN: Maps a SQL row into an aggregate through the controlled BankAccount.Restore factory.</summary>
    /// <param name="reader">TR: BankAccount satırında konumlanmış SQL reader. EN: SQL reader positioned on a BankAccount row.</param>
    /// <returns>TR: Rehydrate edilmiş BankAccount aggregate'ini döndürür. EN: Returns rehydrated BankAccount aggregate.</returns>
    private static BankAccount ReadBankAccount(SqlDataReader reader)
    {
        var externalAccountOrdinal = reader.GetOrdinal("ExternalAccountId");
        var externalIbanOrdinal = reader.GetOrdinal("ExternalIban");
        return BankAccount.Restore(
            reader.GetGuid(reader.GetOrdinal("Id")),
            reader.GetGuid(reader.GetOrdinal("CustomerId")),
            reader.GetGuid(reader.GetOrdinal("WalletId")),
            (CurrencyCode)reader.GetByte(reader.GetOrdinal("Currency")),
            reader.IsDBNull(externalAccountOrdinal) ? null : reader.GetGuid(externalAccountOrdinal),
            reader.IsDBNull(externalIbanOrdinal) ? null : reader.GetString(externalIbanOrdinal),
            (BankAccountStatus)reader.GetByte(reader.GetOrdinal("Status")),
            reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("CreatedAt")),
            reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("UpdatedAt")));
    }
}
