namespace FinWallet.Application.Transfers;

/// <summary>
/// TR: Destination wallet'ın bulunamadığını belirtir; destination owner kimliği istemciye açıklanmaz.
/// EN: Indicates that the destination wallet was not found; destination-owner identity is not exposed to the client.
/// </summary>
public sealed class WalletTransferDestinationNotFoundException : Exception
{
    /// <summary>TR: Güvenli destination-wallet-not-found hatasını oluşturur. EN: Creates the safe destination-wallet-not-found failure.</summary>
    public WalletTransferDestinationNotFoundException()
        : base("The destination wallet was not found.")
    {
    }
}
