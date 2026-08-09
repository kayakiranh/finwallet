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
    /// <param name="customerId">
    /// TR: Credential kaydının bağlı olduğu müşteri kimliği.
    /// EN: Customer identifier to which the credential record belongs.
    /// </param>
    /// <param name="passwordHash">
    /// TR: Base64 biçiminde türetilmiş parola hash değeri.
    /// EN: Derived password hash encoded as Base64.
    /// </param>
    /// <param name="passwordSalt">
    /// TR: Base64 biçiminde benzersiz kriptografik salt değeri.
    /// EN: Unique cryptographic salt encoded as Base64.
    /// </param>
    /// <param name="passwordHashVersion">
    /// TR: Güvenli hash migration'larını destekleyen şema versiyonu.
    /// EN: Hash-scheme version supporting safe future migrations.
    /// </param>
    /// <param name="createdAt">
    /// TR: Credential kaydının oluşturulduğu UTC zaman bilgisi.
    /// EN: UTC timestamp at which the credential record was created.
    /// </param>
    /// <returns>
    /// TR: Yeni müşteri credential nesnesini döndürür.
    /// EN: Returns the newly created customer credential object.
    /// </returns>
    public static CustomerCredential Create(
        Guid customerId,
        string passwordHash,
        string passwordSalt,
        int passwordHashVersion,
        DateTimeOffset createdAt)
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
    /// TR: Credential kaydının bağlı olduğu müşteri kimliğini döndürür.
    /// EN: Gets the customer identifier to which this credential belongs.
    /// </summary>
    public Guid CustomerId { get; private set; }

    /// <summary>
    /// TR: Ham parolayı içermeyen Base64 türetilmiş parola hash değerini döndürür; loglanmamalıdır.
    /// EN: Gets the Base64 derived password hash that never contains the raw password; it must not be logged.
    /// </summary>
    public string PasswordHash { get; private set; }

    /// <summary>
    /// TR: Parola hash'i için kullanılan Base64 benzersiz salt değerini döndürür; loglanmamalıdır.
    /// EN: Gets the Base64 unique salt used for the password hash; it must not be logged.
    /// </summary>
    public string PasswordSalt { get; private set; }

    /// <summary>
    /// TR: Credential'ın hangi sabit parola hash şemasıyla üretildiğini belirten versiyonu döndürür.
    /// EN: Gets the version identifying which fixed password-hashing scheme produced this credential.
    /// </summary>
    public int PasswordHashVersion { get; private set; }

    /// <summary>
    /// TR: Son başarılı login veya lock reset işleminden bu yana art arda gerçekleşen başarısız login sayısını döndürür.
    /// EN: Gets the number of consecutive failed login attempts since the last successful login or lock reset.
    /// </summary>
    public int FailedLoginCount { get; private set; }

    /// <summary>
    /// TR: Credential'ın geçici olarak login kabul etmemesi gereken UTC zamanı döndürür; null ise aktif geçici kilit yoktur.
    /// EN: Gets the UTC time until which the credential must reject login; null means there is no active temporary lock.
    /// </summary>
    public DateTimeOffset? LockedUntil { get; private set; }

    /// <summary>
    /// TR: Parola hash materyalinin son değiştirildiği UTC zamanını döndürür.
    /// EN: Gets the UTC timestamp at which the password-hash material was last changed.
    /// </summary>
    public DateTimeOffset PasswordChangedAt { get; private set; }

    /// <summary>
    /// TR: Credential'ın verilen zamanda geçici login kilidi altında olup olmadığını belirler.
    /// EN: Determines whether the credential is under a temporary login lock at the supplied time.
    /// </summary>
    /// <param name="now">
    /// TR: Kilit durumunun değerlendirileceği UTC zaman bilgisi.
    /// EN: UTC timestamp at which to evaluate the lock state.
    /// </param>
    /// <returns>
    /// TR: Kilit süresi henüz dolmadıysa true döndürür.
    /// EN: Returns true when the lock period has not yet expired.
    /// </returns>
    public bool IsLocked(DateTimeOffset now)
    {
        return LockedUntil.HasValue && LockedUntil.Value > now;
    }

    /// <summary>
    /// TR: Başarısız login denemesini kaydeder ve sabit eşik aşıldığında credential'a geçici kilit uygular.
    /// EN: Records a failed login attempt and applies a temporary lock to the credential when the fixed threshold is reached.
    /// </summary>
    /// <param name="now">
    /// TR: Başarısız login denemesinin gerçekleştiği UTC zaman bilgisi.
    /// EN: UTC timestamp at which the failed login attempt occurred.
    /// </param>
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
    /// TR: Başarılı login sonrasında başarısız deneme sayısını ve süresi dolmuş/geçerli geçici kilit bilgisini temizler.
    /// EN: Clears the failed-attempt counter and temporary lock information after a successful login.
    /// </summary>
    public void RegisterSuccessfulLogin()
    {
        FailedLoginCount = 0;
        LockedUntil = null;
    }

    /// <summary>
    /// TR: Güvenli parola değişimi veya hash migration sonrasında credential'ın hash materyalini yeni değerlerle değiştirir.
    /// EN: Replaces credential hash material with new values after a secure password change or hash migration.
    /// </summary>
    /// <param name="passwordHash">
    /// TR: Yeni Base64 parola hash değeri.
    /// EN: New Base64 password hash value.
    /// </param>
    /// <param name="passwordSalt">
    /// TR: Yeni Base64 benzersiz salt değeri.
    /// EN: New unique Base64 salt value.
    /// </param>
    /// <param name="passwordHashVersion">
    /// TR: Yeni hash şemasının versiyonu.
    /// EN: Version of the new hash scheme.
    /// </param>
    /// <param name="changedAt">
    /// TR: Parola hash materyalinin değiştirildiği UTC zaman bilgisi.
    /// EN: UTC timestamp at which the password-hash material was changed.
    /// </param>
    public void ChangePassword(
        string passwordHash,
        string passwordSalt,
        int passwordHashVersion,
        DateTimeOffset changedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordSalt);

        if (passwordHashVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(passwordHashVersion));
        }

        PasswordHash = passwordHash;
        PasswordSalt = passwordSalt;
        PasswordHashVersion = passwordHashVersion;
        PasswordChangedAt = changedAt;
        RegisterSuccessfulLogin();
    }
}
