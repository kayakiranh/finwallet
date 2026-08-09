using System.Data;
using FinWallet.Application.Authentication;
using FinWallet.Domain.Authentication;
using Microsoft.Data.SqlClient;

namespace FinWallet.Infrastructure.Persistence.SqlServer;

/// <summary>
/// TR: Login, credential lockout, session ve refresh-token lifecycle state'ini explicit parametreli MSSQL komutları ve kısa concurrency-safe transaction sınırlarıyla kalıcılaştırır.
/// EN: Persists login, credential lockout, session and refresh-token lifecycle state using explicit parameterized MSSQL commands and short concurrency-safe transaction boundaries.
/// </summary>
public sealed class SqlAuthenticationStore : IAuthenticationStore
{
    private readonly SqlConnectionFactory _connectionFactory;

    /// <summary>
    /// TR: SQL connection factory bağımlılığıyla authentication store'u oluşturur.
    /// EN: Creates the authentication store with its SQL connection-factory dependency.
    /// </summary>
    /// <param name="connectionFactory">TR: Her persistence operasyonu için yeni pooled SQL connection oluşturan factory. EN: Factory creating a new pooled SQL connection for each persistence operation.</param>
    public SqlAuthenticationStore(SqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    /// <summary>
    /// TR: Normalize telefon numarasıyla Customer ve CustomerCredential state'ini tek join sorgusunda yükler; hassas credential alanlarını yalnızca domain materialization için kullanır.
    /// EN: Loads Customer and CustomerCredential state in a single join query by normalized phone number and uses sensitive credential fields only for domain materialization.
    /// </summary>
    /// <param name="normalizedPhoneNumber">TR: Login lookup için normalize telefon numarası. EN: Normalized phone number used for login lookup.</param>
    /// <param name="cancellationToken">TR: SQL açma/sorgu iptal sinyali. EN: Cancellation signal for SQL open/query operations.</param>
    /// <returns>TR: Eşleşen login datasını; kayıt yoksa null döndürür. EN: Returns matching login data, or null when no record exists.</returns>
    public async Task<AuthenticationLoginData?> FindLoginDataAsync(
        string normalizedPhoneNumber,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedPhoneNumber);

        const string sql = """
            SELECT
                C.Id AS C_Id,
                C.CountryCode AS C_CountryCode,
                C.PhoneNumber AS C_PhoneNumber,
                C.Email AS C_Email,
                C.Status AS C_Status,
                C.CreatedAt AS C_CreatedAt,
                CR.CustomerId AS CR_CustomerId,
                CR.PasswordHash AS CR_PasswordHash,
                CR.PasswordSalt AS CR_PasswordSalt,
                CR.PasswordHashVersion AS CR_PasswordHashVersion,
                CR.FailedLoginCount AS CR_FailedLoginCount,
                CR.LockedUntil AS CR_LockedUntil,
                CR.PasswordChangedAt AS CR_PasswordChangedAt
            FROM dbo.Customers AS C
            INNER JOIN dbo.CustomerCredentials AS CR ON CR.CustomerId = C.Id
            WHERE C.PhoneNumber = @PhoneNumber;
            """;

        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@PhoneNumber", SqlDbType.VarChar, 16).Value = normalizedPhoneNumber;

        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new AuthenticationLoginData(
            SqlAuthenticationMaterializer.ReadCustomer(reader, "C_"),
            SqlAuthenticationMaterializer.ReadCredential(reader, "CR_"));
    }

    /// <summary>
    /// TR: Başarısız login'i credential satırını UPDLOCK altında güncel state ile yeniden yükleyerek atomik uygular ve paralel yanlış login'lerde lost-update oluşmasını engeller.
    /// EN: Atomically applies a failed login by reloading the credential row under UPDLOCK and prevents lost updates across concurrent failed-login attempts.
    /// </summary>
    /// <param name="customerId">TR: Başarısız login'in ait olduğu müşteri kimliği. EN: Customer identifier associated with the failed login.</param>
    /// <param name="failedAt">TR: Başarısız login'in gerçekleştiği UTC zaman bilgisi. EN: UTC timestamp at which the failed login occurred.</param>
    /// <param name="cancellationToken">TR: SQL transaction iptal sinyali. EN: Cancellation signal for the SQL transaction.</param>
    public async Task RegisterFailedLoginAsync(
        Guid customerId,
        DateTimeOffset failedAt,
        CancellationToken cancellationToken)
    {
        if (customerId == Guid.Empty)
        {
            throw new ArgumentException("Customer identifier cannot be empty.", nameof(customerId));
        }

        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);

