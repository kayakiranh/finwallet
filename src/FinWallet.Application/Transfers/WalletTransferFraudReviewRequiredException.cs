namespace FinWallet.Application.Transfers;

/// <summary>
/// TR: Fraud birleşim kararı Review olduğunda otomatik finansal posting yapılmadığını ve ek risk incelemesi gerektiğini belirtir.
/// EN: Indicates that automatic financial posting did not occur because the combined fraud decision is Review and additional risk review is required.
/// </summary>
public sealed class WalletTransferFraudReviewRequiredException : Exception
{
    /// <summary>TR: Fraud-review-required hatasını oluşturur. EN: Creates the fraud-review-required failure.</summary>
    public WalletTransferFraudReviewRequiredException()
        : base("The wallet transfer requires additional risk review.")
    {
    }
}
