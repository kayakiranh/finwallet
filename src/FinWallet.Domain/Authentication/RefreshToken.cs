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
    /// <param name="id">TR: Refresh token kaydının benzersiz kimliği. EN: Unique identifier of the refresh-token record.</param>
    /// <param name="sessionId">TR: Token'ın bağlı olduğu session kimliği. EN: Session identifier to which the token belongs.</param>
    /// <param name="tokenHash">TR: Ham token yerine saklanan tek yönlü hash. EN: One-way hash persisted instead of the raw token.</param>
    /// <param name="createdAt">TR: Token oluşturulma UTC zamanı. EN: UTC creation time of the token.</param>
    /// <param name="expiresAt">TR: Token sona erme UTC zamanı. EN: UTC expiration time of the token.</param>
    /// <returns>TR: Yeni kullanılabilir refresh token kaydını döndürür. EN: Returns the new usable refresh-token record.</returns>
    public static RefreshToken Create(
        Guid id,
        Guid sessionId,
        string tokenHash,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt)
    {
        ValidateCore(id, sessionId, tokenHash, createdAt, expiresAt, null, null, null);

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
    /// TR: MSSQL kaydındaki refresh token lifecycle state'ini domain nesnesine güvenli biçimde yeniden yükler; raw token hiçbir zaman bu akışa girmez.
    /// EN: Safely rehydrates refresh-token lifecycle state from an MSSQL record into the domain object; the raw token never participates in this flow.
    /// </summary>
    /// <param name="id">TR: Kalıcı refresh token kimliği. EN: Persisted refresh-token identifier.</param>
    /// <param name="sessionId">TR: Kalıcı session kimliği. EN: Persisted session identifier.</param>
    /// <param name="tokenHash">TR: Kalıcı SHA-256 token hash'i. EN: Persisted SHA-256 token hash.</param>
    /// <param name="createdAt">TR: Kalıcı oluşturulma UTC zamanı. EN: Persisted UTC creation time.</param>
    /// <param name="expiresAt">TR: Kalıcı sona erme UTC zamanı. EN: Persisted UTC expiration time.</param>
    /// <param name="consumedAt">TR: Kalıcı consume UTC zamanı; kullanılmadıysa null. EN: Persisted UTC consumption time, or null when unused.</param>
    /// <param name="revokedAt">TR: Kalıcı revoke UTC zamanı; revoke edilmediyse null. EN: Persisted UTC revocation time, or null when not revoked.</param>
    /// <param name="replacedByTokenId">TR: Rotation replacement token kimliği; yoksa null. EN: Replacement token identifier created by rotation, or null when absent.</param>
    /// <returns>TR: Kalıcı lifecycle state'ini taşıyan refresh token nesnesini döndürür. EN: Returns a refresh-token object carrying persisted lifecycle state.</returns>
    public static RefreshToken Restore(
        Guid id,
        Guid sessionId,
        string tokenHash,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt,
        DateTimeOffset? consumedAt,
        DateTimeOffset? revokedAt,
        Guid? replacedByTokenId)
    {
        ValidateCore(id, sessionId, tokenHash, createdAt, expiresAt, consumedAt, revokedAt, replacedByTokenId);

        return new RefreshToken
        {
            Id = id,
            SessionId = sessionId,
            TokenHash = tokenHash,
            CreatedAt = createdAt,
            ExpiresAt = expiresAt,
            ConsumedAt = consumedAt,
            RevokedAt = revokedAt,
            ReplacedByTokenId = replacedByTokenId
        };
    }

    /// <summary>TR: Refresh token kaydının benzersiz kimliğini döndürür. EN: Gets the unique refresh-token identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>TR: Token'ın bağlı olduğu session kimliğini döndürür. EN: Gets the session identifier associated with the token.</summary>
    public Guid SessionId { get; private set; }

    /// <summary>TR: Ham token yerine saklanan tek yönlü hash değerini döndürür; loglanmamalıdır. EN: Gets the one-way hash persisted instead of the raw token; it must not be logged.</summary>
    public string TokenHash { get; private set; }

    /// <summary>TR: Token oluşturulma UTC zamanını döndürür. EN: Gets the token UTC creation time.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>TR: Token mutlak sona erme UTC zamanını döndürür. EN: Gets the token absolute UTC expiration time.</summary>
    public DateTimeOffset ExpiresAt { get; private set; }

    /// <summary>TR: Token consume UTC zamanını; kullanılmadıysa null değerini döndürür. EN: Gets the token UTC consumption time, or null when unused.</summary>
    public DateTimeOffset? ConsumedAt { get; private set; }

    /// <summary>TR: Token revoke UTC zamanını; revoke edilmediyse null değerini döndürür. EN: Gets the token UTC revocation time, or null when not revoked.</summary>
    public DateTimeOffset? RevokedAt { get; private set; }

    /// <summary>TR: Rotation replacement token kimliğini; yoksa null değerini döndürür. EN: Gets the replacement token identifier created during rotation, or null when absent.</summary>
    public Guid? ReplacedByTokenId { get; private set; }

    /// <summary>
    /// TR: Refresh token'ın verilen zamanda ilk kez kullanılabilir olup olmadığını belirler.
    /// EN: Determines whether the refresh token is usable for the first time at the supplied time.
    /// </summary>
    /// <param name="now">TR: Geçerliliğin değerlendirileceği UTC zaman bilgisi. EN: UTC timestamp at which validity is evaluated.</param>
    /// <returns>TR: Token süresi dolmamış, revoke edilmemiş ve consume edilmemişse true döndürür. EN: Returns true when the token is unexpired, not revoked and not consumed.</returns>
    public bool IsUsable(DateTimeOffset now)
    {
        return ExpiresAt > now && RevokedAt is null && ConsumedAt is null;
    }

    /// <summary>
    /// TR: Başarılı rotation sırasında mevcut refresh token'ı tek kullanımlık olarak tüketir ve replacement token kimliğini kaydeder.
    /// EN: Consumes the current refresh token as single-use during successful rotation and records the replacement token identifier.
    /// </summary>
    /// <param name="consumedAt">TR: Token consume UTC zamanı. EN: UTC timestamp at which the token was consumed.</param>
    /// <param name="replacementTokenId">TR: Yeni replacement token kimliği. EN: New replacement token identifier.</param>
    /// <exception cref="InvalidOperationException">TR: Token daha önce tüketilmiş veya revoke edilmişse oluşur. EN: Thrown when the token has already been consumed or revoked.</exception>
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

        if (consumedAt < CreatedAt || consumedAt > ExpiresAt)
        {
            throw new ArgumentException("Consumption time must be within the token lifetime.", nameof(consumedAt));
        }

        ConsumedAt = consumedAt;
        ReplacedByTokenId = replacementTokenId;
    }

    /// <summary>
    /// TR: Refresh token kaydını belirtilen zamanda revoke eder ve tekrar kullanılmasını engeller.
    /// EN: Revokes the refresh-token record at the supplied time and prevents future use.
    /// </summary>
    /// <param name="revokedAt">TR: Revoke işleminin UTC zamanı. EN: UTC timestamp at which revocation occurred.</param>
    public void Revoke(DateTimeOffset revokedAt)
    {
        if (revokedAt < CreatedAt)
        {
            throw new ArgumentException("Revocation time cannot be before token creation.", nameof(revokedAt));
        }

        RevokedAt ??= revokedAt;
    }

    /// <summary>
    /// TR: Token'ın daha önce tüketildiği halde tekrar sunulmasının reuse göstergesi olup olmadığını belirler.
    /// EN: Determines whether presenting a previously consumed token indicates reuse.
    /// </summary>
    /// <returns>TR: Token daha önce tüketilmişse true döndürür. EN: Returns true when the token has previously been consumed.</returns>
    public bool IndicatesReuse()
    {
        return ConsumedAt is not null;
    }

    /// <summary>
    /// TR: Create/Restore akışlarında refresh token kimlik, hash ve lifecycle state tutarlılığını doğrular.
    /// EN: Validates refresh-token identity, hash and lifecycle-state consistency for Create/Restore flows.
    /// </summary>
    /// <param name="id">TR: Token kimliği. EN: Token identifier.</param>
    /// <param name="sessionId">TR: Session kimliği. EN: Session identifier.</param>
    /// <param name="tokenHash">TR: Token hash'i. EN: Token hash.</param>
    /// <param name="createdAt">TR: Oluşturulma UTC zamanı. EN: UTC creation time.</param>
    /// <param name="expiresAt">TR: Sona erme UTC zamanı. EN: UTC expiration time.</param>
    /// <param name="consumedAt">TR: İsteğe bağlı consume UTC zamanı. EN: Optional UTC consumption time.</param>
    /// <param name="revokedAt">TR: İsteğe bağlı revoke UTC zamanı. EN: Optional UTC revocation time.</param>
    /// <param name="replacedByTokenId">TR: İsteğe bağlı replacement token kimliği. EN: Optional replacement-token identifier.</param>
    private static void ValidateCore(
        Guid id,
        Guid sessionId,
        string tokenHash,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt,
        DateTimeOffset? consumedAt,
        DateTimeOffset? revokedAt,
        Guid? replacedByTokenId)
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

        if (consumedAt.HasValue && (consumedAt.Value < createdAt || consumedAt.Value > expiresAt))
        {
            throw new ArgumentException("Persisted consumption time is outside the token lifetime.", nameof(consumedAt));
        }

        if (revokedAt.HasValue && revokedAt.Value < createdAt)
        {
            throw new ArgumentException("Persisted revocation time cannot be before token creation.", nameof(revokedAt));
        }

        if (replacedByTokenId.HasValue && replacedByTokenId.Value == Guid.Empty)
        {
            throw new ArgumentException("Replacement token identifier cannot be empty.", nameof(replacedByTokenId));
        }

        if (consumedAt.HasValue != replacedByTokenId.HasValue)
        {
            throw new ArgumentException("Consumed and replacement-token state must be present together.");
        }
    }
}
