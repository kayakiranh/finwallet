namespace FinWallet.Application.Registration;

/// <summary>
/// TR: Aktif registration OTP challenge için sabit resend bekleme süresi dolmadan yeni kod istendiğinde oluşan beklenen güvenlik sonucunu temsil eder.
/// EN: Represents the expected security outcome produced when a new registration OTP is requested before the fixed resend cooldown for the active challenge has elapsed.
/// </summary>
public sealed class RegistrationOtpResendTooSoonException : Exception
{
    /// <summary>
    /// TR: Kalan bekleme süresiyle resend cooldown hatasını oluşturur.
    /// EN: Creates the resend-cooldown error with the remaining wait duration.
    /// </summary>
    /// <param name="retryAfter">
    /// TR: Yeni OTP challenge oluşturulmadan önce beklenmesi gereken yaklaşık süre.
    /// EN: Approximate duration that must elapse before a new OTP challenge may be issued.
    /// </param>
    public RegistrationOtpResendTooSoonException(TimeSpan retryAfter)
        : base("A new registration OTP cannot be issued yet.")
    {
        RetryAfter = retryAfter < TimeSpan.Zero ? TimeSpan.Zero : retryAfter;
    }

    /// <summary>
    /// TR: Yeni OTP isteği için beklenmesi gereken yaklaşık süreyi döndürür.
    /// EN: Gets the approximate duration that must elapse before another OTP issuance attempt.
    /// </summary>
    public TimeSpan RetryAfter { get; }
}
