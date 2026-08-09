namespace FinWallet.Application.Registration;

/// <summary>
/// TR: Yeni müşteri kayıt akışının ülke, telefon, e-posta ve parola girdilerini Application katmanına taşır.
/// EN: Carries country, phone, email and password input for a new customer registration flow into the Application layer.
/// </summary>
public sealed class RegisterCustomerCommand
{
    /// <summary>
    /// TR: Yeni müşteri kayıt komutunu oluşturur.
    /// EN: Creates a new customer-registration command.
    /// </summary>
    /// <param name="countryCode">
    /// TR: Kullanıcının seçtiği iki harfli kayıt ülkesi.
    /// EN: Two-letter registration country selected by the user.
    /// </param>
    /// <param name="phoneNumber">
    /// TR: Kullanıcının uluslararası formatta girdiği telefon numarası.
    /// EN: Phone number entered by the user in international format.
    /// </param>
    /// <param name="email">
    /// TR: Finansal bildirimler için kullanılabilecek isteğe bağlı e-posta adresi.
    /// EN: Optional email address that may be used for financial notifications.
    /// </param>
    /// <param name="password">
    /// TR: Yalnızca işlem süresince bellekte tutulacak ham müşteri parolası.
    /// EN: Raw customer password retained in memory only for the duration of processing.
    /// </param>
    public RegisterCustomerCommand(string countryCode, string phoneNumber, string? email, string password)
    {
        CountryCode = countryCode;
        PhoneNumber = phoneNumber;
        Email = email;
        Password = password;
    }

    /// <summary>
    /// TR: Kullanıcının seçtiği kayıt ülkesi kodunu döndürür.
    /// EN: Gets the registration-country code selected by the user.
    /// </summary>
    public string CountryCode { get; }

    /// <summary>
    /// TR: Kullanıcının girdiği ham telefon numarasını döndürür; handler tarafından normalize edilir.
    /// EN: Gets the raw phone number entered by the user; it is normalized by the handler.
    /// </summary>
    public string PhoneNumber { get; }

    /// <summary>
    /// TR: İsteğe bağlı müşteri e-posta adresini döndürür.
    /// EN: Gets the optional customer email address.
    /// </summary>
    public string? Email { get; }

    /// <summary>
    /// TR: Ham müşteri parolasını döndürür; loglanmamalı veya kalıcı depoya yazılmamalıdır.
    /// EN: Gets the raw customer password; it must not be logged or written to persistent storage.
    /// </summary>
    public string Password { get; }
}
