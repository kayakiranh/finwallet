using FinWallet.Domain.Shared;

namespace FinWallet.Application.Campaigns;

/// <summary>TR: Kampanya indirim maliyetini hangi tarafın karşıladığını provider bağımsız tanımlar. EN: Defines which party funds a campaign discount independently from the provider.</summary>
public enum CampaignSponsor
{
    /// <summary>TR: İndirimi platform karşılar. EN: The platform funds the discount.</summary>
    Platform = 1,
    /// <summary>TR: İndirimi merchant karşılar. EN: The merchant funds the discount.</summary>
    Merchant = 2
}

/// <summary>TR: FakeCampaign veya gerçek provider'dan gelen ve ledger accounting'de kullanılacak kampanya kararını taşır. EN: Carries the campaign decision from FakeCampaign or a real provider for ledger accounting.</summary>
public sealed class CampaignEvaluationResult
{
    /// <summary>TR: Provider bağımsız kampanya sonucunu oluşturur. EN: Creates the provider-independent campaign result.</summary>
    /// <param name="referenceId">TR: Provider değerlendirme referansı. EN: Provider evaluation reference.</param>
    /// <param name="eligible">TR: Kampanya uygulanıp uygulanmadığını belirtir. EN: Indicates whether a campaign applies.</param>
    /// <param name="campaignId">TR: Uygulanan kampanya kimliği veya null. EN: Applied campaign identifier or null.</param>
    /// <param name="originalAmount">TR: İndirim öncesi tutar. EN: Amount before discount.</param>
    /// <param name="discountAmount">TR: İndirim tutarı. EN: Discount amount.</param>
    /// <param name="finalAmount">TR: Müşteri tarafından ödenecek tutar. EN: Amount payable by the customer.</param>
    /// <param name="currency">TR: Tutar currency'si. EN: Amount currency.</param>
    /// <param name="sponsor">TR: İndirim sponsor'u veya null. EN: Discount sponsor or null.</param>
    /// <param name="reason">TR: Machine-readable provider nedeni. EN: Machine-readable provider reason.</param>
    public CampaignEvaluationResult(Guid referenceId, bool eligible, string? campaignId, decimal originalAmount, decimal discountAmount, decimal finalAmount, CurrencyCode currency, CampaignSponsor? sponsor, string reason)
    {
        if (referenceId == Guid.Empty) throw new ArgumentException("Campaign reference cannot be empty.", nameof(referenceId));
        if (originalAmount <= 0m) throw new ArgumentOutOfRangeException(nameof(originalAmount));
        if (discountAmount < 0m || discountAmount > originalAmount) throw new ArgumentOutOfRangeException(nameof(discountAmount));
        if (finalAmount != originalAmount - discountAmount) throw new ArgumentException("Final amount must equal original amount minus discount.", nameof(finalAmount));
        if (eligible && (string.IsNullOrWhiteSpace(campaignId) || sponsor is null)) throw new ArgumentException("Eligible campaign requires campaign identifier and sponsor.");
        if (!eligible && (discountAmount != 0m || sponsor is not null)) throw new ArgumentException("Ineligible campaign cannot carry a discount or sponsor.");
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        ReferenceId = referenceId;
        Eligible = eligible;
        CampaignId = string.IsNullOrWhiteSpace(campaignId) ? null : campaignId.Trim();
        OriginalAmount = originalAmount;
        DiscountAmount = discountAmount;
        FinalAmount = finalAmount;
        Currency = currency;
        Sponsor = sponsor;
        Reason = reason.Trim();
    }

    /// <summary>TR: Provider değerlendirme referansını döndürür. EN: Gets provider evaluation reference.</summary>
    public Guid ReferenceId { get; }
    /// <summary>TR: Kampanyanın uygulanabilirliğini döndürür. EN: Gets campaign eligibility.</summary>
    public bool Eligible { get; }
    /// <summary>TR: Kampanya kimliğini veya null değerini döndürür. EN: Gets campaign identifier or null.</summary>
    public string? CampaignId { get; }
    /// <summary>TR: İndirim öncesi tutarı döndürür. EN: Gets amount before discount.</summary>
    public decimal OriginalAmount { get; }
    /// <summary>TR: İndirim tutarını döndürür. EN: Gets discount amount.</summary>
    public decimal DiscountAmount { get; }
    /// <summary>TR: Müşterinin ödeyeceği final tutarı döndürür. EN: Gets final amount payable by the customer.</summary>
    public decimal FinalAmount { get; }
    /// <summary>TR: Tutar currency'sini döndürür. EN: Gets amount currency.</summary>
    public CurrencyCode Currency { get; }
    /// <summary>TR: İndirim sponsor'unu veya null değerini döndürür. EN: Gets discount sponsor or null.</summary>
    public CampaignSponsor? Sponsor { get; }
    /// <summary>TR: Provider karar nedenini döndürür. EN: Gets provider decision reason.</summary>
    public string Reason { get; }
}

/// <summary>TR: Merchant campaign hesabını external provider DTO'larından ayıran Application sınırıdır. EN: Application boundary decoupling merchant-campaign evaluation from external-provider DTOs.</summary>
public interface ICampaignProvider
{
    /// <summary>TR: Merchant alışverişi için kampanya uygunluğu ve indirimi değerlendirir. EN: Evaluates campaign eligibility and discount for a merchant purchase.</summary>
    /// <param name="customerReference">TR: PII içermeyen customer referansı. EN: Non-PII customer reference.</param>
    /// <param name="merchantId">TR: Merchant dış kimliği. EN: External merchant identifier.</param>
    /// <param name="amount">TR: İndirim öncesi tutar. EN: Amount before discount.</param>
    /// <param name="requestedAt">TR: Kampanya değerlendirme zamanı. EN: Campaign evaluation timestamp.</param>
    /// <param name="correlationId">TR: Dağıtık izleme kimliği. EN: Distributed tracing identifier.</param>
    /// <param name="cancellationToken">TR: HTTP çağrısı iptal sinyali. EN: HTTP-call cancellation signal.</param>
    /// <returns>TR: Provider bağımsız kampanya sonucunu döndürür. EN: Returns provider-independent campaign result.</returns>
    Task<CampaignEvaluationResult> EvaluateAsync(Guid customerReference, string merchantId, Money amount, DateTimeOffset requestedAt, string correlationId, CancellationToken cancellationToken);
}

/// <summary>TR: Campaign provider erişim veya response sözleşme hatasını güvenli application exception olarak temsil eder. EN: Represents campaign-provider access or response-contract failure as a safe application exception.</summary>
public sealed class CampaignProviderException : Exception
{
    /// <summary>TR: Güvenli provider hata koduyla exception oluşturur. EN: Creates the exception with a safe provider error code.</summary>
    /// <param name="code">TR: Machine-readable hata kodu. EN: Machine-readable error code.</param>
    /// <param name="message">TR: Güvenli hata açıklaması. EN: Safe error description.</param>
    /// <param name="innerException">TR: İsteğe bağlı teknik kök neden. EN: Optional technical root cause.</param>
    public CampaignProviderException(string code, string message, Exception? innerException = null) : base(message, innerException)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        Code = code.Trim();
    }

    /// <summary>TR: Machine-readable hata kodunu döndürür. EN: Gets machine-readable error code.</summary>
    public string Code { get; }
}
