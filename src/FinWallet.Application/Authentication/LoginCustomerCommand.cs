namespace FinWallet.Application.Authentication;

/// <summary>
/// TR: Müşteri login akışının telefon, parola ve cihaz kimliği girdilerini taşır.
/// EN: Carries phone, password and device-identifier input for the customer login flow.
/// </summary>
public sealed class LoginCustomerCommand
{
    /// <summary>
    /// TR: Login komutunu oluşturur.
    /// EN: Creates the login command.
    /// </summary>
    /// <param name="phoneNumber">
    /// TR: Müşterinin uluslararası formatta girdiği telefon numarası.
    /// EN: Customer phone number entered in international format.
    /// </param>
    /// <param name="password">
    /// TR: Yalnızca doğrulama süresince bellekte tutulacak ham parola.
    /// EN: Raw password retained in memory only for verification.
    /// </param>
    /// <param name="deviceId">
    /// TR: Session'ın bağlanacağı normalize cihaz veya uygulama örneği kimliği.
    /// EN: Normalized device or application-instance identifier to which the session is bound.
    /// </param>
    public LoginCustomerCommand(string phoneNumber, string password, string deviceId)
    {
        PhoneNumber = phoneNumber;
        Password = password;
        DeviceId = deviceId;
    }

    /// <summary>
    /// TR: Login için girilen ham telefon numarasını döndürür; handler tarafından normalize edilir.
    /// EN: Gets the raw phone number entered for login; it is normalized by the handler.
    /// </summary>
    public string PhoneNumber { get; }

    /// <summary>
    /// TR: Ham müşteri parolasını döndürür; loglanmamalı veya saklanmamalıdır.
    /// EN: Gets the raw customer password; it must not be logged or persisted.
    /// </summary>
    public string Password { get; }

    /// <summary>
    /// TR: Login sonucunda oluşturulacak session'ın bağlanacağı cihaz/uygulama örneği kimliğini döndürür.
    /// EN: Gets the device/application-instance identifier to which the resulting session is bound.
    /// </summary>
    public string DeviceId { get; }
}
