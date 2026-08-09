namespace FakeCampaign.Api.Models;

/// <summary>
/// TR: Kampanya indiriminin ekonomik maliyetini hangi tarafın üstlendiğini belirtir; FinWallet ledger muhasebesi bu bilgiyi kullanır.
/// EN: Identifies which party economically funds the campaign discount; FinWallet ledger accounting uses this information.
/// </summary>
public enum CampaignSponsorType
{
    /// <summary>TR: İndirimi FinWallet/platform karşılar. EN: The FinWallet/platform funds the discount.</summary>
    Platform = 1,

    /// <summary>TR: İndirimi merchant kendi alacağından karşılar. EN: The merchant funds the discount from its own receivable.</summary>
    Merchant = 2
}
