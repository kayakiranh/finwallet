namespace FinWallet.Application.Transfers;

/// <summary>
/// TR: Internal + external fraud karar birleşimi wallet transfer'ı Deny ettiğinde finansal posting başlamadan oluşan güvenli business hatasını temsil eder.
/// EN: Represents the safe business failure raised before financial posting when the combined internal + external fraud decision Denies a wallet transfer.
/// </summary>
public sealed class WalletTransferFraudDeniedException : Exception
{
    /// <summary>TR: Fraud-denied hatasını oluşturur. EN: Creates the fraud-denied failure.</summary>
    public WalletTransferFraudDeniedException()
        : base("The wallet transfer was denied by risk controls.")
    {
    }
}
