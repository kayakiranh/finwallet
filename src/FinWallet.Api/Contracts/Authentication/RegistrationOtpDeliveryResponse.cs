namespace FinWallet.Api.Contracts.Authentication;

/// <summary>
/// TR: Registration OTP üretim/resend işleminin expiration ve SMS delivery sonucunu istemciye döndüren API sözleşmesini tanımlar.
/// EN: Defines the API response returning expiration and SMS-delivery outcome for a registration OTP issuance or resend operation.
/// </summary>
public sealed class RegistrationOtpDeliveryResponse
{
    /// <summary>
    /// TR: OTP delivery response nesnesini oluşturur.
    /// EN: Creates the OTP-delivery response object.
    /// </summary>
    /// <param name="otpExpiresAt">TR: OTP challenge sona erme UTC zamanı. EN: UTC expiration time of the OTP challenge.</param>
    /// <param name="otpDeliverySucceeded">TR: FakeCommunication SMS çağrısı başarılıysa true. EN: True when the FakeCommunication SMS call succeeded.</param>
    public RegistrationOtpDeliveryResponse(DateTimeOffset otpExpiresAt, bool otpDeliverySucceeded)
    {
        OtpExpiresAt = otpExpiresAt;
        OtpDeliverySucceeded = otpDeliverySucceeded;
    }

    /// <summary>TR: OTP challenge sona erme UTC zamanını döndürür. EN: Gets the UTC expiration time of the OTP challenge.</summary>
    public DateTimeOffset OtpExpiresAt { get; }

    /// <summary>TR: SMS provider çağrısının başarılı olup olmadığını döndürür. EN: Gets whether the SMS-provider call succeeded.</summary>
    public bool OtpDeliverySucceeded { get; }
}
