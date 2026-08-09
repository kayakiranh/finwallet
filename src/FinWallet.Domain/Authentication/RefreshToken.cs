namespace FinWallet.Domain.Authentication;

/// <summary>
/// TR: Ham refresh token'ı saklamadan yalnızca token hash'i üzerinden rotation, consume, revoke ve reuse detection için gereken kalıcı güvenlik durumunu temsil eder.
/// EN: Represents the persistent security state required for rotation, consumption, revocation and reuse detection using only a token hash without storing the raw refresh token.
/// </summary>
public sealed class RefreshToken
{
    /// <summary>
    /// TR: Kalıcılık katmanının refresh token nesnesini yeniden oluşturması için ayrılmış kurucudur.
    /// EN: Constructor reserved for persistence materialization of the refresh-token object.
    /// </summary>
    private RefreshToken()
    {
        TokenHash = string.Empty;
    }

    /// <summary>
    /// TR: Belirli bir müşteri oturumuna bağlı yeni refresh token kaydını oluşturur.
    /// EN: Creates a new refresh-token record associated with a specific customer session.
    /// </summary>
    /// <param name="id">
    /// TR: Refresh token kaydının benzersiz kimliği.
    /// EN: Unique identifier of the refresh-token record.
    /// </param>
    /// <param name="sessionId">
    /// TR: Refresh token'ın bağlı olduğu müşteri oturumunun kimliği.
    /// EN: Identifier of the customer session to which the refresh token belongs.
    /// </param>
    /// <param name="tokenHash">
    /// TR: Ham token yerine kalıcı olarak saklanan tek yönlü token hash değeri.
    /// EN: One-way token hash persisted instead of the raw token.
    /// </param>
    /// <param name="createdAt">
    /// TR: Refresh token'ın oluşturulduğu UTC zaman bilgisi.
    /// EN: UTC timestamp at which the refresh token was created.
    /// </param>
    /// <param name="expiresAt">
    /// TR: Refresh token'ın artık kabul edilmeyeceği UTC zaman bilgisi.
    /// EN: UTC timestamp after which the refresh token must no longer be accepted.
    /// </param>
    /// <returns>
    /// TR: Yeni kullanılabilir refresh token kaydını döndürür.
    /// EN: Returns the newly created usable refresh-token record.
    /// </returns>
    public static RefreshToken Create(
        Guid id,
        Guid sessionId,
        string tokenHash,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Refresh-token identifier cannot be empty.", nameof(id));
        }

