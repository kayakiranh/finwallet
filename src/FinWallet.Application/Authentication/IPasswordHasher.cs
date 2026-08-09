namespace FinWallet.Application.Authentication;

/// <summary>
/// TR: Application katmanını somut kriptografi implementasyonundan ayıran ve yalnızca sabit güvenlik politikasına uygun parola hash/doğrulama işlemlerini sunan sınırı tanımlar.
/// EN: Defines the boundary that decouples the Application layer from the concrete cryptography implementation and exposes only password hashing/verification operations compliant with the fixed security policy.
/// </summary>
public interface IPasswordHasher
{
    /// <summary>
    /// TR: Ham paroladan benzersiz salt içeren güvenli ve kalıcı saklamaya uygun hash sonucu üretir.
    /// EN: Produces a secure persistent hash result with a unique salt from a raw password.
    /// </summary>
    /// <param name="password">
    /// TR: Hash üretilecek ham parola; metot sonrasında saklanmamalı veya loglanmamalıdır.
    /// EN: Raw password to hash; it must not be persisted or logged after processing.
    /// </param>
    /// <returns>
    /// TR: Hash, salt ve hash versiyonunu içeren sonucu döndürür.
    /// EN: Returns the result containing hash, salt and hash version.
    /// </returns>
    PasswordHashResult Hash(string password);

    /// <summary>
    /// TR: Ham parolayı kalıcı hash bilgisiyle sabit-zamanlı karşılaştırma kullanarak doğrular.
    /// EN: Verifies a raw password against persisted hash material using constant-time comparison.
    /// </summary>
    /// <param name="password">
    /// TR: Doğrulanacak ham parola.
    /// EN: Raw password to verify.
    /// </param>
    /// <param name="storedHash">
    /// TR: Kalıcı kayıttan okunan Base64 parola hash değeri.
    /// EN: Base64 password hash read from persistent storage.
    /// </param>
    /// <param name="storedSalt">
    /// TR: Kalıcı kayıttan okunan Base64 salt değeri.
    /// EN: Base64 salt read from persistent storage.
    /// </param>
    /// <param name="hashVersion">
    /// TR: Kalıcı kaydın hangi güvenli hash şemasıyla üretildiğini belirleyen versiyon.
    /// EN: Version identifying which secure hash scheme produced the persisted value.
    /// </param>
    /// <returns>
    /// TR: Parola kayıtlı değerle güvenli biçimde eşleşiyorsa true döndürür.
    /// EN: Returns true when the password securely matches the stored value.
    /// </returns>
    bool Verify(string password, string storedHash, string storedSalt, int hashVersion);
}
