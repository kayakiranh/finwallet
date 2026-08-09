namespace FinWallet.Domain.Authentication;

/// <summary>
/// TR: Bir müşterinin belirli cihaz/istemci üzerindeki kimlik doğrulama oturumunu temsil eder; JWT access token'dan bağımsız revoke ve cihaz bazlı oturum yönetimi sağlar.
/// EN: Represents a customer's authenticated session on a specific device/client and enables revocation and device-level session management independently of JWT access tokens.
/// </summary>
public sealed class CustomerSession
{
    /// <summary>
    /// TR: Cihaz/uygulama örneği kimliğinin abuse veya gereksiz storage büyümesi oluşturmaması için kabul edilen maksimum karakter sayısını tanımlar.
    /// EN: Defines the maximum accepted device/application-instance identifier length to prevent abuse or unnecessary storage growth.
    /// </summary>
    private const int MaximumDeviceIdLength = 128;

    /// <summary>
    /// TR: Kalıcılık katmanının session nesnesini yeniden oluşturması için ayrılmış kurucudur.
    /// EN: Constructor reserved for persistence materialization of the session object.
    /// </summary>
    private CustomerSession()
    {
        DeviceId = string.Empty;
    }

    /// <summary>
    /// TR: Yeni aktif müşteri oturumunu oluşturur.
    /// EN: Creates a new active customer session.
    /// </summary>
    /// <param name="id">TR: Oturumun benzersiz kimliği. EN: Unique identifier of the session.</param>
    /// <param name="customerId">TR: Oturumun bağlı olduğu müşteri kimliği. EN: Customer identifier to which the session belongs.</param>
    /// <param name="deviceId">TR: Normalize cihaz/uygulama örneği kimliği. EN: Normalized device/application-instance identifier.</param>
    /// <param name="createdAt">TR: Oturumun oluşturulduğu UTC zaman bilgisi. EN: UTC timestamp at which the session was created.</param>
    /// <param name="expiresAt">TR: Oturumun sona ereceği UTC zaman bilgisi. EN: UTC timestamp at which the session expires.</param>
    /// <returns>TR: Yeni aktif müşteri oturumunu döndürür. EN: Returns the new active customer session.</returns>
    public static CustomerSession Create(
        Guid id,
        Guid customerId,
        string deviceId,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt)
    {
        var normalizedDeviceId = ValidateCore(id, customerId, deviceId, createdAt, createdAt, expiresAt, null);

        return new CustomerSession
        {
            Id = id,
            CustomerId = customerId,
            DeviceId = normalizedDeviceId,
            CreatedAt = createdAt,
            LastActivityAt = createdAt,
            ExpiresAt = expiresAt
        };
    }

    /// <summary>
    /// TR: MSSQL kaydındaki session lifecycle state'ini domain nesnesine güvenli biçimde yeniden yükler; yeni session üretim akışında kullanılmamalıdır.
    /// EN: Safely rehydrates session lifecycle state from an MSSQL record into the domain object; it must not be used for new-session creation flows.
    /// </summary>
    /// <param name="id">TR: Kalıcı session kimliği. EN: Persisted session identifier.</param>
    /// <param name="customerId">TR: Kalıcı müşteri kimliği. EN: Persisted customer identifier.</param>
    /// <param name="deviceId">TR: Kalıcı normalize cihaz kimliği. EN: Persisted normalized device identifier.</param>
    /// <param name="createdAt">TR: Kalıcı oluşturulma UTC zamanı. EN: Persisted UTC creation time.</param>
    /// <param name="lastActivityAt">TR: Kalıcı son aktivite UTC zamanı. EN: Persisted UTC last-activity time.</param>
    /// <param name="expiresAt">TR: Kalıcı session sona erme UTC zamanı. EN: Persisted UTC session expiration.</param>
    /// <param name="revokedAt">TR: Kalıcı revoke UTC zamanı; revoke yoksa null. EN: Persisted UTC revocation time, or null when not revoked.</param>
    /// <returns>TR: Kalıcı state'i taşıyan session domain nesnesini döndürür. EN: Returns a session domain object carrying persisted state.</returns>
    public static CustomerSession Restore(
        Guid id,
        Guid customerId,
        string deviceId,
        DateTimeOffset createdAt,
        DateTimeOffset lastActivityAt,
        DateTimeOffset expiresAt,
        DateTimeOffset? revokedAt)
    {
        var normalizedDeviceId = ValidateCore(id, customerId, deviceId, createdAt, lastActivityAt, expiresAt, revokedAt);

        return new CustomerSession
        {
            Id = id,
            CustomerId = customerId,
            DeviceId = normalizedDeviceId,
            CreatedAt = createdAt,
            LastActivityAt = lastActivityAt,
            ExpiresAt = expiresAt,
            RevokedAt = revokedAt
        };
    }

    /// <summary>TR: Oturumun benzersiz kimliğini döndürür. EN: Gets the unique session identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>TR: Oturumun bağlı olduğu müşteri kimliğini döndürür. EN: Gets the customer identifier associated with the session.</summary>
    public Guid CustomerId { get; private set; }

    /// <summary>TR: Normalize cihaz veya uygulama örneği kimliğini döndürür. EN: Gets the normalized device or application-instance identifier.</summary>
    public string DeviceId { get; private set; }

