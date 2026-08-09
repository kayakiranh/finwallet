using FakeFraud.Api.Models;

namespace FakeFraud.Api.Contracts;

/// <summary>
/// TR: FakeFraud sağlayıcısının bağımsız risk kararı, provider referansı, skor ve makine-okunabilir reason code listesini taşır.
/// EN: Carries the independent FakeFraud provider decision, provider reference, score and machine-readable reason-code list.
/// </summary>
public sealed class FraudEvaluationResponse
{
    /// <summary>TR: Fraud provider yanıtını oluşturur. EN: Creates fraud-provider response.</summary>
    /// <param name="providerReference">TR: Provider değerlendirme referansı. EN: Provider evaluation reference.</param>
    /// <param name="decision">TR: Allow/Review/Deny provider kararı. EN: Allow/Review/Deny provider decision.</param>
    /// <param name="riskScore">TR: 0..100 arası simulator risk skoru. EN: Simulator risk score from 0 through 100.</param>
    /// <param name="reasonCodes">TR: Kararı açıklayan makine-okunabilir reason code'lar. EN: Machine-readable reason codes explaining the decision.</param>
    public FraudEvaluationResponse(Guid providerReference, ExternalFraudDecision decision, int riskScore, IReadOnlyCollection<string> reasonCodes)
    {
        ProviderReference = providerReference;
        Decision = decision;
        RiskScore = riskScore;
        ReasonCodes = reasonCodes;
    }

    /// <summary>TR: Provider değerlendirme referansını döndürür. EN: Gets provider evaluation reference.</summary>
    public Guid ProviderReference { get; }

    /// <summary>TR: Harici fraud kararını döndürür. EN: Gets external fraud decision.</summary>
    public ExternalFraudDecision Decision { get; }

    /// <summary>TR: 0..100 arası simulator risk skorunu döndürür. EN: Gets simulator risk score from 0 through 100.</summary>
    public int RiskScore { get; }

    /// <summary>TR: Karar reason code koleksiyonunu döndürür. EN: Gets decision reason-code collection.</summary>
    public IReadOnlyCollection<string> ReasonCodes { get; }
}
