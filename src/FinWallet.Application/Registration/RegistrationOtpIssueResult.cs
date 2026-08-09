namespace FinWallet.Application.Registration;

/// <summary>
/// TR: Kayıt OTP servisinin yalnızca SMS gönderimi için kısa süreli kullanılacak ham kod ve challenge sona erme bilgisini taşır.
/// EN: Carries the raw code used briefly only for SMS delivery and the challenge expiration returned by the registration OTP service.
/// </summary>
public sealed class RegistrationOtpIssueResult
{
    /// <summary>
    /// TR: OTP üretim sonucunu oluşturur; kod hassas güvenlik verisidir ve loglanmamalıdır.
    /// EN: Creates an OTP issuance result; the code is sensitive security data and must not be logged.
    /// </summary>
    /// <param name="code">
    /// TR: Fake SMS servisine iletilecek tek kullanımlık doğrulama kodu.
    /// EN: One-time verification code sent to the fake SMS service.
    /// </param>
    /// <param name="expiresAt">
    /// TR: OTP challenge'ın sona ereceği UTC zaman bilgisi.
    /// EN: UTC timestamp at which the OTP challenge expires.
    /// </param>
    public RegistrationOtpIssueResult(string code, DateTimeOffset expiresAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        Code = code;
        ExpiresAt = expiresAt;
    }

    /// <summary>
    /// TR: SMS gönderimi dışında kullanılmaması ve hiçbir loga yazılmaması gereken ham OTP kodunu döndürür.
    /// EN: Gets the raw OTP code that must only be used for SMS delivery and must never be written to logs.
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// TR: OTP challenge'ın sona ereceği UTC zamanını döndürür.
    /// EN: Gets the UTC expiration time of the OTP challenge.
    /// </summary>
    public DateTimeOffset ExpiresAt { get; }
}
