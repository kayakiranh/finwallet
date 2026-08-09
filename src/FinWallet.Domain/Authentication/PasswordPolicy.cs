namespace FinWallet.Domain.Authentication;

/// <summary>
/// TR: Müşteri parolalarının kabul edilmesi için değiştirilemeyen uygulama güvenlik kurallarını uygular; kurallar runtime configuration üzerinden gevşetilemez.
/// EN: Applies the fixed application security rules for accepting customer passwords; the rules cannot be weakened through runtime configuration.
/// </summary>
public static class PasswordPolicy
{
    /// <summary>
    /// TR: Kabul edilen parolalar için minimum karakter sayısını tanımlar.
    /// EN: Defines the minimum number of characters accepted for passwords.
    /// </summary>
    public const int MinimumLength = 12;

    /// <summary>
    /// TR: Aşırı uzun parola girdilerinin kaynak tüketimini sınırlamak için kabul edilen maksimum karakter sayısını tanımlar.
    /// EN: Defines the maximum accepted password length to limit resource consumption from excessively long password input.
    /// </summary>
    public const int MaximumLength = 128;

    /// <summary>
    /// TR: Parolanın sabit uzunluk ve içerik kurallarını doğrular; güvenliği zayıflatabilecek yapılandırılabilir seçenekler kullanmaz.
    /// EN: Validates the fixed password length and content rules without exposing configurable options that could weaken security.
    /// </summary>
    /// <param name="password">
    /// TR: Kayıt veya parola doğrulama öncesinde kontrol edilecek ham parola.
    /// EN: Raw password to validate before registration or password processing.
    /// </param>
    /// <exception cref="ArgumentException">
    /// TR: Parola boşsa, uzunluk sınırlarını ihlal ediyorsa veya kontrol karakteri içeriyorsa oluşur.
    /// EN: Thrown when the password is empty, violates length limits or contains control characters.
    /// </exception>
    public static void Validate(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        if (password.Length < MinimumLength || password.Length > MaximumLength)
        {
            throw new ArgumentException($"Password length must be between {MinimumLength} and {MaximumLength} characters.", nameof(password));
        }

        if (password.Any(char.IsControl))
        {
            throw new ArgumentException("Password cannot contain control characters.", nameof(password));
        }
    }
}
