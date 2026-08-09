namespace FinWallet.Application.Authentication;

/// <summary>
/// TR: Telefon/parola eşleşmesinin başarısız olduğunu kullanıcı varlığı veya müşteri durumu hakkında ek bilgi sızdırmadan ifade eden authentication hatasını temsil eder.
/// EN: Represents an authentication error indicating failed phone/password verification without leaking additional information about customer existence or state.
/// </summary>
public sealed class InvalidCredentialsException : UnauthorizedAccessException
{
    /// <summary>
    /// TR: Enumeration riskini azaltmak için sabit ve genel invalid-credentials mesajıyla hatayı oluşturur.
    /// EN: Creates the error with a fixed generic invalid-credentials message to reduce enumeration risk.
    /// </summary>
    public InvalidCredentialsException()
        : base("Invalid credentials.")
    {
    }
}
