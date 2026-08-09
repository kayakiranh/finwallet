using FakeCampaign.Api.Contracts;
using FakeCampaign.Api.Models;

namespace FakeCampaign.Api.Services;

/// <summary>
/// TR: FakeCampaign simulatorının deterministic merchant/currency/tarih uygunluğu ve indirim hesaplama motorudur; herhangi bir FinWallet ledger veya bakiye state'i değiştirmez.
/// EN: Deterministic merchant/currency/date eligibility and discount-calculation engine for the FakeCampaign simulator; it never changes FinWallet ledger or balance state.
/// </summary>
public sealed class CampaignEvaluationService
{
    private static readonly IReadOnlyCollection<CampaignDefinition> Campaigns = CreateCampaigns();

    /// <summary>
    /// TR: Merchant alışverişini simulator kampanya seed'leriyle değerlendirir ve en yüksek geçerli indirimi seçerek sponsor bilgisiyle birlikte döndürür.
    /// EN: Evaluates a merchant purchase against simulator campaign seeds and returns the highest eligible discount together with sponsor information.
    /// </summary>
    /// <param name="request">TR: Kampanya değerlendirmesi yapılacak merchant alışveriş isteği. EN: Merchant-purchase request to evaluate for campaigns.</param>
    /// <returns>TR: Kampanya uygunluğu, indirim, final tutar ve sponsor kararını döndürür. EN: Returns campaign eligibility, discount, final amount and sponsor decision.</returns>
    /// <exception cref="ArgumentException">TR: Zorunlu string/kimlik alanları geçersizse oluşur. EN: Thrown when required string/identifier fields are invalid.</exception>
    /// <exception cref="ArgumentOutOfRangeException">TR: Alışveriş tutarı pozitif değilse oluşur. EN: Thrown when the purchase amount is not positive.</exception>
    public CampaignEvaluationResponse Evaluate(CampaignEvaluationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.CustomerReference == Guid.Empty)
        {
            throw new ArgumentException("Customer reference cannot be empty.", nameof(request));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(request.MerchantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Currency);

        if (request.Amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Purchase amount must be positive.");
        }

        var merchantId = request.MerchantId.Trim();
        var currency = request.Currency.Trim().ToUpperInvariant();
        var requestDate = DateOnly.FromDateTime(request.RequestedAt.UtcDateTime);
        var reference = Guid.NewGuid();

        var candidates = Campaigns
            .Where(campaign =>
                string.Equals(campaign.MerchantId, merchantId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(campaign.Currency, currency, StringComparison.OrdinalIgnoreCase)
                && requestDate >= campaign.StartDate
                && requestDate <= campaign.EndDate)
            .ToArray();

        if (candidates.Length == 0)
        {
            return NoDiscount(reference, request.Amount, currency, "NO_ELIGIBLE_CAMPAIGN");
        }

        var amountEligibleCampaigns = candidates
            .Where(campaign => request.Amount >= campaign.MinimumAmount)
            .Select(campaign => new
            {
                Campaign = campaign,
                Discount = campaign.CalculateDiscount(request.Amount)
            })
            .OrderByDescending(static evaluation => evaluation.Discount)
            .ThenBy(static evaluation => evaluation.Campaign.Id, StringComparer.Ordinal)
            .ToArray();

        if (amountEligibleCampaigns.Length == 0)
        {
            return NoDiscount(reference, request.Amount, currency, "MINIMUM_AMOUNT_NOT_MET");
        }

        var selected = amountEligibleCampaigns[0];
        var finalAmount = request.Amount - selected.Discount;

        return new CampaignEvaluationResponse(
            reference,
            true,
            selected.Campaign.Id,
            request.Amount,
            selected.Discount,
            finalAmount,
            currency,
            selected.Campaign.SponsorType,
            "CAMPAIGN_APPLIED");
    }

    /// <summary>
    /// TR: Kampanya uygulanmadığı durumda tutarı değiştirmeyen standart provider yanıtını üretir.
    /// EN: Creates the standard provider response that leaves the amount unchanged when no campaign applies.
    /// </summary>
    /// <param name="reference">TR: Provider değerlendirme referansı. EN: Provider evaluation reference.</param>
    /// <param name="amount">TR: Orijinal alışveriş tutarı. EN: Original purchase amount.</param>
    /// <param name="currency">TR: Alışveriş para birimi. EN: Purchase currency.</param>
    /// <param name="reason">TR: Kampanya uygulanmama nedeni. EN: Reason why no campaign was applied.</param>
    /// <returns>TR: Sıfır indirimli provider yanıtını döndürür. EN: Returns the provider response with zero discount.</returns>
    private static CampaignEvaluationResponse NoDiscount(Guid reference, decimal amount, string currency, string reason)
    {
        return new CampaignEvaluationResponse(
            reference,
            false,
            null,
            amount,
            0m,
            amount,
            currency,
            null,
            reason);
    }

    /// <summary>
    /// TR: Simulatorın başlangıç merchant kampanyalarını oluşturur; tanımlar production kampanya verisi değil deterministic test seed'idir.
    /// EN: Creates the simulator's initial merchant campaigns; definitions are deterministic test seeds rather than production campaign data.
    /// </summary>
    /// <returns>TR: Fake provider kampanya seed koleksiyonunu döndürür. EN: Returns the fake-provider campaign seed collection.</returns>
    private static IReadOnlyCollection<CampaignDefinition> CreateCampaigns()
    {
        return new[]
        {
            new CampaignDefinition(
                "CMP-COFFEE-10",
                "MRC-COFFEE-001",
                "TRY",
                new DateOnly(2026, 1, 1),
                new DateOnly(2030, 12, 31),
                200m,
                CampaignDiscountType.Percentage,
                10m,
                100m,
                CampaignSponsorType.Platform),
            new CampaignDefinition(
                "CMP-ELECTRONICS-5",
                "MRC-ELECTRONICS-001",
                "TRY",
                new DateOnly(2026, 1, 1),
                new DateOnly(2030, 12, 31),
                1_000m,
                CampaignDiscountType.Percentage,
                5m,
                500m,
                CampaignSponsorType.Merchant),
            new CampaignDefinition(
                "CMP-TRAVEL-EUR-20",
                "MRC-TRAVEL-001",
                "EUR",
                new DateOnly(2026, 1, 1),
                new DateOnly(2030, 12, 31),
                100m,
                CampaignDiscountType.FixedAmount,
                20m,
                20m,
                CampaignSponsorType.Merchant)
        };
    }
}
