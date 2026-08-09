namespace FinWallet.Application.Transfers;

/// <summary>
/// TR: Source veya destination wallet lifecycle durumunun yeni transfer işlemine izin vermediğini güvenli business conflict olarak belirtir.
/// EN: Indicates as a safe business conflict that source or destination wallet lifecycle state does not allow the new transfer.
/// </summary>
public sealed class WalletTransferUnavailableException : Exception
{
    /// <summary>TR: Wallet-transfer-unavailable hatasını oluşturur. EN: Creates the wallet-transfer-unavailable failure.</summary>
    public WalletTransferUnavailableException()
        : base("One of the wallets is not available for this transfer.")
    {
    }
}
