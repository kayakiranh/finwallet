namespace FinWallet.Application.Authentication;

/// <summary>
/// TR: Parola türetme işleminin kalıcı olarak saklanacak hash, benzersiz salt ve güvenli migration için hash versiyonu çıktısını taşır.
/// EN: Carries the password-derivation output consisting of the hash, unique salt and hash version used for safe future migrations.
/// </summary>
public sealed class PasswordHashResult
{
    /// <summary>
    /// TR: Parola hash sonucunu oluşturur.
    /// EN: Creates a password-hash result.
    /// </summary>
    /// <param name="hash">
    /// TR: Base64 biçiminde türetilmiş parola hash değeri.
    /// EN: Derived password hash encoded as Base64.
    /// </param>
    /// <param name="salt">
    /// TR: Base64 biçiminde benzersiz kriptografik salt değeri.
    /// EN: Unique cryptographic salt encoded as Base64.
    /// </param>
    /// <param name="version">
    /// TR: İleride güvenli rehash/migration yapılabilmesi için sabit hash şeması versiyonu.
    /// EN: Fixed hash-scheme version enabling safe rehash/migration in the future.
    /// </param>
    public PasswordHashResult(string hash, string salt, int version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hash);
        ArgumentException.ThrowIfNullOrWhiteSpace(salt);

        Hash = hash;
        Salt = salt;
        Version = version;
    }

    /// <summary>
    /// TR: Base64 biçimindeki türetilmiş parola hash değerini döndürür.
    /// EN: Gets the derived password hash encoded as Base64.
    /// </summary>
    public string Hash { get; }

    /// <summary>
    /// TR: Base64 biçimindeki benzersiz parola salt değerini döndürür.
    /// EN: Gets the unique password salt encoded as Base64.
    /// </summary>
    public string Salt { get; }

    /// <summary>
    /// TR: Kullanılan sabit parola hash şemasının versiyonunu döndürür.
    /// EN: Gets the version of the fixed password-hashing scheme used.
    /// </summary>
    public int Version { get; }
}
