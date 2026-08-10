using FinWallet.Application.Purchases;

namespace FinWallet.Api.Contracts.Purchases;

/// <summary>TR: Merchant purchase için source wallet, merchant ve kampanya öncesi tutarı taşır. EN: Carries source wallet, merchant and pre-campaign amount for a merchant purchase.</summary>
public sealed class PurchaseRequest
{
    /// <summary>TR: Source wallet kimliğini döndürür veya ayarlar. EN: Gets or sets source-wallet identifier.</summary>
    public Guid WalletId { get; init; }
    /// <summary>TR: Merchant kimliğini döndürür veya ayarlar. EN: Gets or sets merchant identifier.</summary>
    public string MerchantId { get; init; } = string.Empty;
    /// <summary>TR: Kampanya uygulanmadan önceki tutarı döndürür veya ayarlar. EN: Gets or sets amount before campaign application.</summary>
    public decimal Amount { get; init; }
}

/// <summary>TR: Completed purchase ve campaign accounting sonucunu public API'ye taşır. EN: Carries completed purchase and campaign-accounting result to the public API.</summary>
public sealed class PurchaseResponse
{
    /// <summary>TR: Application PurchaseResult değerini public response'a dönüştürür. EN: Converts Application PurchaseResult into public response.</summary>
    /// <param name="result">TR: Completed purchase sonucu. EN: Completed purchase result.</param>
    public PurchaseResponse(PurchaseResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        TransactionId = result.TransactionId;
        WalletId = result.WalletId;
        MerchantId = result.MerchantId;
        OriginalAmount = result.OriginalAmount;
        DiscountAmount = result.DiscountAmount;
        FinalAmount = result.FinalAmount;
        Currency = result.Currency.ToString();
        CampaignId = result.CampaignId;
        Sponsor = result.Sponsor?.ToString();
        CompletedAt = result.CompletedAt;
        WasReplay = result.WasReplay;
    }

    /// <summary>TR: FinancialTransaction kimliğini döndürür. EN: Gets FinancialTransaction identifier.</summary>
    public Guid TransactionId { get; }
    /// <summary>TR: Source wallet kimliğini döndürür. EN: Gets source-wallet identifier.</summary>
    public Guid WalletId { get; }
    /// <summary>TR: Merchant kimliğini döndürür. EN: Gets merchant identifier.</summary>
    public string MerchantId { get; }
    /// <summary>TR: Kampanya öncesi tutarı döndürür. EN: Gets pre-campaign amount.</summary>
    public decimal OriginalAmount { get; }
    /// <summary>TR: Discount tutarını döndürür. EN: Gets discount amount.</summary>
    public decimal DiscountAmount { get; }
    /// <summary>TR: Wallet'tan düşülen final tutarı döndürür. EN: Gets final amount debited from wallet.</summary>
    public decimal FinalAmount { get; }
    /// <summary>TR: Currency kodunu döndürür. EN: Gets currency code.</summary>
    public string Currency { get; }
    /// <summary>TR: Uygulanan campaign kimliğini döndürür. EN: Gets applied campaign identifier.</summary>
    public string? CampaignId { get; }
    /// <summary>TR: Campaign sponsor bilgisini döndürür. EN: Gets campaign sponsor.</summary>
    public string? Sponsor { get; }
    /// <summary>TR: Completion UTC zamanını döndürür. EN: Gets completion UTC timestamp.</summary>
    public DateTimeOffset CompletedAt { get; }
    /// <summary>TR: Durable replay bilgisini döndürür. EN: Gets durable-replay state.</summary>
    public bool WasReplay { get; }
}
