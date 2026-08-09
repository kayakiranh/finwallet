namespace FinWallet.Api.Contracts.Authentication;

/// <summary>
/// TR: Yeni müşteri registration endpoint'ine gönderilen ülke, telefon, e-posta ve parola alanlarını tanımlar.
/// EN: Defines country, phone, email and password fields submitted to the new-customer registration endpoint.
/// </summary>
public sealed class RegisterCustomerRequest
{
    /// <summary>
    /// TR: Kullanıcının kayıt için seçtiği iki harfli ülke kodunu döndürür veya ayarlar.
    /// EN: Gets or sets the two-letter country code selected by the user for registration.
    /// </summary>
    public string CountryCode { get; set; } = string.Empty;

    /// <summary>
    /// TR: Uluslararası formatta kullanıcı telefon numarasını döndürür veya ayarlar.
    /// EN: Gets or sets the user phone number in international format.
    /// </summary>
    public string PhoneNumber { get; set; } = string.Empty;

    /// <summary>
    /// TR: Finansal bildirimlerde kullanılabilecek isteğe bağlı e-posta adresini döndürür veya ayarlar.
    /// EN: Gets or sets the optional email address that may be used for financial notifications.
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// TR: Yalnızca request işleme süresince bellekte tutulması gereken ham parolayı döndürür veya ayarlar; loglanmamalıdır.
    /// EN: Gets or sets the raw password that must remain in memory only during request processing; it must not be logged.
    /// </summary>
    public string Password { get; set; } = string.Empty;
}
