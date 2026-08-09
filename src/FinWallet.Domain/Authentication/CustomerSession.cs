namespace FinWallet.Domain.Authentication;

/// <summary>
/// TR: Bir müşterinin belirli cihaz/istemci üzerindeki kimlik doğrulama oturumunu temsil eder; JWT access token'dan bağımsız olarak revoke ve cihaz bazlı oturum yönetimi sağlar.
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
    /// <param name="id">
    /// TR: Oturumun benzersiz kimliği.
    /// EN: Unique identifier of the session.
    /// </param>
    /// <param name="customerId">
    /// TR: Oturumun bağlı olduğu müşteri kimliği.
    /// EN: Customer identifier to which the session belongs.
    /// </param>
    /// <param name="deviceId">
    /// TR: İstemci tarafından üretilen veya normalize edilen cihaz/uygulama örneği kimliği; boş olamaz ve 128 karakteri aşamaz.
    /// EN: Device/application-instance identifier generated or normalized by the client; it cannot be empty or exceed 128 characters.
    /// </param>
    /// <param name="createdAt">
    /// TR: Oturumun oluşturulduğu UTC zaman bilgisi.
    /// EN: UTC timestamp at which the session was created.
    /// </param>
    /// <param name="expiresAt">
    /// TR: Oturumun yeni token üretimi için artık kullanılamayacağı UTC zaman bilgisi.
    /// EN: UTC timestamp after which the session can no longer be used to issue new tokens.
    /// </param>
    /// <returns>
    /// TR: Yeni aktif müşteri oturumunu döndürür.
    /// EN: Returns the new active customer session.
    /// </returns>
    public static CustomerSession Create(
        Guid id,
        Guid customerId,
        string deviceId,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt)
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

        if (expiresAt <= createdAt)
        {
            throw new ArgumentException("Session expiration must be after creation time.", nameof(expiresAt));
        }

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
    /// TR: Oturumun benzersiz kimliğini döndürür ve JWT içindeki SessionId claim'i ile ilişkilendirilebilir.
    /// EN: Gets the unique session identifier and may be correlated with the SessionId claim in JWT access tokens.
    /// </summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// TR: Oturumun bağlı olduğu müşteri kimliğini döndürür.
    /// EN: Gets the customer identifier to which this session belongs.
    /// </summary>
    public Guid CustomerId { get; private set; }

    /// <summary>
    /// TR: Oturumun bağlı olduğu normalize cihaz veya uygulama örneği kimliğini döndürür.
    /// EN: Gets the normalized device or application-instance identifier associated with the session.
    /// </summary>
    public string DeviceId { get; private set; }

    /// <summary>
    /// TR: Oturumun oluşturulduğu UTC zamanını döndürür.
    /// EN: Gets the UTC time at which the session was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// TR: Oturumun en son başarılı kimlik doğrulama/refresh aktivitesinin UTC zamanını döndürür.
    /// EN: Gets the UTC time of the session's most recent successful authentication/refresh activity.
    /// </summary>
    public DateTimeOffset LastActivityAt { get; private set; }

    /// <summary>
    /// TR: Oturumun mutlak sona erme UTC zamanını döndürür.
    /// EN: Gets the absolute UTC expiration time of the session.
    /// </summary>
    public DateTimeOffset ExpiresAt { get; private set; }

    /// <summary>
    /// TR: Oturumun manuel, güvenlik veya token reuse nedeniyle revoke edildiği UTC zamanını döndürür; null ise revoke edilmemiştir.
    /// EN: Gets the UTC time at which the session was revoked manually, for security or due to token reuse; null means it has not been revoked.
    /// </summary>
    public DateTimeOffset? RevokedAt { get; private set; }

    /// <summary>
    /// TR: Oturumun verilen zamanda token üretimi/refresh için kullanılabilir olup olmadığını belirler.
    /// EN: Determines whether the session can be used for token issuance/refresh at the supplied time.
    /// </summary>
    /// <param name="now">
    /// TR: Oturum geçerliliğinin değerlendirileceği UTC zaman bilgisi.
    /// EN: UTC timestamp at which to evaluate session validity.
    /// </param>
    /// <returns>
    /// TR: Oturum revoke edilmemiş ve süresi dolmamışsa true döndürür.
    /// EN: Returns true when the session is not revoked and has not expired.
    /// </returns>
    public bool IsActive(DateTimeOffset now)
    {
        return RevokedAt is null && ExpiresAt > now;
    }

    /// <summary>
    /// TR: Başarılı login veya refresh sonrasında oturumun son aktivite zamanını günceller.
    /// EN: Updates the session's last-activity time after a successful login or refresh.
    /// </summary>
    /// <param name="activityAt">
    /// TR: Başarılı aktivitenin gerçekleştiği UTC zaman bilgisi.
    /// EN: UTC timestamp at which the successful activity occurred.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// TR: Oturum daha önce revoke edilmişse oluşur.
    /// EN: Thrown when the session has already been revoked.
    /// </exception>
    public void Touch(DateTimeOffset activityAt)
    {
        if (RevokedAt is not null)
        {
            throw new InvalidOperationException("Revoked session cannot be touched.");
        }

        if (activityAt < LastActivityAt)
        {
            throw new ArgumentException("Activity time cannot move backwards.", nameof(activityAt));
        }

        LastActivityAt = activityAt;
    }

    /// <summary>
    /// TR: Oturumu belirtilen zamanda kalıcı olarak revoke eder; aynı oturuma bağlı yeni refresh işlemleri artık kabul edilmemelidir.
    /// EN: Permanently revokes the session at the supplied time; new refresh operations associated with the session must no longer be accepted.
    /// </summary>
    /// <param name="revokedAt">
    /// TR: Revoke işleminin gerçekleştiği UTC zaman bilgisi.
    /// EN: UTC timestamp at which revocation occurred.
    /// </param>
    public void Revoke(DateTimeOffset revokedAt)
    {
        RevokedAt ??= revokedAt;
    }
}