        if (sessionId == Guid.Empty)
        {
            throw new ArgumentException("Session identifier cannot be empty.", nameof(sessionId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);

        if (expiresAt <= createdAt)
        {
            throw new ArgumentException("Refresh-token expiration must be after creation time.", nameof(expiresAt));
        }

        return new RefreshToken
        {
            Id = id,
            SessionId = sessionId,
            TokenHash = tokenHash,
            CreatedAt = createdAt,
            ExpiresAt = expiresAt
        };
    }

    /// <summary>
    /// TR: Refresh token kaydının benzersiz kimliğini döndürür.
    /// EN: Gets the unique identifier of the refresh-token record.
    /// </summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// TR: Refresh token'ın bağlı olduğu müşteri oturumu kimliğini döndürür.
    /// EN: Gets the customer-session identifier associated with the refresh token.
    /// </summary>
    public Guid SessionId { get; private set; }

    /// <summary>
    /// TR: Ham refresh token yerine saklanan tek yönlü hash değerini döndürür; bu değer de hassas güvenlik verisi olarak loglanmamalıdır.
    /// EN: Gets the one-way hash persisted instead of the raw refresh token; this value is also sensitive security data and must not be logged.
    /// </summary>
    public string TokenHash { get; private set; }

    /// <summary>
    /// TR: Refresh token'ın oluşturulduğu UTC zamanını döndürür.
    /// EN: Gets the UTC time at which the refresh token was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// TR: Refresh token'ın mutlak sona erme UTC zamanını döndürür.
    /// EN: Gets the absolute UTC expiration time of the refresh token.
    /// </summary>
    public DateTimeOffset ExpiresAt { get; private set; }

    /// <summary>
    /// TR: Token'ın başarılı refresh işleminde tüketildiği UTC zamanını döndürür; null ise henüz tüketilmemiştir.
    /// EN: Gets the UTC time at which the token was consumed by a successful refresh; null means it has not yet been consumed.
    /// </summary>
    public DateTimeOffset? ConsumedAt { get; private set; }

    /// <summary>
    /// TR: Token'ın güvenlik veya logout nedeniyle revoke edildiği UTC zamanını döndürür; null ise revoke edilmemiştir.
    /// EN: Gets the UTC time at which the token was revoked for security or logout; null means it has not been revoked.
    /// </summary>
    public DateTimeOffset? RevokedAt { get; private set; }

    /// <summary>
    /// TR: Rotation sırasında bu token'ın yerine oluşturulan yeni refresh token kaydının kimliğini döndürür.
    /// EN: Gets the identifier of the replacement refresh-token record created during rotation.
    /// </summary>
    public Guid? ReplacedByTokenId { get; private set; }

    /// <summary>
    /// TR: Refresh token'ın verilen zamanda ilk kez kullanılabilir olup olmadığını belirler.
    /// EN: Determines whether the refresh token is usable for the first time at the supplied time.
    /// </summary>
    /// <param name="now">
    /// TR: Token geçerliliğinin değerlendirileceği UTC zaman bilgisi.
    /// EN: UTC timestamp at which to evaluate token validity.
    /// </param>
    /// <returns>
    /// TR: Token süresi dolmamış, revoke edilmemiş ve daha önce tüketilmemişse true döndürür.
    /// EN: Returns true when the token is unexpired, not revoked and has not previously been consumed.
    /// </returns>
    public bool IsUsable(DateTimeOffset now)
    {
        return ExpiresAt > now && RevokedAt is null && ConsumedAt is null;
    }

    /// <summary>
    /// TR: Başarılı rotation sırasında mevcut refresh token'ı tek kullanımlık olarak tüketir ve yerine geçen token kimliğini kaydeder.
    /// EN: Consumes the current refresh token as single-use during successful rotation and records the replacement token identifier.
    /// </summary>
    /// <param name="consumedAt">
    /// TR: Token'ın tüketildiği UTC zaman bilgisi.
    /// EN: UTC timestamp at which the token was consumed.
    /// </param>
    /// <param name="replacementTokenId">
    /// TR: Rotation sonucunda oluşturulan yeni refresh token kaydının kimliği.
    /// EN: Identifier of the new refresh-token record created by rotation.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// TR: Token daha önce tüketilmiş veya revoke edilmişse oluşur.
    /// EN: Thrown when the token has already been consumed or revoked.
    /// </exception>
    public void Consume(DateTimeOffset consumedAt, Guid replacementTokenId)
    {
        if (replacementTokenId == Guid.Empty)
        {
            throw new ArgumentException("Replacement token identifier cannot be empty.", nameof(replacementTokenId));
        }

        if (ConsumedAt is not null || RevokedAt is not null)
        {
            throw new InvalidOperationException("Refresh token has already been consumed or revoked.");
        }

        ConsumedAt = consumedAt;
        ReplacedByTokenId = replacementTokenId;
    }

    /// <summary>
    /// TR: Refresh token kaydını belirtilen zamanda revoke eder ve tekrar kullanılmasını engeller.
    /// EN: Revokes the refresh-token record at the supplied time and prevents future use.
    /// </summary>
    /// <param name="revokedAt">
    /// TR: Revoke işleminin gerçekleştiği UTC zaman bilgisi.
    /// EN: UTC timestamp at which revocation occurred.
    /// </param>
    public void Revoke(DateTimeOffset revokedAt)
    {
        RevokedAt ??= revokedAt;
    }

    /// <summary>
    /// TR: Token'ın daha önce tüketildiği halde tekrar sunulmasının reuse saldırısı göstergesi olup olmadığını belirler.
    /// EN: Determines whether presenting a token that has already been consumed represents a refresh-token reuse signal.
    /// </summary>
    /// <returns>
    /// TR: Token daha önce tüketilmişse true döndürür.
    /// EN: Returns true when the token has previously been consumed.
    /// </returns>
    public bool IndicatesReuse()
    {
        return ConsumedAt is not null;
    }
}
