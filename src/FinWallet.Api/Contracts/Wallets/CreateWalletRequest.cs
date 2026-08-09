namespace FinWallet.Api.Contracts.Wallets;

/// <summary>
/// TR: Authenticated customer için oluşturulacak wallet currency kodunu taşıyan Web API request modelidir.
/// EN: Web API request model carrying the wallet currency code to create for an authenticated customer.
/// </summary>
public sealed class CreateWalletRequest
{
    /// <summary>TR: `TRY`, `USD` veya `EUR` currency kodunu döndürür veya ayarlar. EN: Gets or sets the `TRY`, `USD` or `EUR` currency code.</summary>
    public string Currency { get; init; } = string.Empty;
}
