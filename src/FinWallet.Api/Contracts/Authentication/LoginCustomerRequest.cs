namespace FinWallet.Api.Contracts.Authentication;

/// <summary>
/// TR: Müşteri login endpoint'ine gönderilen telefon, parola ve cihaz kimliği alanlarını tanımlar.
/// EN: Defines the phone, password and device-identifier fields submitted to the customer-login endpoint.
/// </summary>
public sealed class LoginCustomerRequest
{
    /// <summary>
    /// TR: Uluslararası formatta müşteri telefon numarasını döndürür veya ayarlar.
    /// EN: Gets or sets the customer phone number in international format.
    /// </summary>
    public string PhoneNumber { get; set; } = string.Empty;

    /// <summary>
    /// TR: Yalnızca doğrulama süresince bellekte tutulacak ham parolayı döndürür veya ayarlar; loglanmamalıdır.
    /// EN: Gets or sets the raw password retained only for verification; it must not be logged.
    /// </summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// TR: Oluşturulacak session'ın bağlanacağı istemci cihaz/uygulama örneği kimliğini döndürür veya ayarlar.
    /// EN: Gets or sets the client device/application-instance identifier to which the new session will be bound.
    /// </summary>
    public string DeviceId { get; set; } = string.Empty;
}
