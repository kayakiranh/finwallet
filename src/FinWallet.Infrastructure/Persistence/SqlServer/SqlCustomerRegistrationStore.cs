using System.Data;
using FinWallet.Application.Registration;
using FinWallet.Domain.Authentication;
using FinWallet.Domain.Customers;
using Microsoft.Data.SqlClient;

namespace FinWallet.Infrastructure.Persistence.SqlServer;

/// <summary>
/// TR: Customer registration ve activation state'ini parametreli MSSQL komutlarıyla kalıcılaştıran store'dur; Customer ile CustomerCredential ilk kaydını tek transaction içinde yazar.
/// EN: Store that persists customer registration and activation state through parameterized MSSQL commands and writes the initial Customer plus CustomerCredential records in one transaction.
/// </summary>
public sealed class SqlCustomerRegistrationStore : ICustomerRegistrationStore
{
    private readonly SqlConnectionFactory _connectionFactory;

    /// <summary>TR: SQL connection factory bağımlılığıyla registration store'u oluşturur. EN: Creates the registration store with its SQL connection-factory dependency.</summary>
    /// <param name="connectionFactory">TR: Her operasyon için yeni pooled SQL connection oluşturan factory. EN: Factory creating a new pooled SQL connection for each operation.</param>
    public SqlCustomerRegistrationStore(SqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    /// <summary>TR: Normalize telefon numarasının mevcut olup olmadığını kontrol eder; asıl yarış güvenliği DB unique constraint'tedir. EN: Checks whether the normalized phone number exists; the actual race-safety guarantee is the database unique constraint.</summary>
    /// <param name="normalizedPhoneNumber">TR: Normalize uluslararası telefon numarası. EN: Normalized international phone number.</param>
    /// <param name="cancellationToken">TR: SQL işlem iptal sinyali. EN: Cancellation signal for SQL operations.</param>
    /// <returns>TR: Telefon numarası mevcutsa true döndürür. EN: Returns true when the phone number exists.</returns>
    public async Task<bool> ExistsByPhoneNumberAsync(string normalizedPhoneNumber, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedPhoneNumber);
        const string sql = "SELECT TOP (1) 1 FROM dbo.Customers WHERE PhoneNumber = @PhoneNumber;";

        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@PhoneNumber", SqlDbType.VarChar, 16).Value = normalizedPhoneNumber;
        return await command.ExecuteScalarAsync(cancellationToken) is not null;
    }

