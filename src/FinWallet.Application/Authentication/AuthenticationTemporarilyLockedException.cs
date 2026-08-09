namespace FinWallet.Application.Authentication;

/// <summary>
/// TR: Sabit başarısız-login eşiği nedeniyle credential'ın geçici olarak yeni login denemelerini kabul etmediğini ifade eden authentication hatasını temsil eder.
/// EN: Represents an authentication error indicating that the credential temporarily rejects new login attempts because the fixed failed-login threshold was reached.
/// </summary>
public sealed class AuthenticationTemporarilyLockedException : UnauthorizedAccessException
{
    /// <summary>
    /// TR: Geçici kilit hatasını oluşturur; kesin unlock zamanı istemci mesajında paylaşılmaz.
    /// EN: Creates the temporary-lock error; the exact unlock time is not exposed in the client-facing message.
    /// </summary>
    public AuthenticationTemporarilyLockedException()
        : base("Authentication is temporarily unavailable for this credential.")
    {
    }
}