    /// <summary>TR: Oturumun oluşturulduğu UTC zamanı döndürür. EN: Gets the UTC creation time of the session.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>TR: Son başarılı authentication/refresh aktivite UTC zamanını döndürür. EN: Gets the UTC time of the latest successful authentication/refresh activity.</summary>
    public DateTimeOffset LastActivityAt { get; private set; }

    /// <summary>TR: Oturumun mutlak sona erme UTC zamanını döndürür. EN: Gets the absolute UTC expiration time of the session.</summary>
    public DateTimeOffset ExpiresAt { get; private set; }

    /// <summary>TR: Oturumun revoke edildiği UTC zamanı; revoke edilmediyse null döndürür. EN: Gets the UTC revocation time, or null when the session has not been revoked.</summary>
    public DateTimeOffset? RevokedAt { get; private set; }

    /// <summary>
    /// TR: Oturumun verilen zamanda token üretimi/refresh için kullanılabilir olup olmadığını belirler.
    /// EN: Determines whether the session can be used for token issuance/refresh at the supplied time.
    /// </summary>
    /// <param name="now">TR: Geçerliliğin değerlendirileceği UTC zaman bilgisi. EN: UTC timestamp at which validity is evaluated.</param>
    /// <returns>TR: Oturum revoke edilmemiş ve süresi dolmamışsa true döndürür. EN: Returns true when the session is not revoked and has not expired.</returns>
    public bool IsActive(DateTimeOffset now)
    {
        return RevokedAt is null && ExpiresAt > now;
    }

    /// <summary>
    /// TR: Başarılı login veya refresh sonrasında oturumun son aktivite zamanını günceller.
    /// EN: Updates the session's last-activity time after a successful login or refresh.
    /// </summary>
    /// <param name="activityAt">TR: Başarılı aktivitenin UTC zamanı. EN: UTC timestamp of the successful activity.</param>
    /// <exception cref="InvalidOperationException">TR: Oturum revoke edilmişse oluşur. EN: Thrown when the session has been revoked.</exception>
    public void Touch(DateTimeOffset activityAt)
    {
        if (RevokedAt is not null)
        {
            throw new InvalidOperationException("Revoked session cannot be touched.");
        }

        if (activityAt < LastActivityAt || activityAt > ExpiresAt)
        {
            throw new ArgumentException("Activity time must be monotonic and cannot exceed session expiration.", nameof(activityAt));
        }

        LastActivityAt = activityAt;
    }

    /// <summary>
    /// TR: Oturumu belirtilen zamanda kalıcı olarak revoke eder ve yeni refresh işlemlerini engeller.
    /// EN: Permanently revokes the session at the supplied time and prevents new refresh operations.
    /// </summary>
    /// <param name="revokedAt">TR: Revoke işleminin UTC zamanı. EN: UTC timestamp of revocation.</param>
    public void Revoke(DateTimeOffset revokedAt)
    {
        if (revokedAt < CreatedAt)
        {
            throw new ArgumentException("Revocation time cannot be before session creation.", nameof(revokedAt));
        }

        RevokedAt ??= revokedAt;
    }

    /// <summary>
    /// TR: Create ve Restore akışlarında session kimliği, cihaz uzunluğu ve lifecycle zamanlarının tutarlılığını doğrular.
    /// EN: Validates session identifiers, device length and lifecycle timestamp consistency for both Create and Restore flows.
    /// </summary>
    /// <param name="id">TR: Doğrulanacak session kimliği. EN: Session identifier to validate.</param>
    /// <param name="customerId">TR: Doğrulanacak müşteri kimliği. EN: Customer identifier to validate.</param>
    /// <param name="deviceId">TR: Doğrulanacak cihaz kimliği. EN: Device identifier to validate.</param>
    /// <param name="createdAt">TR: Session oluşturulma UTC zamanı. EN: Session UTC creation time.</param>
    /// <param name="lastActivityAt">TR: Son aktivite UTC zamanı. EN: Last-activity UTC time.</param>
    /// <param name="expiresAt">TR: Session sona erme UTC zamanı. EN: Session UTC expiration time.</param>
    /// <param name="revokedAt">TR: İsteğe bağlı revoke UTC zamanı. EN: Optional UTC revocation time.</param>
    /// <returns>TR: Trim edilmiş ve doğrulanmış cihaz kimliğini döndürür. EN: Returns the trimmed and validated device identifier.</returns>
    private static string ValidateCore(
        Guid id,
        Guid customerId,
        string deviceId,
        DateTimeOffset createdAt,
        DateTimeOffset lastActivityAt,
        DateTimeOffset expiresAt,
        DateTimeOffset? revokedAt)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Session identifier cannot be empty.", nameof(id));
        }

        if (customerId == Guid.Empty)
        {
            throw new ArgumentException("Customer identifier cannot be empty.", nameof(customerId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);
        var normalizedDeviceId = deviceId.Trim();
        if (normalizedDeviceId.Length > MaximumDeviceIdLength)
        {
            throw new ArgumentException($"Device identifier cannot exceed {MaximumDeviceIdLength} characters.", nameof(deviceId));
        }

        if (expiresAt <= createdAt || lastActivityAt < createdAt || lastActivityAt > expiresAt)
        {
            throw new ArgumentException("Session lifecycle timestamps are inconsistent.");
        }

        if (revokedAt.HasValue && revokedAt.Value < createdAt)
        {
            throw new ArgumentException("Session revocation cannot be before creation.", nameof(revokedAt));
        }

        return normalizedDeviceId;
    }
}
