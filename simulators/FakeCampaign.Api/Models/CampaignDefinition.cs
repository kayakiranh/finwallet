namespace FakeCampaign.Api.Models;

/// <summary>
/// TR: FakeCampaign simulatorında merchant, currency, tarih, minimum tutar, indirim ve sponsor kurallarını tek deterministic kampanya tanımında toplar.
/// EN: Groups merchant, currency, date, minimum-amount, discount and sponsor rules into one deterministic campaign definition in the FakeCampaign simulator.
/// </summary>
public sealed class CampaignDefinition
{
    /// <summary>
    /// TR: Kampanya tanımını oluşturur ve indirim parametrelerinin pozitif/tutarlı olmasını zorunlu kılar.
    /// EN: Creates a campaign definition and requires its discount parameters to be positive and internally consistent.
    /// </summary>
    /// <param name="id">TR: Provider kampanya kimliği. EN: Provider campaign identifier.</param>
    /// <param name="merchantId">TR: Kampanyanın uygulanabildiği merchant kimliği. EN: Merchant identifier eligible for the campaign.</param>
    /// <param name="currency">TR: Kampanyanın para birimi. EN: Currency of the campaign.</param>
    /// <param name="startDate">TR: Kampanyanın başlangıç tarihi. EN: Campaign start date.</param>
    /// <param name="endDate">TR: Kampanyanın bitiş tarihi. EN: Campaign end date.</param>
    /// <param name="minimumAmount">TR: Kampanya için gereken minimum alışveriş tutarı. EN: Minimum purchase amount required for the campaign.</param>
    /// <param name="discountType">TR: Yüzde veya sabit tutar indirim tipi. EN: Percentage or fixed-amount discount type.</param>
    /// <param name="discountValue">TR: İndirim oranı veya sabit indirim tutarı. EN: Discount rate or fixed discount amount.</param>
    /// <param name="maximumDiscount">TR: Tek işlemde uygulanabilecek maksimum indirim. EN: Maximum discount applicable to one transaction.</param>
    /// <param name="sponsorType">TR: İndirimin maliyetini karşılayan taraf. EN: Party funding the discount.</param>
    public CampaignDefinition(
        string id,
        string merchantId,
        string currency,
        DateOnly startDate,
        DateOnly endDate,
        decimal minimumAmount,
        CampaignDiscountType discountType,
        decimal discountValue,
        decimal maximumDiscount,
        CampaignSponsorType sponsorType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(merchantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);

        if (endDate < startDate)
        {
            throw new ArgumentException("Campaign end date cannot be before start date.", nameof(endDate));
        }

        if (minimumAmount < 0 || discountValue <= 0 || maximumDiscount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(discountValue), "Campaign monetary values must be positive and minimum amount cannot be negative.");
        }

        Id = id.Trim();
        MerchantId = merchantId.Trim();
        Currency = currency.Trim().ToUpperInvariant();
        StartDate = startDate;
        EndDate = endDate;
        MinimumAmount = minimumAmount;
        DiscountType = discountType;
        DiscountValue = discountValue;
        MaximumDiscount = maximumDiscount;
        SponsorType = sponsorType;
    }

    /// <summary>TR: Kampanyanın dış kimliğini döndürür. EN: Gets the external campaign identifier.</summary>
    public string Id { get; }

    /// <summary>TR: Kampanyanın geçerli olduğu merchant kimliğini döndürür. EN: Gets the merchant identifier for which the campaign is valid.</summary>
    public string MerchantId { get; }

    /// <summary>TR: Kampanyanın geçerli olduğu para birimini döndürür. EN: Gets the currency in which the campaign is valid.</summary>
    public string Currency { get; }

    /// <summary>TR: Kampanyanın ilk geçerli yerel takvim tarihini döndürür. EN: Gets the first calendar date on which the campaign is valid.</summary>
    public DateOnly StartDate { get; }

    /// <summary>TR: Kampanyanın son geçerli yerel takvim tarihini döndürür. EN: Gets the last calendar date on which the campaign is valid.</summary>
    public DateOnly EndDate { get; }

    /// <summary>TR: Kampanyanın uygulanması için gereken minimum alışveriş tutarını döndürür. EN: Gets the minimum purchase amount required for the campaign.</summary>
    public decimal MinimumAmount { get; }

    /// <summary>TR: İndirim hesaplama tipini döndürür. EN: Gets the discount calculation type.</summary>
    public CampaignDiscountType DiscountType { get; }

    /// <summary>TR: Yüzde oranı veya sabit tutar anlamına gelen indirim değerini döndürür. EN: Gets the discount value representing either a percentage rate or fixed amount.</summary>
    public decimal DiscountValue { get; }

    /// <summary>TR: Tek işlemde uygulanabilecek maksimum indirim tutarını döndürür. EN: Gets the maximum discount amount that may be applied to one transaction.</summary>
    public decimal MaximumDiscount { get; }

    /// <summary>TR: Kampanya maliyetini kimin karşılayacağını döndürür. EN: Gets the party responsible for funding the campaign discount.</summary>
    public CampaignSponsorType SponsorType { get; }

    /// <summary>
    /// TR: Alışveriş tutarına kampanya indirim formülünü uygular, maksimum indirim sınırını ve tutarın altına düşmeme kuralını korur.
    /// EN: Applies the campaign discount formula to the purchase amount while enforcing the maximum-discount cap and never exceeding the purchase amount.
    /// </summary>
    /// <param name="amount">TR: İndirim öncesi pozitif alışveriş tutarı. EN: Positive purchase amount before discount.</param>
    /// <returns>TR: Kampanya kapsamında uygulanacak indirim tutarını döndürür. EN: Returns the discount amount applied under the campaign.</returns>
    public decimal CalculateDiscount(decimal amount)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount));
        }

        var calculated = DiscountType switch
        {
            CampaignDiscountType.Percentage => amount * (DiscountValue / 100m),
            CampaignDiscountType.FixedAmount => DiscountValue,
            _ => throw new InvalidOperationException("Unsupported campaign discount type.")
        };

        return Math.Min(amount, Math.Min(calculated, MaximumDiscount));
    }
}
