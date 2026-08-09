namespace FinWallet.Application.Transfers;

/// <summary>
/// TR: JWT geçerli görünse bile para hareketi için server-side session'ın bulunamadığını, expire veya revoke olduğunu belirtir.
/// EN: Indicates that the server-side session required for a money movement is missing, expired or revoked even when the JWT itself appears valid.
/// </summary>
public sealed class WalletTransferSessionInvalidException : Exception
{
    /// <summary>TR: Güvenli transfer-session-invalid hatasını oluşturur. EN: Creates the safe transfer-session-invalid failure.</summary>
    public WalletTransferSessionInvalidException()
        : base("The authenticated session is no longer valid for financial operations.")
    {
    }
}
