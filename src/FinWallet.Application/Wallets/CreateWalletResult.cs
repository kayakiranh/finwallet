namespace FinWallet.Application.Wallets;

/// <summary>
/// TR: Wallet create use-case sonucunda güncel wallet state'i ve bu request'in yeni kayıt oluşturup oluşturmadığını taşır.
/// EN: Carries current wallet state and whether this request created a new record after the wallet-create use case.
/// </summary>
public sealed class CreateWalletResult
{
    /// <summary>TR: Create-wallet sonucunu oluşturur. EN: Creates the create-wallet result.</summary>
    /// <param name="wallet">TR: Güncel wallet sonucu. EN: Current wallet result.</param>
    /// <param name="wasCreated">TR: Bu request yeni durable wallet oluşturduysa true. EN: True when this request created the durable wallet.</param>
    public CreateWalletResult(WalletResult wallet, bool wasCreated)
    {
        Wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
        WasCreated = wasCreated;
    }

    /// <summary>TR: Güncel wallet sonucunu döndürür. EN: Gets current wallet result.</summary>
    public WalletResult Wallet { get; }

    /// <summary>TR: Bu request yeni wallet kaydı oluşturduysa true döndürür. EN: Gets whether this request created a new wallet record.</summary>
    public bool WasCreated { get; }
}
