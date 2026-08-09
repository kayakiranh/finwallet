namespace FinWallet.Application.Authentication;

/// <summary>
/// TR: Refresh token'ın bilinmemesi, sona ermesi, revoke edilmesi veya bağlı session/customer state'inin yenilemeye izin vermemesi durumunu tek güvenli hata olarak temsil eder.
/// EN: Represents unknown, expired or revoked refresh tokens, or session/customer state that disallows refresh, as one safe error without revealing additional security state.
/// </summary>
public sealed class InvalidRefreshTokenException : UnauthorizedAccessException
{
    /// <summary>
    /// TR: Güvenlik state'i hakkında ayrıntı sızdırmayan genel invalid-refresh-token hatasını oluşturur.
    /// EN: Creates a generic invalid-refresh-token error that does not leak detailed security state.
    /// </summary>
    public InvalidRefreshTokenException()
        : base("Invalid refresh token.")
    {
    }
}
