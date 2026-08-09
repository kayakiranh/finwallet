namespace FinWallet.Domain.Registration;

/// <summary>
/// TR: Müşteri kaydında kabul edilen ülkeleri ve ülke ile telefon numarası arasındaki zorunlu uyumu merkezi bir business policy olarak uygular.
/// EN: Applies the allowed registration countries and mandatory country-to-phone compatibility as a centralized business policy for customer registration.
/// </summary>
public sealed class RegistrationCountryPolicy
{
    private static readonly IReadOnlyDictionary<string, RegistrationCountryRule> Rules =
        new Dictionary<string, RegistrationCountryRule>(StringComparer.OrdinalIgnoreCase)
        {
            ["TR"] = new RegistrationCountryRule("TR", "+90", 10),
            ["AZ"] = new RegistrationCountryRule("AZ", "+994", 9)
        };

    /// <summary>
    /// TR: Ülke kodunu normalize eder, ülkenin kayıt için desteklendiğini doğrular ve telefon numarasının seçilen ülkeye ait olmasını zorunlu kılar.
    /// EN: Normalizes the country code, verifies that the country is supported for registration and requires the phone number to belong to the selected country.
    /// </summary>
    /// <param name="countryCode">
    /// TR: Kullanıcının kayıt sırasında seçtiği iki harfli ülke kodu.
    /// EN: Two-letter country code selected by the user during registration.
    /// </param>
    /// <param name="phoneNumber">
    /// TR: Formatı daha önce doğrulanmış normalize telefon numarası.
    /// EN: Normalized phone number whose basic format has already been validated.
    /// </param>
    /// <returns>
    /// TR: Kayıt için kabul edilmiş normalize ülke kodunu döndürür.
    /// EN: Returns the normalized country code accepted for registration.
    /// </returns>
    /// <exception cref="RegistrationNotAllowedException">
    /// TR: Ülke allow-list içinde değilse veya telefon numarası seçilen ülke kuralıyla eşleşmiyorsa oluşur.
    /// EN: Thrown when the country is not in the allow-list or the phone number does not match the selected country rule.
    /// </exception>
    public string Validate(string countryCode, PhoneNumber phoneNumber)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(countryCode);
        ArgumentNullException.ThrowIfNull(phoneNumber);

        var normalizedCountryCode = countryCode.Trim().ToUpperInvariant();
        if (!Rules.TryGetValue(normalizedCountryCode, out var rule))
        {
            throw new RegistrationNotAllowedException($"Registration is not supported for country '{normalizedCountryCode}'.");
        }

        if (!rule.Matches(phoneNumber))
        {
            throw new RegistrationNotAllowedException("Phone number does not match the selected registration country.");
        }

        return normalizedCountryCode;
    }

    /// <summary>
    /// TR: Mevcut business policy tarafından kayıt için desteklenen ülke kodlarını salt okunur olarak döndürür.
    /// EN: Gets the country codes currently supported for registration by the business policy as a read-only collection.
    /// </summary>
    /// <returns>
    /// TR: Desteklenen ülke kodlarını döndürür.
    /// EN: Returns the supported country codes.
    /// </returns>
    public IReadOnlyCollection<string> GetSupportedCountryCodes()
    {
        return Rules.Keys.OrderBy(static code => code, StringComparer.Ordinal).ToArray();
    }
}
