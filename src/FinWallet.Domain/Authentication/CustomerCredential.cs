namespace FinWallet.Domain.Authentication;

/// <summary>
/// TR: Müşterinin parola hash materyalini ve login kilitleme durumunu Customer tablosundan ayrı tutan güvenlik modelini temsil eder.
/// EN: Represents the security model that keeps password-hash material and login lockout state separate from the Customer table.
/// </summary>
public sealed class CustomerCredential
{
    /// <summary>
    /// TR: Art arda başarısız login denemelerinde geçici kilit uygulanmadan önce izin verilen maksimum deneme sayısını tanımlar.
    /// EN: Defines the maximum number of consecutive failed login attempts allowed before a temporary lock is applied.
    /// </summary>
    private const int MaximumFailedLoginAttempts = 5;

    /// <summary>
    /// TR: Başarısız login eşiği aşıldığında uygulanan sabit geçici kilit süresini tanımlar.
    /// EN: Defines the fixed temporary lock duration applied after the failed-login threshold is reached.
    /// </summary>
    private static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(15);

    /// <summary>
    /// TR: Kalıcılık katmanının credential nesnesini yeniden oluşturması için ayrılmış kurucudur.
    /// EN: Constructor reserved for persistence materialization of the credential object.
    /// </summary>
    private CustomerCredential()
    {
        PasswordHash = string.Empty;
        PasswordSalt = string.Empty;
    }

    /// <summary>
    /// TR: Müşteriye ait yeni credential kaydını güvenli hash materyaliyle oluşturur.
    /// EN: Creates a new credential record for a customer using secure password-hash material.
    /// </summary>
    /// <param name="customerId">TR: Credential kaydının bağlı olduğu müşteri kimliği. EN: Customer identifier to which the credential record belongs.</param>
    /// <param name="passwordHash">TR: Base64 biçiminde türetilmiş parola hash değeri. EN: Derived password hash encoded as Base64.</param>
    /// <param name="passwordSalt">TR: Base64 biçiminde benzersiz kriptografik salt değeri. EN: Unique cryptographic salt encoded as Base64.</param>
    /// <param name="passwordHashVersion">TR: Güvenli hash migration'larını destekleyen şema versiyonu. EN: Hash-scheme version supporting safe future migrations.</param>
    /// <param name="createdAt">TR: Credential kaydının oluşturulduğu UTC zaman bilgisi. EN: UTC timestamp at which the credential record was created.</param>
    /// <returns>TR: Yeni müşteri credential nesnesini döndürür. EN: Returns the newly created customer credential object.</returns>
    public static CustomerCredential Create(
        Guid customerId,
        string passwordHash,
        string passwordSalt,
        int passwordHashVersion,
        DateTimeOffset createdAt)
    {
        ValidateIdentityAndHash(customerId, passwordHash, passwordSalt, passwordHashVersion);

        return new CustomerCredential
        {
            CustomerId = customerId,
            PasswordHash = passwordHash,
            PasswordSalt = passwordSalt,
            PasswordHashVersion = passwordHashVersion,
            PasswordChangedAt = createdAt
        };
    }

    /// <summary>
    /// TR: MSSQL kaydındaki parola ve lockout state'ini domain nesnesine güvenli biçimde yeniden yükler; yeni credential oluşturma akışında kullanılmamalıdır.
    /// EN: Safely rehydrates password and lockout state from an MSSQL record into the domain object; it must not be used for new-credential creation flows.
    /// </summary>
    /// <param name="customerId">TR: Kalıcı müşteri kimliği. EN: Persisted customer identifier.</param>
    /// <param name="passwordHash">TR: Kalıcı Base64 parola hash'i. EN: Persisted Base64 password hash.</param>
    /// <param name="passwordSalt">TR: Kalıcı Base64 salt değeri. EN: Persisted Base64 salt value.</param>
    /// <param name="passwordHashVersion">TR: Kalıcı hash şeması versiyonu. EN: Persisted hash-scheme version.</param>
    /// <param name="failedLoginCount">TR: Kalıcı başarısız login sayacı. EN: Persisted failed-login counter.</param>
    /// <param name="lockedUntil">TR: Kalıcı geçici lock sona erme zamanı. EN: Persisted temporary-lock expiration.</param>
    /// <param name="passwordChangedAt">TR: Parola hash materyalinin son değiştiği UTC zaman. EN: UTC time at which password hash material last changed.</param>
    /// <returns>TR: Kalıcı güvenlik state'ini taşıyan credential nesnesini döndürür. EN: Returns a credential object carrying persisted security state.</returns>
    public static CustomerCredential Restore(
        Guid customerId,
        string passwordHash,
        string passwordSalt,
        int passwordHashVersion,
        int failedLoginCount,
        DateTimeOffset? lockedUntil,
        DateTimeOffset passwordChangedAt)
    {
        ValidateIdentityAndHash(customerId, passwordHash, passwordSalt, passwordHashVersion);

        if (failedLoginCount < 0 || failedLoginCount >= MaximumFailedLoginAttempts)
        {
            throw new ArgumentOutOfRangeException(nameof(failedLoginCount));
        }

        return new CustomerCredential
        {
            CustomerId = customerId,
            PasswordHash = passwordHash,
            PasswordSalt = passwordSalt,
            PasswordHashVersion = passwordHashVersion,
            FailedLoginCount = failedLoginCount,
            LockedUntil = lockedUntil,
            PasswordChangedAt = passwordChangedAt
        };
    }

    /// <summary>TR: Credential kaydının bağlı olduğu müşteri kimliğini döndürür. EN: Gets the customer identifier to which this credential belongs.</summary>
    public Guid CustomerId { get; private set; }

