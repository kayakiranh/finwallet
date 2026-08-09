using System.Security.Cryptography;
using FinWallet.Application.Authentication;
using FinWallet.Domain.Authentication;

namespace FinWallet.Infrastructure.Authentication;

/// <summary>
/// TR: Parolaları sabit güvenlik parametreleriyle PBKDF2-HMAC-SHA512 kullanarak türeten ve doğrulayan somut kriptografi implementasyonudur; algoritma ve work factor runtime configuration ile değiştirilemez.
/// EN: Concrete cryptography implementation that derives and verifies passwords with PBKDF2-HMAC-SHA512 using fixed security parameters; the algorithm and work factor cannot be changed through runtime configuration.
/// </summary>
public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    /// <summary>
    /// TR: V1 parola hash şemasını tanımlayan kalıcı versiyon numarasıdır.
    /// EN: Persistent version number identifying the version-1 password hash scheme.
    /// </summary>
    private const int CurrentHashVersion = 1;

    /// <summary>
    /// TR: PBKDF2-HMAC-SHA512 için kullanılan sabit iteration sayısıdır; runtime ayarı değildir.
    /// EN: Fixed iteration count used for PBKDF2-HMAC-SHA512; it is not a runtime setting.
    /// </summary>
    private const int IterationCount = 220_000;

    /// <summary>
    /// TR: Her parola için kriptografik olarak rastgele üretilecek salt uzunluğunu byte cinsinden tanımlar.
    /// EN: Defines in bytes the cryptographically random salt length generated independently for every password.
    /// </summary>
    private const int SaltLength = 32;

    /// <summary>
    /// TR: PBKDF2 ile üretilecek parola hash çıktısının byte uzunluğunu tanımlar.
    /// EN: Defines the byte length of the password hash output derived with PBKDF2.
    /// </summary>
    private const int HashLength = 64;

    /// <summary>
    /// TR: Parola politikasını doğrular, yeni benzersiz salt üretir ve sabit PBKDF2-HMAC-SHA512 parametreleriyle hash sonucu oluşturur.
    /// EN: Validates the password policy, generates a new unique salt and creates the hash result with fixed PBKDF2-HMAC-SHA512 parameters.
    /// </summary>
    /// <param name="password">
    /// TR: Hash üretilecek ham parola; bu değer saklanmaz ve loglanmamalıdır.
    /// EN: Raw password to hash; this value is not persisted and must not be logged.
    /// </param>
    /// <returns>
    /// TR: Base64 hash, Base64 salt ve hash versiyonunu içeren sonucu döndürür.
    /// EN: Returns a result containing the Base64 hash, Base64 salt and hash version.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// TR: Parola sabit parola politikasını karşılamıyorsa oluşur.
    /// EN: Thrown when the password does not satisfy the fixed password policy.
    /// </exception>
    public PasswordHashResult Hash(string password)
    {
        PasswordPolicy.Validate(password);

        var salt = RandomNumberGenerator.GetBytes(SaltLength);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            IterationCount,
            HashAlgorithmName.SHA512,
            HashLength);

        return new PasswordHashResult(
            Convert.ToBase64String(hash),
            Convert.ToBase64String(salt),
            CurrentHashVersion);
    }

    /// <summary>
    /// TR: Ham parolayı kayıtlı V1 PBKDF2-HMAC-SHA512 hash'iyle yeniden türetir ve timing saldırılarını azaltmak için sabit-zamanlı karşılaştırma yapar.
    /// EN: Re-derives the raw password against the stored version-1 PBKDF2-HMAC-SHA512 hash and performs constant-time comparison to reduce timing-attack exposure.
    /// </summary>
    /// <param name="password">
    /// TR: Doğrulanacak ham parola.
    /// EN: Raw password to verify.
    /// </param>
    /// <param name="storedHash">
    /// TR: Kalıcı kayıttaki Base64 parola hash değeri.
    /// EN: Base64 password hash from persistent storage.
    /// </param>
    /// <param name="storedSalt">
    /// TR: Kalıcı kayıttaki Base64 salt değeri.
    /// EN: Base64 salt from persistent storage.
    /// </param>
    /// <param name="hashVersion">
    /// TR: Kalıcı kayıttaki parola hash şeması versiyonu.
    /// EN: Password hash scheme version stored with the credential.
    /// </param>
    /// <returns>
    /// TR: Parola kayıtlı hash ile eşleşiyorsa true; temel parola sınırlarını ihlal ediyorsa false döndürür.
    /// EN: Returns true when the password matches the stored hash; returns false when the password violates basic password boundaries.
    /// </returns>
    /// <exception cref="NotSupportedException">
    /// TR: Kalıcı kayıt uygulamanın desteklemediği bir hash versiyonu içeriyorsa oluşur.
    /// EN: Thrown when the persisted credential contains a hash version unsupported by the application.
    /// </exception>
    /// <exception cref="FormatException">
    /// TR: Kalıcı hash veya salt Base64 biçiminde bozuksa oluşur ve veri bütünlüğü problemi olarak ele alınmalıdır.
    /// EN: Thrown when the persisted hash or salt contains invalid Base64 and should be treated as a data-integrity problem.
    /// </exception>
    public bool Verify(string password, string storedHash, string storedSalt, int hashVersion)
    {
        ArgumentNullException.ThrowIfNull(password);
        ArgumentException.ThrowIfNullOrWhiteSpace(storedHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(storedSalt);

        if (hashVersion != CurrentHashVersion)
        {
            throw new NotSupportedException($"Password hash version '{hashVersion}' is not supported.");
        }

        if (password.Length < PasswordPolicy.MinimumLength
            || password.Length > PasswordPolicy.MaximumLength
            || password.Any(char.IsControl))
        {
            return false;
        }

        var salt = Convert.FromBase64String(storedSalt);
        var expectedHash = Convert.FromBase64String(storedHash);
        var actualHash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            IterationCount,
            HashAlgorithmName.SHA512,
            expectedHash.Length);

        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }
}
