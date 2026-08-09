namespace FinWallet.Application.Transfers;

/// <summary>
/// TR: Aynı idempotency key için durable Processing kaydı bulunduğunda yeni finansal etki uygulanmadan mevcut işlemin halen devam ettiğini belirtir.
/// EN: Indicates that a durable Processing record already exists for the same idempotency key so no new financial effect is applied while the existing operation is still in progress.
/// </summary>
public sealed class WalletTransferInProgressException : Exception
{
    /// <summary>TR: Transfer-in-progress hatasını oluşturur. EN: Creates the transfer-in-progress failure.</summary>
    public WalletTransferInProgressException()
        : base("The wallet transfer is already in progress.")
    {
    }
}
