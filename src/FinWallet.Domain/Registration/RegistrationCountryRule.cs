namespace FinWallet.Domain.Registration;

/// <summary>
/// TR: Bir ülkenin müşteri kaydına kabul edilmesi için gereken ülke kodu, telefon kodu ve ulusal numara uzunluğu kurallarını temsil eder.
/// EN: Represents the country code, calling code and national number length rules required for accepting customer registration from a country.
/// </summary>
public sealed class RegistrationCountryRule
{
    /// <summary>
    /// TR: Kayıt ülkesi kuralını oluşturur ve kuralın kendi içinde tutarlı olmasını doğrular.
    /// EN: Creates a registration-country rule and validates that the rule is internally consistent.
    /// </summary>
    /// <param name="countryCode">
    /// TR: İki harfli ülke kodu.
    /// EN: Two-letter country code.
    /// </param>
    /// <param name="callingCode">
    /// TR: Artı işaretiyle başlayan uluslararası telefon ülke kodu.
    /// EN: International country calling code beginning with a plus sign.
    /// </param>
    /// <param name="nationalNumberLength">
    /// TR: Ülke kodundan sonra beklenen ulusal telefon numarası rakam sayısı.
    /// EN: Expected number of national phone-number digits after the country calling code.
    /// </param>
    /// <exception cref="ArgumentException">
    /// TR: Ülke kodu veya telefon kodu geçersizse oluşur.
    /// EN: Thrown when the country code or calling code is invalid.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// TR: Ulusal telefon numarası uzunluğu geçerli sınırlar dışında ise oluşur.
    /// EN: Thrown when the national phone-number length is outside valid boundaries.
    /// </exception>
    public RegistrationCountryRule(string countryCode, string callingCode, int nationalNumberLength)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(countryCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(callingCode);

        var normalizedCountryCode = countryCode.Trim().ToUpperInvariant();
        if (normalizedCountryCode.Length != 2 || !normalizedCountryCode.All(char.IsAsciiLetter))
        {
            throw new ArgumentException("Country code must contain exactly two ASCII letters.", nameof(countryCode));
        }

        var normalizedCallingCode = callingCode.Trim();
        if (!normalizedCallingCode.StartsWith('+') || normalizedCallingCode.Length < 2 || !normalizedCallingCode[1..].All(char.IsAsciiDigit))
        {
            throw new ArgumentException("Calling code must start with '+' and contain only digits after it.", nameof(callingCode));
        }

        if (nationalNumberLength is < 4 or > 12)
        {
            throw new ArgumentOutOfRangeException(nameof(nationalNumberLength), "National phone-number length must be between 4 and 12 digits.");
        }

        CountryCode = normalizedCountryCode;
        CallingCode = normalizedCallingCode;
        NationalNumberLength = nationalNumberLength;
    }

    /// <summary>
    /// TR: Kuralın ait olduğu normalize iki harfli ülke kodunu döndürür.
    /// EN: Gets the normalized two-letter country code to which this rule belongs.
    /// </summary>
    public string CountryCode { get; }

    /// <summary>
    /// TR: Kuralın kabul ettiği uluslararası telefon ülke kodunu döndürür.
    /// EN: Gets the international country calling code accepted by this rule.
    /// </summary>
    public string CallingCode { get; }

    /// <summary>
    /// TR: Ülke telefon kodundan sonra bulunması gereken ulusal numara rakam sayısını döndürür.
    /// EN: Gets the number of national phone digits required after the country calling code.
    /// </summary>
    public int NationalNumberLength { get; }

    /// <summary>
    /// TR: Normalize telefon numarasının bu ülke kuralının telefon kodu ve ulusal numara uzunluğu ile eşleşip eşleşmediğini belirler.
    /// EN: Determines whether a normalized phone number matches this country's calling code and national-number length.
    /// </summary>
    /// <param name="phoneNumber">
    /// TR: Doğrulanacak normalize telefon numarası.
    /// EN: Normalized phone number to validate.
    /// </param>
    /// <returns>
    /// TR: Telefon numarası ülke kuralıyla tam uyumluysa true döndürür.
    /// EN: Returns true when the phone number fully complies with the country rule.
    /// </returns>
    public bool Matches(PhoneNumber phoneNumber)
    {
        ArgumentNullException.ThrowIfNull(phoneNumber);

        return phoneNumber.Value.StartsWith(CallingCode, StringComparison.Ordinal)
            && phoneNumber.Value.Length == CallingCode.Length + NationalNumberLength;
    }
}