        var currentCredential = await ReadCredentialForUpdateAsync(
            connection,
            transaction,
            customerId,
            cancellationToken);

        if (currentCredential is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new InvalidOperationException("Credential was not found for failed-login persistence.");
        }

        currentCredential.RegisterFailedLogin(failedAt);
        await UpdateCredentialStateAsync(connection, transaction, currentCredential, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    /// <summary>
    /// TR: Başarılı parola doğrulamasını credential satırını UPDLOCK altında yeniden kontrol ederek finalize eder; password snapshot değişmiş veya paralel denemeler geçici lock oluşturmuşsa session yaratmaz.
    /// EN: Finalizes successful password verification by rechecking the credential row under UPDLOCK and does not create a session if the password snapshot changed or concurrent attempts created a temporary lock.
    /// </summary>
    /// <param name="credential">TR: Parola doğrulamasında kullanılan credential snapshot'ı. EN: Credential snapshot used during password verification.</param>
    /// <param name="session">TR: Oluşturulmak istenen yeni müşteri session nesnesi. EN: New customer-session object intended to be created.</param>
    /// <param name="refreshToken">TR: Yeni session'a bağlı ilk refresh-token kaydı. EN: Initial refresh-token record associated with the new session.</param>
    /// <param name="cancellationToken">TR: SQL transaction iptal sinyali. EN: Cancellation signal for the SQL transaction.</param>
    /// <returns>TR: Session transaction'ı güvenli biçimde commit edildiyse true; lock veya password state değişimi nedeniyle reddedildiyse false döndürür. EN: Returns true when the session transaction commits safely, or false when rejected because of lock or password-state changes.</returns>
    public async Task<bool> TryCreateSessionAsync(
        CustomerCredential credential,
        CustomerSession session,
        RefreshToken refreshToken,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(credential);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(refreshToken);

        if (credential.CustomerId != session.CustomerId || refreshToken.SessionId != session.Id)
        {
            throw new ArgumentException("Credential, session and refresh-token relationships are inconsistent.");
        }

        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);

        var currentCredential = await ReadCredentialForUpdateAsync(
            connection,
            transaction,
            credential.CustomerId,
            cancellationToken);

        if (currentCredential is null
            || currentCredential.IsLocked(session.CreatedAt)
            || !HasSamePasswordMaterial(currentCredential, credential))
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        currentCredential.RegisterSuccessfulLogin();
        await UpdateCredentialStateAsync(connection, transaction, currentCredential, cancellationToken);
        await InsertSessionAsync(connection, transaction, session, cancellationToken);
        await InsertRefreshTokenAsync(connection, transaction, refreshToken, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    /// <summary>
    /// TR: Ham refresh token'dan türetilmiş hash ile RefreshToken, CustomerSession ve Customer state'ini tek join sorgusunda yükler.
    /// EN: Loads RefreshToken, CustomerSession and Customer state in a single join query using the hash derived from the raw refresh token.
    /// </summary>
    /// <param name="tokenHash">TR: Deterministik SHA-256 refresh-token lookup hash'i. EN: Deterministic SHA-256 refresh-token lookup hash.</param>
    /// <param name="cancellationToken">TR: SQL açma/sorgu iptal sinyali. EN: Cancellation signal for SQL open/query operations.</param>
    /// <returns>TR: Eşleşen refresh state'ini; token bilinmiyorsa null döndürür. EN: Returns matching refresh state, or null when the token is unknown.</returns>
    public async Task<RefreshAuthenticationData?> FindRefreshDataAsync(
        string tokenHash,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);

        const string sql = """
            SELECT
                C.Id AS C_Id,
                C.CountryCode AS C_CountryCode,
                C.PhoneNumber AS C_PhoneNumber,
                C.Email AS C_Email,
                C.Status AS C_Status,
                C.CreatedAt AS C_CreatedAt,
                S.Id AS S_Id,
                S.CustomerId AS S_CustomerId,
                S.DeviceId AS S_DeviceId,
                S.CreatedAt AS S_CreatedAt,
                S.LastActivityAt AS S_LastActivityAt,
                S.ExpiresAt AS S_ExpiresAt,
                S.RevokedAt AS S_RevokedAt,
                R.Id AS R_Id,
                R.SessionId AS R_SessionId,
                R.TokenHash AS R_TokenHash,
                R.CreatedAt AS R_CreatedAt,
                R.ExpiresAt AS R_ExpiresAt,
                R.ConsumedAt AS R_ConsumedAt,
                R.RevokedAt AS R_RevokedAt,
                R.ReplacedByTokenId AS R_ReplacedByTokenId
            FROM dbo.RefreshTokens AS R
            INNER JOIN dbo.CustomerSessions AS S ON S.Id = R.SessionId
            INNER JOIN dbo.Customers AS C ON C.Id = S.CustomerId
            WHERE R.TokenHash = @TokenHash;
            """;

        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@TokenHash", SqlDbType.Char, 64).Value = tokenHash;

        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new RefreshAuthenticationData(
            SqlAuthenticationMaterializer.ReadCustomer(reader, "C_"),
            SqlAuthenticationMaterializer.ReadSession(reader, "S_"),
            SqlAuthenticationMaterializer.ReadRefreshToken(reader, "R_"));
    }

    /// <summary>
    /// TR: Replacement token'ı transaction içine ekler, eski token'ı `ConsumedAt IS NULL AND RevokedAt IS NULL` koşuluyla compare-and-set olarak consume eder ve yalnızca tek yarışmacının rotation commit etmesine izin verir.
    /// EN: Inserts the replacement token inside the transaction, conditionally consumes the old token with `ConsumedAt IS NULL AND RevokedAt IS NULL` compare-and-set semantics and allows only one racing request to commit rotation.
    /// </summary>
    /// <param name="session">TR: Son aktivite zamanı güncellenmiş session state'i. EN: Session state with updated last-activity time.</param>
    /// <param name="consumedToken">TR: Consume edilmek istenen eski refresh token. EN: Previous refresh token intended to be consumed.</param>
    /// <param name="replacementToken">TR: Rotation ile oluşturulan yeni refresh token. EN: New refresh token created by rotation.</param>
    /// <param name="cancellationToken">TR: SQL transaction iptal sinyali. EN: Cancellation signal for the SQL transaction.</param>
    /// <returns>TR: Bu request CAS yarışını kazanıp commit ettiyse true; token başka request tarafından önce consume/revoke edildiyse false döndürür. EN: Returns true when this request wins the CAS race and commits; false when another request already consumed or revoked the token.</returns>
    public async Task<bool> TryRotateRefreshTokenAsync(
        CustomerSession session,
        RefreshToken consumedToken,
        RefreshToken replacementToken,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(consumedToken);
        ArgumentNullException.ThrowIfNull(replacementToken);

        if (consumedToken.SessionId != session.Id || replacementToken.SessionId != session.Id)
        {
            throw new ArgumentException("Refresh-token rotation objects do not belong to the same session.");
        }

        if (consumedToken.ConsumedAt is null || consumedToken.ReplacedByTokenId != replacementToken.Id)
        {
            throw new ArgumentException("Consumed token domain state does not describe the supplied replacement token.");
        }

        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);

        await InsertRefreshTokenAsync(connection, transaction, replacementToken, cancellationToken);

        const string consumeSql = """
            UPDATE dbo.RefreshTokens
            SET ConsumedAt = @ConsumedAt,
                ReplacedByTokenId = @ReplacedByTokenId
            WHERE Id = @Id
              AND SessionId = @SessionId
              AND TokenHash = @TokenHash
              AND ConsumedAt IS NULL
              AND RevokedAt IS NULL;
            """;

        await using (var consumeCommand = new SqlCommand(consumeSql, connection, transaction))
        {
            consumeCommand.Parameters.Add("@ConsumedAt", SqlDbType.DateTimeOffset).Value = consumedToken.ConsumedAt.Value;
            consumeCommand.Parameters.Add("@ReplacedByTokenId", SqlDbType.UniqueIdentifier).Value = replacementToken.Id;
            consumeCommand.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = consumedToken.Id;
            consumeCommand.Parameters.Add("@SessionId", SqlDbType.UniqueIdentifier).Value = session.Id;
            consumeCommand.Parameters.Add("@TokenHash", SqlDbType.Char, 64).Value = consumedToken.TokenHash;

            var affectedRows = await consumeCommand.ExecuteNonQueryAsync(cancellationToken);
            if (affectedRows != 1)
            {
                await transaction.RollbackAsync(cancellationToken);
                return false;
            }
        }

        const string sessionSql = """
            UPDATE dbo.CustomerSessions
            SET LastActivityAt = @LastActivityAt
            WHERE Id = @Id
              AND RevokedAt IS NULL
              AND ExpiresAt > @LastActivityAt;
            """;

        await using (var sessionCommand = new SqlCommand(sessionSql, connection, transaction))
        {
            sessionCommand.Parameters.Add("@LastActivityAt", SqlDbType.DateTimeOffset).Value = session.LastActivityAt;
            sessionCommand.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = session.Id;
            var sessionRows = await sessionCommand.ExecuteNonQueryAsync(cancellationToken);
            if (sessionRows != 1)
            {
                await transaction.RollbackAsync(cancellationToken);
                return false;
            }
        }

        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    /// <summary>
    /// TR: Güvenlik olayı veya refresh-token reuse tespitinde session ve ona bağlı henüz revoke edilmemiş tüm refresh token kayıtlarını tek transaction içinde revoke eder.
    /// EN: Revokes a session and all associated refresh-token records not yet revoked within one transaction after a security event or refresh-token reuse detection.
    /// </summary>
    /// <param name="sessionId">TR: Revoke edilecek session kimliği. EN: Session identifier to revoke.</param>
    /// <param name="revokedAt">TR: Güvenlik revoke UTC zamanı. EN: UTC security-revocation time.</param>
    /// <param name="cancellationToken">TR: SQL transaction iptal sinyali. EN: Cancellation signal for the SQL transaction.</param>
    public async Task RevokeSessionAsync(
        Guid sessionId,
        DateTimeOffset revokedAt,
        CancellationToken cancellationToken)
    {
        if (sessionId == Guid.Empty)
        {
            throw new ArgumentException("Session identifier cannot be empty.", nameof(sessionId));
        }

        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);

        const string sessionSql = """
            UPDATE dbo.CustomerSessions
            SET RevokedAt = COALESCE(RevokedAt, @RevokedAt)
            WHERE Id = @SessionId;
            """;

        await using (var sessionCommand = new SqlCommand(sessionSql, connection, transaction))
        {
            sessionCommand.Parameters.Add("@RevokedAt", SqlDbType.DateTimeOffset).Value = revokedAt;
            sessionCommand.Parameters.Add("@SessionId", SqlDbType.UniqueIdentifier).Value = sessionId;
            EnsureSingleRow(await sessionCommand.ExecuteNonQueryAsync(cancellationToken), "Session revoke");
        }

        const string tokenSql = """
            UPDATE dbo.RefreshTokens
            SET RevokedAt = COALESCE(RevokedAt, @RevokedAt)
            WHERE SessionId = @SessionId;
            """;

        await using (var tokenCommand = new SqlCommand(tokenSql, connection, transaction))
        {
            tokenCommand.Parameters.Add("@RevokedAt", SqlDbType.DateTimeOffset).Value = revokedAt;
            tokenCommand.Parameters.Add("@SessionId", SqlDbType.UniqueIdentifier).Value = sessionId;
            await tokenCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    /// <summary>
    /// TR: Credential satırını mevcut transaction içinde UPDLOCK/ROWLOCK ile yükler ve domain state olarak materialize eder.
    /// EN: Loads the credential row under UPDLOCK/ROWLOCK inside the current transaction and materializes it as domain state.
    /// </summary>
    /// <param name="connection">TR: Açık SQL connection. EN: Open SQL connection.</param>
    /// <param name="transaction">TR: Aktif SQL transaction. EN: Active SQL transaction.</param>
    /// <param name="customerId">TR: Kilit altında yüklenecek müşteri credential kimliği. EN: Customer credential identifier to load under lock.</param>
    /// <param name="cancellationToken">TR: SQL sorgu iptal sinyali. EN: Cancellation signal for the SQL query.</param>
    /// <returns>TR: Credential bulunursa domain nesnesini; bulunamazsa null döndürür. EN: Returns the credential domain object when found, otherwise null.</returns>
    private static async Task<CustomerCredential?> ReadCredentialForUpdateAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        Guid customerId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                CustomerId AS CR_CustomerId,
                PasswordHash AS CR_PasswordHash,
                PasswordSalt AS CR_PasswordSalt,
                PasswordHashVersion AS CR_PasswordHashVersion,
                FailedLoginCount AS CR_FailedLoginCount,
                LockedUntil AS CR_LockedUntil,
                PasswordChangedAt AS CR_PasswordChangedAt
            FROM dbo.CustomerCredentials WITH (UPDLOCK, ROWLOCK)
            WHERE CustomerId = @CustomerId;
            """;

        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@CustomerId", SqlDbType.UniqueIdentifier).Value = customerId;
        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return SqlAuthenticationMaterializer.ReadCredential(reader, "CR_");
    }

    /// <summary>
    /// TR: İki credential snapshot'ının aynı parola hash materyalini temsil edip etmediğini sabit alan karşılaştırmalarıyla belirler.
    /// EN: Determines whether two credential snapshots represent the same password-hash material using fixed field comparisons.
    /// </summary>
    /// <param name="current">TR: DB lock altında yeniden yüklenen güncel credential. EN: Current credential reloaded under a DB lock.</param>
    /// <param name="verifiedSnapshot">TR: Parola doğrulaması sırasında kullanılan credential snapshot'ı. EN: Credential snapshot used during password verification.</param>
    /// <returns>TR: Hash, salt, version ve password-change zamanı aynıysa true döndürür. EN: Returns true when hash, salt, version and password-change time are identical.</returns>
    private static bool HasSamePasswordMaterial(CustomerCredential current, CustomerCredential verifiedSnapshot)
    {
        return string.Equals(current.PasswordHash, verifiedSnapshot.PasswordHash, StringComparison.Ordinal)
            && string.Equals(current.PasswordSalt, verifiedSnapshot.PasswordSalt, StringComparison.Ordinal)
            && current.PasswordHashVersion == verifiedSnapshot.PasswordHashVersion
            && current.PasswordChangedAt == verifiedSnapshot.PasswordChangedAt;
    }

    /// <summary>
    /// TR: Güncel credential security state'ini mevcut SQL transaction içinde kalıcılaştırır.
    /// EN: Persists current credential security state inside the existing SQL transaction.
    /// </summary>
    /// <param name="connection">TR: Açık SQL connection. EN: Open SQL connection.</param>
    /// <param name="transaction">TR: Aktif SQL transaction. EN: Active SQL transaction.</param>
    /// <param name="credential">TR: Kalıcılaştırılacak credential domain state'i. EN: Credential domain state to persist.</param>
    /// <param name="cancellationToken">TR: SQL update iptal sinyali. EN: Cancellation signal for the SQL update.</param>
    private static async Task UpdateCredentialStateAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        CustomerCredential credential,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE dbo.CustomerCredentials
            SET FailedLoginCount = @FailedLoginCount,
                LockedUntil = @LockedUntil,
                PasswordHash = @PasswordHash,
                PasswordSalt = @PasswordSalt,
                PasswordHashVersion = @PasswordHashVersion,
                PasswordChangedAt = @PasswordChangedAt
            WHERE CustomerId = @CustomerId;
            """;

        await using var command = new SqlCommand(sql, connection, transaction);
        AddCredentialParameters(command, credential);
        EnsureSingleRow(await command.ExecuteNonQueryAsync(cancellationToken), "Credential update");
    }

    /// <summary>
    /// TR: Yeni session kaydını mevcut SQL transaction içinde oluşturur.
    /// EN: Inserts a new session record inside the existing SQL transaction.
    /// </summary>
    /// <param name="connection">TR: Açık SQL connection. EN: Open SQL connection.</param>
    /// <param name="transaction">TR: Aktif SQL transaction. EN: Active SQL transaction.</param>
    /// <param name="session">TR: Eklenecek session domain nesnesi. EN: Session domain object to insert.</param>
    /// <param name="cancellationToken">TR: SQL komutu iptal sinyali. EN: Cancellation signal for the SQL command.</param>
    private static async Task InsertSessionAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        CustomerSession session,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO dbo.CustomerSessions
                (Id, CustomerId, DeviceId, CreatedAt, LastActivityAt, ExpiresAt, RevokedAt)
            VALUES
                (@Id, @CustomerId, @DeviceId, @CreatedAt, @LastActivityAt, @ExpiresAt, @RevokedAt);
            """;

        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = session.Id;
        command.Parameters.Add("@CustomerId", SqlDbType.UniqueIdentifier).Value = session.CustomerId;
        command.Parameters.Add("@DeviceId", SqlDbType.NVarChar, 128).Value = session.DeviceId;
        command.Parameters.Add("@CreatedAt", SqlDbType.DateTimeOffset).Value = session.CreatedAt;
        command.Parameters.Add("@LastActivityAt", SqlDbType.DateTimeOffset).Value = session.LastActivityAt;
        command.Parameters.Add("@ExpiresAt", SqlDbType.DateTimeOffset).Value = session.ExpiresAt;
        command.Parameters.Add("@RevokedAt", SqlDbType.DateTimeOffset).Value = (object?)session.RevokedAt ?? DBNull.Value;
        EnsureSingleRow(await command.ExecuteNonQueryAsync(cancellationToken), "Session insert");
    }

    /// <summary>
    /// TR: Raw token yerine yalnızca token hash ve lifecycle state'ini mevcut SQL transaction içinde RefreshTokens tablosuna ekler.
    /// EN: Inserts only token hash and lifecycle state, never the raw token, into RefreshTokens inside the existing SQL transaction.
    /// </summary>
    /// <param name="connection">TR: Açık SQL connection. EN: Open SQL connection.</param>
    /// <param name="transaction">TR: Aktif SQL transaction. EN: Active SQL transaction.</param>
    /// <param name="refreshToken">TR: Eklenecek refresh-token domain nesnesi. EN: Refresh-token domain object to insert.</param>
    /// <param name="cancellationToken">TR: SQL komutu iptal sinyali. EN: Cancellation signal for the SQL command.</param>
    private static async Task InsertRefreshTokenAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        RefreshToken refreshToken,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO dbo.RefreshTokens
                (Id, SessionId, TokenHash, CreatedAt, ExpiresAt, ConsumedAt, RevokedAt, ReplacedByTokenId)
            VALUES
                (@Id, @SessionId, @TokenHash, @CreatedAt, @ExpiresAt, @ConsumedAt, @RevokedAt, @ReplacedByTokenId);
            """;

        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = refreshToken.Id;
        command.Parameters.Add("@SessionId", SqlDbType.UniqueIdentifier).Value = refreshToken.SessionId;
        command.Parameters.Add("@TokenHash", SqlDbType.Char, 64).Value = refreshToken.TokenHash;
        command.Parameters.Add("@CreatedAt", SqlDbType.DateTimeOffset).Value = refreshToken.CreatedAt;
        command.Parameters.Add("@ExpiresAt", SqlDbType.DateTimeOffset).Value = refreshToken.ExpiresAt;
        command.Parameters.Add("@ConsumedAt", SqlDbType.DateTimeOffset).Value = (object?)refreshToken.ConsumedAt ?? DBNull.Value;
        command.Parameters.Add("@RevokedAt", SqlDbType.DateTimeOffset).Value = (object?)refreshToken.RevokedAt ?? DBNull.Value;
        command.Parameters.Add("@ReplacedByTokenId", SqlDbType.UniqueIdentifier).Value = (object?)refreshToken.ReplacedByTokenId ?? DBNull.Value;
        EnsureSingleRow(await command.ExecuteNonQueryAsync(cancellationToken), "Refresh-token insert");
    }

