namespace FinWallet.Application.Transfers;

/// <summary>
/// TR: Source wallet'ın bulunamadığını veya authenticated customer'a ait olmadığını ownership bilgisi sızdırmadan belirtir.
/// EN: Indicates that the source wallet was not found or is not owned by the authenticated customer without leaking ownership information.
/// </summary>
public sealed class WalletTransferSourceNotFoundException : Exception
{
    /// <summary>TR: Güvenli source-wallet-not-found hatasını oluşturur. EN: Creates the safe source-wallet-not-found failure.</summary>
    public WalletTransferSourceNotFoundException()
        : base("The source wallet was not found.")
    {
    }
}
