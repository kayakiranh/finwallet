using Microsoft.Data.SqlClient;

namespace FinWallet.Infrastructure.Persistence.SqlServer;

/// <summary>
/// TR: Her persistence operasyonu için kapalı ve sahipliği çağırana ait yeni SqlConnection oluşturan factory'dir; connection pooling Microsoft.Data.SqlClient tarafından yönetilir.
/// EN: Factory that creates a new closed SqlConnection owned by the caller for each persistence operation; connection pooling is managed by Microsoft.Data.SqlClient.
/// </summary>
public sealed class SqlConnectionFactory
{
    private readonly SqlServerSettings _settings;

    /// <summary>
    /// TR: Doğrulanmış SQL Server deployment ayarlarıyla connection factory'yi oluşturur.
    /// EN: Creates the connection factory with validated SQL Server deployment settings.
    /// </summary>
    /// <param name="settings">
    /// TR: Secret/configuration kaynağından connection string taşıyan MSSQL ayarları.
    /// EN: MSSQL settings carrying the connection string from a secret/configuration source.
    /// </param>
    public SqlConnectionFactory(SqlServerSettings settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    /// <summary>
    /// TR: Henüz açılmamış yeni SqlConnection nesnesi oluşturur; `await using` ile dispose etme sorumluluğu çağırandadır.
    /// EN: Creates a new unopened SqlConnection; the caller owns disposal, normally through `await using`.
    /// </summary>
    /// <returns>
    /// TR: Connection pooling'e katılabilecek yeni SqlConnection nesnesini döndürür.
    /// EN: Returns a new SqlConnection eligible for connection pooling.
    /// </returns>
    public SqlConnection Create()
    {
        return new SqlConnection(_settings.ConnectionString);
    }
}
