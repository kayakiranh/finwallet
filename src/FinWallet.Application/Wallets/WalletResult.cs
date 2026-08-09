using FinWallet.Domain.Shared;
using FinWallet.Domain.Wallets;

namespace FinWallet.Application.Wallets;

/// <summary>
/// TR: Wallet use-case'lerinin API katmanına taşıdığı provider-bağımsız cüzdan state'ini temsil eder.
/// EN: Represents provider-independent wallet state returned by wallet use cases to the API layer.
/// </summary>
public sealed class WalletResult
{
    /// <summary>TR: Wallet sonucunu domain aggregate'inden oluşturur. EN: Creates a wallet result from the domain aggregate.</summary>
    /// <param name="wallet">TR: Kaynak Wallet aggregate'i. EN: Source Wallet aggregate.</param>
    public WalletResult(Wallet wallet)
    {
        ArgumentNullException.ThrowIfNull(wallet);
        WalletId = wallet.Id;
        Currency = wallet.Currency;
        AvailableBalance = wallet.AvailableBalance;
        BlockedBalance = wallet.BlockedBalance;
        Status = wallet.Status;
        CreatedAt = wallet.CreatedAt;
    }

    /// <summary>TR: Internal wallet kimliğini döndürür. EN: Gets internal wallet identifier.</summary>
    public Guid WalletId { get; }

    /// <summary>TR: Wallet currency değerini döndürür. EN: Gets wallet currency.</summary>
    public CurrencyCode Currency { get; }

    /// <summary>TR: Kullanılabilir bakiye değerini döndürür. EN: Gets available balance.</summary>
    public decimal AvailableBalance { get; }

    /// <summary>TR: Bloke bakiye değerini döndürür. EN: Gets blocked balance.</summary>
    public decimal BlockedBalance { get; }

    /// <summary>TR: Wallet lifecycle durumunu döndürür. EN: Gets wallet lifecycle state.</summary>
    public WalletStatus Status { get; }

    /// <summary>TR: Wallet oluşturulma UTC zamanını döndürür. EN: Gets wallet UTC creation time.</summary>
    public DateTimeOffset CreatedAt { get; }
}