    /// <summary>
    /// TR: Credential alanlarını SQL komutuna açık tiplerle parametre olarak ekler ve hassas değerlerin SQL metnine karışmasını engeller.
    /// EN: Adds credential fields to a SQL command as explicitly typed parameters and prevents sensitive values from entering SQL text.
    /// </summary>
    /// <param name="command">TR: Parametre eklenecek SQL komutu. EN: SQL command receiving parameters.</param>
    /// <param name="credential">TR: Parametre değerlerini sağlayan credential domain nesnesi. EN: Credential domain object supplying parameter values.</param>
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

    /// <summary>
    /// TR: Tek satır değiştirmesi gereken persistence komutlarının sonuçlarını doğrular ve sessiz kayıp/güncelleme hatalarını engeller.
    /// EN: Validates persistence commands that must affect exactly one row and prevents silent lost/missing updates.
    /// </summary>
    /// <param name="affectedRows">TR: SQL komutunun etkilediği satır sayısı. EN: Number of rows affected by the SQL command.</param>
    /// <param name="operation">TR: Hata mesajında kullanılacak teknik operasyon adı. EN: Technical operation name used in the error message.</param>
    private static void EnsureSingleRow(int affectedRows, string operation)
    {
        if (affectedRows != 1)
        {
            throw new InvalidOperationException($"{operation} did not affect exactly one row.");
        }
    }
}
