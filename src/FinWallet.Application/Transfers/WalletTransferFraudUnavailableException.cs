namespace FinWallet.Application.Transfers;

/// <summary>
/// TR: Zorunlu external fraud değerlendirmesi timeout/network/provider hatası nedeniyle tamamlanamadığında wallet transfer'ın fail-closed durduğunu belirtir.
/// EN: Indicates that wallet transfer stopped fail-closed because required external fraud evaluation could not complete due to timeout, network or provider failure.
/// </summary>
public sealed class WalletTransferFraudUnavailableException : Exception
{
    /// <summary>TR: Fraud-unavailable hatasını inner exception ile oluşturur. EN: Creates the fraud-unavailable failure with its inner exception.</summary>
    /// <param name="innerException">TR: Tanılama amacıyla saklanan provider exception; API'ye doğrudan dönülmez. EN: Provider exception retained for diagnostics and never returned directly by the API.</param>
    public WalletTransferFraudUnavailableException(Exception innerException)
        : base("Required fraud evaluation is temporarily unavailable.", innerException)
    {
        ArgumentNullException.ThrowIfNull(innerException);
    }
}