    /// <summary>TR: Base64 türetilmiş parola hash değerini döndürür; loglanmamalıdır. EN: Gets the Base64 derived password hash; it must not be logged.</summary>
    public string PasswordHash { get; private set; }

    /// <summary>TR: Base64 benzersiz parola salt değerini döndürür; loglanmamalıdır. EN: Gets the unique Base64 password salt; it must not be logged.</summary>
    public string PasswordSalt { get; private set; }

    /// <summary>TR: Sabit parola hash şeması versiyonunu döndürür. EN: Gets the fixed password-hashing scheme version.</summary>
    public int PasswordHashVersion { get; private set; }

    /// <summary>TR: Son başarılı login veya lock reset işleminden sonraki başarısız login sayısını döndürür. EN: Gets the failed-login count since the last successful login or lock reset.</summary>
    public int FailedLoginCount { get; private set; }

    /// <summary>TR: Geçici login kilidinin sona erdiği UTC zamanı; lock yoksa null döndürür. EN: Gets the UTC expiration of a temporary login lock, or null when no lock exists.</summary>
    public DateTimeOffset? LockedUntil { get; private set; }

    /// <summary>TR: Parola hash materyalinin son değiştirildiği UTC zamanı döndürür. EN: Gets the UTC time at which password-hash material was last changed.</summary>
    public DateTimeOffset PasswordChangedAt { get; private set; }

    /// <summary>
    /// TR: Credential'ın verilen zamanda geçici login kilidi altında olup olmadığını belirler.
    /// EN: Determines whether the credential is under a temporary login lock at the supplied time.
    /// </summary>
    /// <param name="now">TR: Kilit durumunun değerlendirileceği UTC zaman bilgisi. EN: UTC timestamp at which to evaluate the lock state.</param>
    /// <returns>TR: Kilit süresi henüz dolmadıysa true döndürür. EN: Returns true when the lock period has not yet expired.</returns>
    public bool IsLocked(DateTimeOffset now)
    {
        return LockedUntil.HasValue && LockedUntil.Value > now;
    }

    /// <summary>
    /// TR: Başarısız login denemesini kaydeder ve sabit eşik aşıldığında credential'a geçici kilit uygular.
    /// EN: Records a failed login attempt and applies a temporary lock to the credential when the fixed threshold is reached.
    /// </summary>
    /// <param name="now">TR: Başarısız login denemesinin gerçekleştiği UTC zaman bilgisi. EN: UTC timestamp at which the failed login attempt occurred.</param>
    public void RegisterFailedLogin(DateTimeOffset now)
    {
        FailedLoginCount++;

        if (FailedLoginCount >= MaximumFailedLoginAttempts)
        {
            LockedUntil = now.Add(LockDuration);
            FailedLoginCount = 0;
        }
    }

    /// <summary>
    /// TR: Başarılı login sonrasında başarısız deneme sayısını ve geçici kilit bilgisini temizler.
    /// EN: Clears the failed-attempt counter and temporary lock information after a successful login.
    /// </summary>
    public void RegisterSuccessfulLogin()
    {
        FailedLoginCount = 0;
        LockedUntil = null;
    }

    /// <summary>
    /// TR: Güvenli parola değişimi veya hash migration sonrasında credential hash materyalini değiştirir.
    /// EN: Replaces credential hash material after a secure password change or hash migration.
    /// </summary>
    /// <param name="passwordHash">TR: Yeni Base64 parola hash değeri. EN: New Base64 password hash value.</param>
    /// <param name="passwordSalt">TR: Yeni Base64 benzersiz salt değeri. EN: New unique Base64 salt value.</param>
    /// <param name="passwordHashVersion">TR: Yeni hash şeması versiyonu. EN: Version of the new hash scheme.</param>
    /// <param name="changedAt">TR: Parola hash materyalinin değiştirildiği UTC zaman bilgisi. EN: UTC timestamp at which password-hash material changed.</param>
    public void ChangePassword(
        string passwordHash,
        string passwordSalt,
        int passwordHashVersion,
        DateTimeOffset changedAt)
    {
        ValidateIdentityAndHash(CustomerId, passwordHash, passwordSalt, passwordHashVersion);

        PasswordHash = passwordHash;
        PasswordSalt = passwordSalt;
        PasswordHashVersion = passwordHashVersion;
        PasswordChangedAt = changedAt;
        RegisterSuccessfulLogin();
    }

    /// <summary>
    /// TR: Credential kimliği ve hash materyali için Create/Restore/ChangePassword akışlarında ortak temel doğrulamayı uygular.
    /// EN: Applies shared basic validation for credential identity and hash material across Create, Restore and ChangePassword flows.
    /// </summary>
    /// <param name="customerId">TR: Doğrulanacak müşteri kimliği. EN: Customer identifier to validate.</param>
    /// <param name="passwordHash">TR: Doğrulanacak Base64 hash değeri. EN: Base64 hash value to validate.</param>
    /// <param name="passwordSalt">TR: Doğrulanacak Base64 salt değeri. EN: Base64 salt value to validate.</param>
    /// <param name="passwordHashVersion">TR: Doğrulanacak hash versiyonu. EN: Hash version to validate.</param>
    private static void ValidateIdentityAndHash(Guid customerId, string passwordHash, string passwordSalt, int passwordHashVersion)
    {
        if (customerId == Guid.Empty)
        {
            throw new ArgumentException("Customer identifier cannot be empty.", nameof(customerId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordSalt);

        if (passwordHashVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(passwordHashVersion));
        }
    }
}
