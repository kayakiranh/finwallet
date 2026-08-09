namespace FinWallet.Domain.Fraud;

/// <summary>
/// TR: Tek bir internal fraud kuralının provider bağımsız karar, risk puanı ve machine-readable reason code çıktısını temsil eder.
/// EN: Represents the provider-independent decision, risk points and machine-readable reason code produced by one internal fraud rule.
/// </summary>
public sealed class FraudRuleResult
{
    /// <summary>
    /// TR: Internal fraud kural sonucunu oluşturur.
    /// EN: Creates an internal fraud-rule result.
    /// </summary>
    /// <param name="decision">TR: Kuralın Allow, Review veya Deny kararı. EN: Allow, Review or Deny decision produced by the rule.</param>
    /// <param name="riskPoints">TR: Kuralın 0-100 aralığındaki risk katkısı. EN: Rule risk contribution in the 0-100 range.</param>
    /// <param name="reasonCode">TR: Kural sonucunu açıklayan kararlı reason code. EN: Stable reason code explaining the rule result.</param>
    public FraudRuleResult(FraudDecision decision, int riskPoints, string reasonCode)
    {
        if (riskPoints is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(riskPoints));
        ArgumentException.ThrowIfNullOrWhiteSpace(reasonCode);

        Decision = decision;
        RiskPoints = riskPoints;
        ReasonCode = reasonCode.Trim();
    }

    /// <summary>TR: Kuralın fraud kararını döndürür. EN: Gets the fraud decision produced by the rule.</summary>
    public FraudDecision Decision { get; }

    /// <summary>TR: Kuralın risk puanı katkısını döndürür. EN: Gets the rule risk-point contribution.</summary>
    public int RiskPoints { get; }

    /// <summary>TR: Kural sonucunun machine-readable reason code değerini döndürür. EN: Gets the machine-readable reason code of the rule result.</summary>
    public string ReasonCode { get; }
}
