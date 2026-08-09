using FinWallet.Domain.Fraud;

namespace FinWallet.Application.Fraud;

/// <summary>
/// TR: Dış fraud provider'ından alınan ve provider transport detaylarından arındırılmış referans, karar, skor ve reason code bilgisini Application katmanına taşır.
/// EN: Carries provider-reference, decision, score and reason-code information returned by an external fraud provider after removing provider transport details.
/// </summary>
public sealed class ExternalFraudEvaluationResult
{
    /// <summary>
    /// TR: Normalize dış fraud değerlendirme sonucunu oluşturur.
    /// EN: Creates a normalized external fraud-evaluation result.
    /// </summary>
    /// <param name="providerReference">TR: Dış fraud değerlendirmesinin provider referansı. EN: Provider reference of the external fraud evaluation.</param>
    /// <param name="decision">TR: Provider bağımsız FinWallet fraud kararı. EN: Provider-independent FinWallet fraud decision.</param>
    /// <param name="riskScore">TR: 0-100 aralığındaki provider risk skoru. EN: Provider risk score in the 0-100 range.</param>
    /// <param name="reasonCodes">TR: Provider kararını açıklayan normalize reason code'lar. EN: Normalized reason codes explaining the provider decision.</param>
    public ExternalFraudEvaluationResult(
        Guid providerReference,
        FraudDecision decision,
        int riskScore,
        IReadOnlyCollection<string> reasonCodes)
    {
        if (providerReference == Guid.Empty) throw new ArgumentException("Provider reference cannot be empty.", nameof(providerReference));
        if (riskScore is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(riskScore));
        ArgumentNullException.ThrowIfNull(reasonCodes);

        ProviderReference = providerReference;
        Decision = decision;
        RiskScore = riskScore;
        ReasonCodes = reasonCodes;
    }

    /// <summary>TR: Dış provider değerlendirme referansını döndürür. EN: Gets the external-provider evaluation reference.</summary>
    public Guid ProviderReference { get; }

    /// <summary>TR: Provider bağımsız fraud kararını döndürür. EN: Gets the provider-independent fraud decision.</summary>
    public FraudDecision Decision { get; }

    /// <summary>TR: 0-100 aralığındaki provider risk skorunu döndürür. EN: Gets the provider risk score in the 0-100 range.</summary>
    public int RiskScore { get; }

    /// <summary>TR: Normalize reason code koleksiyonunu döndürür. EN: Gets the normalized reason-code collection.</summary>
    public IReadOnlyCollection<string> ReasonCodes { get; }
}
