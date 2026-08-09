namespace FinWallet.Api.Contracts.Authentication;

/// <summary>
/// TR: Başarılı registration talebinden sonra pending müşteri kimliği, SMS OTP expiration bilgisi ve ilk SMS delivery sonucunu istemciye döndüren API sözleşmesini tanımlar.
/// EN: Defines the API response returning the pending-customer identifier, SMS OTP expiration and initial SMS-delivery outcome after a successful registration request.
/// </summary>
public sealed class RegisterCustomerResponse
{
    /// <summary>
    /// TR: Registration response nesnesini oluşturur.
    /// EN: Creates the registration response object.
    /// </summary>
    /// <param name="customerId">TR: SMS doğrulaması bekleyen müşteri kimliği. EN: Identifier of the customer awaiting SMS verification.</param>
    /// <param name="otpExpiresAt">TR: OTP challenge sona erme UTC zamanı. EN: UTC expiration time of the OTP challenge.</param>
    /// <param name="otpDeliverySucceeded">TR: İlk FakeCommunication SMS çağrısı başarılıysa true. EN: True when the initial FakeCommunication SMS call succeeded.</param>
    public RegisterCustomerResponse(
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
    /// TR: OTP challenge'ın sona ereceği UTC zamanını döndürür.
    /// EN: Gets the UTC time at which the OTP challenge expires.
    /// </summary>
    public DateTimeOffset OtpExpiresAt { get; }

    /// <summary>
    /// TR: İlk OTP SMS çağrısının provider tarafından başarılı kabul edilip edilmediğini döndürür; false ise resend endpoint'i kullanılabilir.
    /// EN: Gets whether the initial OTP SMS call was accepted successfully by the provider; when false, the resend endpoint may be used.
    /// </summary>
    public bool OtpDeliverySucceeded { get; }
}
