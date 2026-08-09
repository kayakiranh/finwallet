namespace FinWallet.Domain.Registration;

/// <summary>
/// TR: Kayıt, OTP ve müşteri iletişiminde kullanılan normalize uluslararası telefon numarasını temsil eder; yalnızca artı işareti ve rakamlardan oluşan E.164-benzeri değeri domain içinde taşır.
/// EN: Represents the normalized international phone number used for registration, OTP and customer communication; it carries an E.164-like value containing only a leading plus sign and digits inside the domain.
/// </summary>
public sealed class PhoneNumber : IEquatable<PhoneNumber>
{
    /// <summary>
    /// TR: Normalize telefon numarası nesnesini oluşturur; dışarıdan doğrudan çağrılmak yerine <see cref="Create(string)"/> kullanılmalıdır.
    /// EN: Creates the normalized phone number object; callers should use <see cref="Create(string)"/> instead of invoking this constructor directly.
    /// </summary>
    /// <param name="value">
    /// TR: Doğrulanmış ve normalize edilmiş uluslararası telefon numarası.
    /// EN: Validated and normalized international phone number.
    /// </param>
    private PhoneNumber(string value)
    {
        Value = value;
    }

    /// <summary>
    /// TR: Normalize edilmiş telefon numarasını ülke kodu dahil artı işaretiyle başlayan biçimde döndürür.
    /// EN: Gets the normalized phone number including the country calling code in a format beginning with a plus sign.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// TR: Kullanıcı girişini normalize eder ve temel uluslararası telefon formatını doğrular; ülkenin kayıt için desteklenip desteklenmediğine bu metot karar vermez.
    /// EN: Normalizes user input and validates the basic international phone format; this method does not decide whether the country is supported for registration.
    /// </summary>
    /// <param name="rawValue">
    /// TR: Kullanıcıdan alınan telefon numarası; boşluk, tire ve parantez gibi yaygın görsel ayraçlar içerebilir.
    /// EN: Phone number received from the user; it may contain common visual separators such as spaces, hyphens and parentheses.
    /// </param>
    /// <returns>
    /// TR: Normalize edilmiş telefon numarası value object'ini döndürür.
    /// EN: Returns the normalized phone number value object.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// TR: Değer boşsa, artı işaretiyle başlamıyorsa, desteklenmeyen karakter içeriyorsa veya E.164 uzunluk sınırlarını ihlal ediyorsa oluşur.
    /// EN: Thrown when the value is empty, does not start with a plus sign, contains unsupported characters or violates E.164 length boundaries.
    /// </exception>
    public static PhoneNumber Create(string rawValue)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rawValue);

        var trimmed = rawValue.Trim();
        if (!trimmed.StartsWith('+'))
        {
            throw new ArgumentException("Phone number must start with the international '+' prefix.", nameof(rawValue));
        }

        var normalizedCharacters = new List<char>(trimmed.Length);
        for (var index = 0; index < trimmed.Length; index++)
        {
            var character = trimmed[index];
            if (index == 0 && character == '+')
            {
                normalizedCharacters.Add(character);
                continue;
            }

            if (char.IsAsciiDigit(character))
            {
                normalizedCharacters.Add(character);
                continue;
            }

            if (character is ' ' or '-' or '(' or ')')
            {
                continue;
            }

            throw new ArgumentException("Phone number contains an unsupported character.", nameof(rawValue));
        }

        var normalized = new string(normalizedCharacters.ToArray());
        var digitCount = normalized.Length - 1;
        if (digitCount is < 8 or > 15)
        {
            throw new ArgumentException("Phone number must contain between 8 and 15 digits.", nameof(rawValue));
        }

        return new PhoneNumber(normalized);
    }

    /// <summary>
    /// TR: İki telefon numarasını normalize değerlerine göre karşılaştırır.
    /// EN: Compares two phone numbers by their normalized values.
    /// </summary>
    /// <param name="other">
    /// TR: Karşılaştırılacak diğer telefon numarası.
    /// EN: Other phone number to compare.
    /// </param>
    /// <returns>
    /// TR: Normalize değerler aynıysa true döndürür.
    /// EN: Returns true when the normalized values are equal.
    /// </returns>
    public bool Equals(PhoneNumber? other)
    {
        return other is not null && StringComparer.Ordinal.Equals(Value, other.Value);
    }

    /// <summary>
    /// TR: Nesnenin başka bir telefon numarasıyla değer eşitliğini kontrol eder.
    /// EN: Checks value equality with another phone number object.
    /// </summary>
    /// <param name="obj">
    /// TR: Karşılaştırılacak nesne.
    /// EN: Object to compare.
    /// </param>
    /// <returns>
    /// TR: Nesne aynı normalize telefon değerini taşıyorsa true döndürür.
    /// EN: Returns true when the object carries the same normalized phone value.
    /// </returns>
    public override bool Equals(object? obj)
    {
        return obj is PhoneNumber other && Equals(other);
    }

    /// <summary>
    /// TR: Normalize telefon değeri için kararlı hash kodunu üretir.
    /// EN: Produces a stable hash code for the normalized phone value.
    /// </summary>
    /// <returns>
    /// TR: Telefon numarasının hash kodunu döndürür.
    /// EN: Returns the phone number hash code.
    /// </returns>
    public override int GetHashCode()
    {
        return StringComparer.Ordinal.GetHashCode(Value);
    }

    /// <summary>
    /// TR: Telefon numarasını normalize metin değeri olarak döndürür.
    /// EN: Returns the phone number as its normalized text value.
    /// </summary>
    /// <returns>
    /// TR: Normalize telefon numarası.
    /// EN: Normalized phone number.
    /// </returns>
    public override string ToString()
    {
        return Value;
    }
}
