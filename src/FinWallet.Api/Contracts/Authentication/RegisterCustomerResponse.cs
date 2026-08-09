namespace FinWallet.Api.Contracts.Authentication;

/// <summary>
/// TR: Başarılı registration talebinden sonra pending müşteri kimliği ve SMS OTP expiration bilgisini istemciye döndüren API sözleşmesini tanımlar.
/// EN: Defines the API response returning the pending-customer identifier and SMS OTP expiration after a successful registration request.
/// </summary>
public sealed class RegisterCustomerResponse
{
    /// <summary>
    /// TR: Registration response nesnesini oluşturur.
    /// EN: Creates the registration response object.
    /// </summary>
    /// <param name="customerId">TR: SMS doğrulaması bekleyen müşteri kimliği. EN: Identifier of the customer awaiting SMS verification.</param>
    /// <param name="otpExpiresAt">TR: OTP challenge sona erme UTC zamanı. EN: UTC expiration time of the OTP challenge.</param>
    public RegisterCustomerResponse(Guid customerId, DateTimeOffset otpExpiresAt)
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
    /// TR: OTP challenge'ın sona ereceği UTC zamanını döndürür.
    /// EN: Gets the UTC time at which the OTP challenge expires.
    /// </summary>
    public DateTimeOffset OtpExpiresAt { get; }
}
