using FinWallet.Application.Wallets;

namespace FinWallet.Api.Contracts.Wallets;

/// <summary>
/// TR: Wallet kimliği, currency, bakiye ve lifecycle state'ini dış Web API sözleşmesinde temsil eder.
/// EN: Represents wallet identifier, currency, balances and lifecycle state in the external Web API contract.
/// </summary>
public sealed class WalletResponse
{
    /// <summary>TR: Application wallet sonucunu dış API response modeline dönüştürür. EN: Converts an Application wallet result into the external API response model.</summary>
    /// <param name="result">TR: Application katmanından gelen wallet sonucu. EN: Wallet result returned by the Application layer.</param>
    public WalletResponse(WalletResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        WalletId = result.WalletId;
        Currency = result.Currency.ToString();
        AvailableBalance = result.AvailableBalance;
        BlockedBalance = result.BlockedBalance;
        Status = result.Status.ToString();
        CreatedAt = result.CreatedAt;
    }

    /// <summary>TR: Internal wallet kimliğini döndürür. EN: Gets internal wallet identifier.</summary>
    public Guid WalletId { get; }

    /// <summary>TR: Wallet currency kodunu metin olarak döndürür. EN: Gets wallet currency code as text.</summary>
    public string Currency { get; }

    /// <summary>TR: Kullanılabilir bakiye değerini döndürür. EN: Gets available balance.</summary>
    public decimal AvailableBalance { get; }

    /// <summary>TR: Bloke bakiye değerini döndürür. EN: Gets blocked balance.</summary>
    public decimal BlockedBalance { get; }

    /// <summary>TR: Wallet lifecycle durumunu metin olarak döndürür. EN: Gets wallet lifecycle state as text.</summary>
    public string Status { get; }

    /// <summary>TR: Wallet oluşturulma UTC zamanını döndürür. EN: Gets wallet UTC creation time.</summary>
    public DateTimeOffset CreatedAt { get; }
}
