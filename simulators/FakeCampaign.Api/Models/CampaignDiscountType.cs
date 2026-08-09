namespace FakeCampaign.Api.Models;

/// <summary>
/// TR: FakeCampaign tanımının indirimi yüzde veya sabit tutar olarak nasıl hesapladığını belirtir.
/// EN: Identifies whether a FakeCampaign definition calculates its discount as a percentage or a fixed amount.
/// </summary>
public enum CampaignDiscountType
{
    /// <summary>TR: İndirim alışveriş tutarının belirli yüzdesi olarak hesaplanır. EN: Discount is calculated as a percentage of the purchase amount.</summary>
    Percentage = 1,

    /// <summary>TR: İndirim sabit para tutarı olarak hesaplanır. EN: Discount is calculated as a fixed monetary amount.</summary>
    FixedAmount = 2
}
