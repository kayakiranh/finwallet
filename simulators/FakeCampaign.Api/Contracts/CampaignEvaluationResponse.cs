using FakeCampaign.Api.Models;

namespace FakeCampaign.Api.Contracts;

/// <summary>
/// TR: FakeCampaign sağlayıcısının merchant alışverişi için verdiği kampanya uygunluğu, indirim, final tutar ve sponsor kararını temsil eder.
/// EN: Represents the FakeCampaign provider decision containing campaign eligibility, discount, final amount and sponsor information for a merchant purchase.
/// </summary>
public sealed class CampaignEvaluationResponse
{
    /// <summary>
    /// TR: Kampanya değerlendirme yanıtını oluşturur.
    /// EN: Creates the campaign-evaluation response.
    /// </summary>
    /// <param name="providerReference">TR: Fake provider değerlendirme referansı. EN: Fake-provider evaluation reference.</param>
    /// <param name="eligible">TR: Kampanya uygulanabilirlik sonucu. EN: Campaign eligibility result.</param>
    /// <param name="campaignId">TR: Eşleşen kampanya kimliği; uygun değilse null. EN: Matching campaign identifier, or null when ineligible.</param>
    /// <param name="originalAmount">TR: İndirim öncesi alışveriş tutarı. EN: Purchase amount before discount.</param>
    /// <param name="discountAmount">TR: Hesaplanan kampanya indirimi. EN: Calculated campaign discount.</param>
    /// <param name="finalAmount">TR: Müşteri için indirim sonrası tutar. EN: Customer amount after discount.</param>
    /// <param name="currency">TR: Tutarların para birimi. EN: Currency of the amounts.</param>
    /// <param name="sponsorType">TR: İndirimin ekonomik sponsor'u; uygun değilse null. EN: Economic sponsor of the discount, or null when ineligible.</param>
    /// <param name="reason">TR: Kampanya kararının makine-okunabilir nedeni. EN: Machine-readable reason for the campaign decision.</param>
    public CampaignEvaluationResponse(
        Guid providerReference,
        bool eligible,
        string? campaignId,
        decimal originalAmount,
        decimal discountAmount,
        decimal finalAmount,
        string currency,
        CampaignSponsorType? sponsorType,
        string reason)
    {
        ProviderReference = providerReference;
        Eligible = eligible;
        CampaignId = campaignId;
        OriginalAmount = originalAmount;
        DiscountAmount = discountAmount;
        FinalAmount = finalAmount;
        Currency = currency;
        SponsorType = sponsorType;
        Reason = reason;
    }

    /// <summary>TR: Fake provider değerlendirme referansını döndürür. EN: Gets the fake-provider evaluation reference.</summary>
    public Guid ProviderReference { get; }

    /// <summary>TR: Kampanyanın işlem için uygun olup olmadığını döndürür. EN: Gets whether a campaign is eligible for the operation.</summary>
    public bool Eligible { get; }

    /// <summary>TR: Eşleşen kampanya kimliğini; uygun değilse null değerini döndürür. EN: Gets the matching campaign identifier, or null when ineligible.</summary>
    public string? CampaignId { get; }

    /// <summary>TR: İndirim öncesi alışveriş tutarını döndürür. EN: Gets the purchase amount before discount.</summary>
    public decimal OriginalAmount { get; }

    /// <summary>TR: Provider tarafından hesaplanan indirim tutarını döndürür. EN: Gets the discount amount calculated by the provider.</summary>
    public decimal DiscountAmount { get; }

    /// <summary>TR: İndirim sonrası müşteri tutarını döndürür. EN: Gets the customer amount after discount.</summary>
    public decimal FinalAmount { get; }

    /// <summary>TR: Tutarların para birimi kodunu döndürür. EN: Gets the currency code of the amounts.</summary>
    public string Currency { get; }

    /// <summary>TR: İndirimi finanse edecek tarafı; kampanya yoksa null değerini döndürür. EN: Gets the party funding the discount, or null when no campaign applies.</summary>
    public CampaignSponsorType? SponsorType { get; }

    /// <summary>TR: Kampanya kararının makine-okunabilir nedenini döndürür. EN: Gets the machine-readable reason for the campaign decision.</summary>
    public string Reason { get; }
}
