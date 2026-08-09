namespace FinWallet.Application.Registration;

/// <summary>
/// TR: Kayıt talebinin kalıcı pending customer oluşturduğunu ve SMS doğrulamasının hangi zamana kadar tamamlanması gerektiğini istemci katmanına taşır.
/// EN: Carries the fact that registration created a persistent pending customer and the deadline by which SMS verification must be completed.
/// </summary>
public sealed class RegisterCustomerResult
{
    /// <summary>
    /// TR: Kayıt sonucunu oluşturur.
    /// EN: Creates the registration result.
    /// </summary>
    /// <param name="customerId">
    /// TR: SMS doğrulaması bekleyen yeni müşterinin kimliği.
    /// EN: Identifier of the new customer awaiting SMS verification.
    /// </param>
    /// <param name="otpExpiresAt">
    /// TR: SMS OTP doğrulamasının sona ereceği UTC zaman bilgisi.
    /// EN: UTC timestamp at which the SMS OTP challenge expires.
    /// </param>
    public RegisterCustomerResult(Guid customerId, DateTimeOffset otpExpiresAt)
    {
        CustomerId = customerId;
        OtpExpiresAt = otpExpiresAt;
    }

    /// <summary>
    /// TR: SMS doğrulaması bekleyen müşteri kimliğini döndürür.
    /// EN: Gets the identifier of the customer awaiting SMS verification.
    /// </summary>
    public Guid CustomerId { get; }

    /// <summary>
    /// TR: OTP doğrulamasının sona ereceği UTC zamanını döndürür.
    /// EN: Gets the UTC expiration time of the OTP challenge.
    /// </summary>
    public DateTimeOffset OtpExpiresAt { get; }
}