    /// <summary>TR: Pending Customer ve CustomerCredential kayıtlarını tek MSSQL transaction içinde oluşturur. EN: Creates pending Customer and CustomerCredential records inside one MSSQL transaction.</summary>
    /// <param name="customer">TR: PendingVerification durumundaki yeni müşteri. EN: New customer in PendingVerification state.</param>
    /// <param name="credential">TR: Aynı müşteriye ait güvenli credential kaydı. EN: Secure credential record for the same customer.</param>
    /// <param name="cancellationToken">TR: SQL transaction iptal sinyali. EN: Cancellation signal for the SQL transaction.</param>
    /// <exception cref="RegistrationConflictException">TR: Telefon unique constraint'e çarparsa oluşur. EN: Thrown when the phone number violates the unique constraint.</exception>
    public async Task CreatePendingCustomerAsync(Customer customer, CustomerCredential credential, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(customer);
        ArgumentNullException.ThrowIfNull(credential);

        if (customer.Id != credential.CustomerId)
        {
            throw new ArgumentException("Customer and credential identifiers must match.", nameof(credential));
        }

        if (customer.Status != CustomerStatus.PendingVerification)
        {
            throw new ArgumentException("A new registration must be persisted in PendingVerification state.", nameof(customer));
        }

        const string customerSql = """
            INSERT INTO dbo.Customers (Id, CountryCode, PhoneNumber, Email, Status, CreatedAt)
            VALUES (@Id, @CountryCode, @PhoneNumber, @Email, @Status, @CreatedAt);
            """;
        const string credentialSql = """
            INSERT INTO dbo.CustomerCredentials
                (CustomerId, PasswordHash, PasswordSalt, PasswordHashVersion, FailedLoginCount, LockedUntil, PasswordChangedAt)
            VALUES
                (@CustomerId, @PasswordHash, @PasswordSalt, @PasswordHashVersion, @FailedLoginCount, @LockedUntil, @PasswordChangedAt);
            """;

        try
        {
            await using var connection = _connectionFactory.Create();
            await connection.OpenAsync(cancellationToken);
            await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

            await using (var customerCommand = new SqlCommand(customerSql, connection, transaction))
            {
                customerCommand.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = customer.Id;
                customerCommand.Parameters.Add("@CountryCode", SqlDbType.Char, 2).Value = customer.CountryCode;
                customerCommand.Parameters.Add("@PhoneNumber", SqlDbType.VarChar, 16).Value = customer.PhoneNumber;
                customerCommand.Parameters.Add("@Email", SqlDbType.NVarChar, 320).Value = (object?)customer.Email ?? DBNull.Value;
                customerCommand.Parameters.Add("@Status", SqlDbType.TinyInt).Value = (byte)customer.Status;
                customerCommand.Parameters.Add("@CreatedAt", SqlDbType.DateTimeOffset).Value = customer.CreatedAt;
                await customerCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            await using (var credentialCommand = new SqlCommand(credentialSql, connection, transaction))
            {
                AddCredentialParameters(credentialCommand, credential);
                await credentialCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch (SqlException exception) when (IsUniqueConstraintViolation(exception))
        {
            throw new RegistrationConflictException("A registration already exists for the supplied phone number.");
        }
    }

    /// <summary>TR: Customer kaydını kimliğiyle yükleyip kontrollü domain Restore factory'si üzerinden yeniden oluşturur. EN: Loads a Customer by identifier and rehydrates it through the controlled domain Restore factory.</summary>
    /// <param name="customerId">TR: Yüklenecek müşteri kimliği. EN: Customer identifier to load.</param>
    /// <param name="cancellationToken">TR: SQL işlem iptal sinyali. EN: Cancellation signal for SQL operations.</param>
    /// <returns>TR: Kayıt bulunursa Customer, aksi halde null döndürür. EN: Returns Customer when found, otherwise null.</returns>
    public async Task<Customer?> FindCustomerAsync(Guid customerId, CancellationToken cancellationToken)
    {
        if (customerId == Guid.Empty)
        {
            throw new ArgumentException("Customer identifier cannot be empty.", nameof(customerId));
        }

        const string sql = "SELECT Id, CountryCode, PhoneNumber, Email, Status, CreatedAt FROM dbo.Customers WHERE Id = @Id;";
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = customerId;
        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? SqlAuthenticationMaterializer.ReadCustomer(reader) : null;
    }

    /// <summary>TR: OTP doğrulaması sonrası yalnızca PendingVerification -> Active geçişini koşullu SQL UPDATE ile kalıcılaştırır. EN: Persists only the PendingVerification -> Active transition after OTP verification through a conditional SQL UPDATE.</summary>
    /// <param name="customer">TR: Active duruma geçirilmiş Customer domain nesnesi. EN: Customer domain object transitioned to Active.</param>
    /// <param name="cancellationToken">TR: SQL update iptal sinyali. EN: Cancellation signal for the SQL update.</param>
    public async Task UpdateCustomerAsync(Customer customer, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(customer);
        if (customer.Status != CustomerStatus.Active)
        {
            throw new ArgumentException("Registration store may only persist activation to Active state.", nameof(customer));
        }

        const string sql = """
            UPDATE dbo.Customers
            SET Status = @ActiveStatus
            WHERE Id = @Id
              AND Status = @PendingStatus;
            """;

        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = customer.Id;
        command.Parameters.Add("@ActiveStatus", SqlDbType.TinyInt).Value = (byte)CustomerStatus.Active;
        command.Parameters.Add("@PendingStatus", SqlDbType.TinyInt).Value = (byte)CustomerStatus.PendingVerification;

        var affectedRows = await command.ExecuteNonQueryAsync(cancellationToken);
        if (affectedRows != 1)
        {
            throw new InvalidOperationException("Customer activation did not affect exactly one pending customer row.");
        }
    }

    /// <summary>TR: CustomerCredential alanlarını açık SQL tipleriyle parametre olarak ekler. EN: Adds CustomerCredential fields as explicitly typed SQL parameters.</summary>
    /// <param name="command">TR: Parametre eklenecek SQL komutu. EN: SQL command receiving parameters.</param>
    /// <param name="credential">TR: Parametre değerlerini sağlayan credential nesnesi. EN: Credential object supplying parameter values.</param>
    private static void AddCredentialParameters(SqlCommand command, CustomerCredential credential)
    {
        command.Parameters.Add("@CustomerId", SqlDbType.UniqueIdentifier).Value = credential.CustomerId;
        command.Parameters.Add("@PasswordHash", SqlDbType.VarChar, 128).Value = credential.PasswordHash;
        command.Parameters.Add("@PasswordSalt", SqlDbType.VarChar, 64).Value = credential.PasswordSalt;
        command.Parameters.Add("@PasswordHashVersion", SqlDbType.Int).Value = credential.PasswordHashVersion;
        command.Parameters.Add("@FailedLoginCount", SqlDbType.Int).Value = credential.FailedLoginCount;
        command.Parameters.Add("@LockedUntil", SqlDbType.DateTimeOffset).Value = (object?)credential.LockedUntil ?? DBNull.Value;
        command.Parameters.Add("@PasswordChangedAt", SqlDbType.DateTimeOffset).Value = credential.PasswordChangedAt;
    }

    /// <summary>TR: SQL Server 2601/2627 kodlarını unique constraint ihlali olarak sınıflandırır. EN: Classifies SQL Server error numbers 2601/2627 as unique-constraint violations.</summary>
    /// <param name="exception">TR: Sınıflandırılacak SqlException. EN: SqlException to classify.</param>
    /// <returns>TR: Unique constraint ihlaliyse true döndürür. EN: Returns true when the error is a unique-constraint violation.</returns>
    private static bool IsUniqueConstraintViolation(SqlException exception)
    {
        return exception.Number is 2601 or 2627;
    }
}
