namespace FinWallet.Infrastructure.Persistence.SqlServer;

/// <summary>
/// TR: FinWallet'ın MSSQL bağlantısı için yalnızca deployment sırasında sağlanan connection string değerini taşır; finansal transaction veya timeout business kararlarını yapılandırmaz.
/// EN: Carries only the deployment-supplied connection string used by FinWallet to connect to MSSQL; it does not make financial transaction or timeout business decisions configurable.
/// </summary>
public sealed class SqlServerSettings
{
    /// <summary>
    /// TR: MSSQL deployment ayarını oluşturur ve boş connection string kullanımını engeller.
    /// EN: Creates MSSQL deployment settings and rejects an empty connection string.
    /// </summary>
    /// <param name="connectionString">
    /// TR: Secret/configuration kaynağından sağlanan MSSQL connection string; loglanmamalıdır.
    /// EN: MSSQL connection string supplied by a secret/configuration source; it must not be logged.
    /// </param>
    public SqlServerSettings(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ConnectionString = connectionString;
    }

    /// <summary>
    /// TR: Secret/configuration kaynağından alınan MSSQL connection string değerini döndürür; hiçbir log alanına yazılmamalıdır.
    /// EN: Gets the MSSQL connection string obtained from the secret/configuration source; it must never be written to logs.
    /// </summary>
    public string ConnectionString { get; }
}
