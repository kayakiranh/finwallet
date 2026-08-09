namespace FinWallet.Application.Registration;

/// <summary>
/// TR: Registration OTP kodunun yanlış, süresi geçmiş veya daha önce tüketilmiş olması durumunu ayrıntı sızdırmadan temsil eder.
/// EN: Represents an incorrect, expired or previously consumed registration OTP without leaking detailed verification state.
/// </summary>
public sealed class InvalidRegistrationOtpException : UnauthorizedAccessException
{
    /// <summary>
    /// TR: Genel registration OTP doğrulama hatasını oluşturur.
    /// EN: Creates the generic registration OTP verification error.
    /// </summary>
    public InvalidRegistrationOtpException()
        : base("Invalid or expired registration verification code.")
    {
    }
}
