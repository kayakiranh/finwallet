namespace FinWallet.Application.Registration;

/// <summary>
/// TR: Registration OTP resend işleminin yeni challenge expiration bilgisini ve SMS provider delivery sonucunu taşır.
/// EN: Carries the new challenge expiration and SMS-provider delivery outcome produced by a registration OTP resend operation.
/// </summary>
public sealed class ResendRegistrationOtpResult
{
    /// <summary>
    /// TR: OTP resend sonucunu oluşturur.
    /// EN: Creates the OTP-resend result.
    /// </summary>
    /// <param name="otpExpiresAt">TR: Yeni OTP challenge sona erme UTC zamanı. EN: UTC expiration time of the new OTP challenge.</param>
    /// <param name="otpDeliverySucceeded">TR: FakeCommunication SMS çağrısı başarılıysa true. EN: True when the FakeCommunication SMS call succeeded.</param>
    public ResendRegistrationOtpResult(DateTimeOffset otpExpiresAt, bool otpDeliverySucceeded)
    {
        OtpExpiresAt = otpExpiresAt;
        OtpDeliverySucceeded = otpDeliverySucceeded;
    }

    /// <summary>TR: Yeni OTP challenge sona erme UTC zamanını döndürür. EN: Gets the UTC expiration time of the new OTP challenge.</summary>
    public DateTimeOffset OtpExpiresAt { get; }

    /// <summary>TR: SMS provider çağrısının başarılı olup olmadığını döndürür. EN: Gets whether the SMS-provider call succeeded.</summary>
    public bool OtpDeliverySucceeded { get; }
}
