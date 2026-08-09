namespace FinWallet.Application.Registration;

/// <summary>
/// TR: Aynı registration için sabit resend cooldown süresi dolmadan yeni OTP üretme isteğinin reddedildiğini temsil eder.
/// EN: Represents rejection of a new OTP issuance request before the fixed resend-cooldown period has elapsed for the same registration.
/// </summary>
public sealed class RegistrationOtpRateLimitException : InvalidOperationException
{
    /// <summary>
    /// TR: İstemciye OTP veya Redis state'i hakkında ayrıntı sızdırmayan genel resend-rate-limit hatasını oluşturur.
    /// EN: Creates a generic resend-rate-limit error without exposing OTP or Redis state details to the client.
    /// </summary>
    public RegistrationOtpRateLimitException()
        : base("A new registration verification code cannot be issued yet.")
    {
    }
}
