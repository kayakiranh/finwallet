namespace FinWallet.Application.Registration;

/// <summary>
/// TR: Kayıt talebinin kalıcı pending customer oluşturduğunu, SMS doğrulama süresini ve ilk OTP gönderiminin provider tarafından başarılı kabul edilip edilmediğini istemci katmanına taşır.
/// EN: Carries the fact that registration created a persistent pending customer, the SMS-verification deadline and whether the initial OTP delivery was accepted successfully by the provider.
/// </summary>
public sealed class RegisterCustomerResult
{
    /// <summary>
    /// TR: Kayıt sonucunu oluşturur.
    /// EN: Creates the registration result.
    /// </summary>
    /// <param name="customerId">TR: SMS doğrulaması bekleyen yeni müşteri kimliği. EN: Identifier of the new customer awaiting SMS verification.</param>
    /// <param name="otpExpiresAt">TR: SMS OTP challenge sona erme UTC zamanı. EN: UTC timestamp at which the SMS OTP challenge expires.</param>
    /// <param name="otpDeliverySucceeded">TR: FakeCommunication SMS çağrısı başarıyla kabul edildiyse true. EN: True when the FakeCommunication SMS call was accepted successfully.</param>
    public RegisterCustomerResult(
        Guid customerId,
        DateTimeOffset otpExpiresAt,
        bool otpDeliverySucceeded)
    {
        CustomerId = customerId;
        OtpExpiresAt = otpExpiresAt;
        OtpDeliverySucceeded = otpDeliverySucceeded;
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

    /// <summary>
    /// TR: İlk OTP SMS gönderiminin provider tarafından başarılı kabul edilip edilmediğini döndürür; false ise istemci resend akışını kullanabilir.
    /// EN: Gets whether the initial OTP SMS delivery was accepted successfully by the provider; when false, the client may use the resend flow.
    /// </summary>
    public bool OtpDeliverySucceeded { get; }
}
