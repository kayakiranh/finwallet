namespace FinWallet.Application.Authentication;

/// <summary>
/// TR: Daha önce consume edilmiş refresh token'ın tekrar sunulduğunu ve bağlı session'ın güvenlik amacıyla revoke edildiğini ifade eden güvenlik olayını temsil eder.
/// EN: Represents the security event in which a previously consumed refresh token is presented again and the associated session is revoked for protection.
/// </summary>
public sealed class RefreshTokenReuseDetectedException : UnauthorizedAccessException
{
    /// <summary>
    /// TR: Refresh-token reuse tespit hatasını genel istemci mesajıyla oluşturur.
    /// EN: Creates the refresh-token reuse detection error with a generic client-facing message.
    /// </summary>
    public RefreshTokenReuseDetectedException()
        : base("Refresh token reuse was detected and the session was revoked.")
    {
    }
}
