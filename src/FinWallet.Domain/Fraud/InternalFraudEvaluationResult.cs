namespace FinWallet.Domain.Fraud;

/// <summary>
/// TR: Tüm internal fraud kurallarının birleşik kararını, toplam risk skorunu ve tetiklenen reason code listesini temsil eder.
/// EN: Represents the combined decision, total risk score and triggered reason-code list produced by all internal fraud rules.
/// </summary>
public sealed class InternalFraudEvaluationResult
{
    /// <summary>
    /// TR: Birleşik internal fraud sonucunu oluşturur.
    /// EN: Creates a combined internal fraud result.
    /// </summary>
    /// <param name="decision">TR: Internal kuralların birleşik Allow/Review/Deny kararı. EN: Combined Allow/Review/Deny decision from internal rules.</param>
    /// <param name="riskScore">TR: 0-100 aralığına sınırlandırılmış toplam internal risk skoru. EN: Total internal risk score capped to the 0-100 range.</param>
    /// <param name="reasonCodes">TR: Risk oluşturan veya değerlendirmeyi açıklayan reason code koleksiyonu. EN: Reason-code collection representing triggered risk signals or evaluation outcome.</param>
    public InternalFraudEvaluationResult(
        FraudDecision decision,
        int riskScore,
        IReadOnlyCollection<string> reasonCodes)
    {
        if (riskScore is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(riskScore));
        ArgumentNullException.ThrowIfNull(reasonCodes);

        Decision = decision;
        RiskScore = riskScore;
        ReasonCodes = reasonCodes;
    }

    /// <summary>TR: Birleşik internal fraud kararını döndürür. EN: Gets the combined internal fraud decision.</summary>
    public FraudDecision Decision { get; }

    /// <summary>TR: 0-100 aralığındaki internal risk skorunu döndürür. EN: Gets the internal risk score in the 0-100 range.</summary>
    public int RiskScore { get; }

    /// <summary>TR: Internal fraud reason code koleksiyonunu döndürür. EN: Gets the internal fraud reason-code collection.</summary>
    public IReadOnlyCollection<string> ReasonCodes { get; }
}
