namespace FakeCampaign.Api.Contracts;

/// <summary>
/// TR: FakeCampaign sağlayıcısının merchant alışverişi için kampanya uygunluğu ve indirim hesaplamasında kullandığı isteği temsil eder.
/// EN: Represents the request used by the FakeCampaign provider to calculate campaign eligibility and discount for a merchant purchase.
/// </summary>
public sealed class CampaignEvaluationRequest
{
    /// <summary>TR: FinWallet müşterisinin internal olmayan dış referansını döndürür veya ayarlar. EN: Gets or sets the non-sensitive external customer reference supplied by FinWallet.</summary>
    public Guid CustomerReference { get; init; }

    /// <summary>TR: Alışveriş yapılan merchant'ın dış kimliğini döndürür veya ayarlar. EN: Gets or sets the external merchant identifier used for the purchase.</summary>
    public string MerchantId { get; init; } = string.Empty;

    /// <summary>TR: Kampanya uygulanmadan önceki pozitif alışveriş tutarını döndürür veya ayarlar. EN: Gets or sets the positive purchase amount before campaign application.</summary>
    public decimal Amount { get; init; }

    /// <summary>TR: Alışveriş para birimi kodunu döndürür veya ayarlar. EN: Gets or sets the purchase currency code.</summary>
    public string Currency { get; init; } = string.Empty;

    /// <summary>TR: Kampanya tarih uygunluğunun değerlendirileceği işlem zamanını döndürür veya ayarlar. EN: Gets or sets the operation timestamp used to evaluate campaign date eligibility.</summary>
    public DateTimeOffset RequestedAt { get; init; }
}
