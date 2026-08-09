namespace FinWallet.Domain.Fraud;

/// <summary>
/// TR: Bağımsız internal fraud kurallarını sırayla çalıştırır, Deny > Review > Allow önceliğiyle kararları birleştirir ve toplam risk skorunu 100 ile sınırlar.
/// EN: Executes independent internal fraud rules in sequence, combines decisions using Deny > Review > Allow precedence and caps the total risk score at 100.
/// </summary>
public sealed class InternalFraudEngine
{
    private readonly IReadOnlyCollection<IInternalFraudRule> _rules;

    /// <summary>
    /// TR: En az bir internal fraud kuralından oluşan rule engine'i oluşturur.
    /// EN: Creates the rule engine from at least one internal fraud rule.
    /// </summary>
    /// <param name="rules">TR: Değerlendirme sırasında çalıştırılacak bağımsız fraud kural koleksiyonu. EN: Collection of independent fraud rules executed during evaluation.</param>
    public InternalFraudEngine(IEnumerable<IInternalFraudRule> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        _rules = rules.ToArray();
        if (_rules.Count == 0)
        {
            throw new ArgumentException("At least one internal fraud rule is required.", nameof(rules));
        }
    }

    /// <summary>
    /// TR: Tüm internal fraud kurallarını verilen context üzerinde çalıştırır ve birleşik karar/skor/reason code sonucunu üretir.
    /// EN: Executes all internal fraud rules against the supplied context and produces the combined decision/score/reason-code result.
    /// </summary>
    /// <param name="context">TR: Internal kuralların değerlendireceği fraud context'i. EN: Fraud context evaluated by internal rules.</param>
    /// <returns>TR: Birleşik internal fraud değerlendirme sonucunu döndürür. EN: Returns the combined internal fraud-evaluation result.</returns>
    public InternalFraudEvaluationResult Evaluate(FraudAssessmentContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var decision = FraudDecision.Allow;
        var riskScore = 0;
        var reasonCodes = new List<string>();

        foreach (var rule in _rules)
        {
            var ruleResult = rule.Evaluate(context);
            riskScore = Math.Min(100, riskScore + ruleResult.RiskPoints);

            if (ruleResult.Decision != FraudDecision.Allow)
            {
                reasonCodes.Add(ruleResult.ReasonCode);
            }

            if (ruleResult.Decision == FraudDecision.Deny)
            {
                decision = FraudDecision.Deny;
            }
            else if (ruleResult.Decision == FraudDecision.Review && decision == FraudDecision.Allow)
            {
                decision = FraudDecision.Review;
            }
        }

        if (reasonCodes.Count == 0)
        {
            reasonCodes.Add("INTERNAL_NO_RISK_SIGNAL");
        }

        return new InternalFraudEvaluationResult(decision, riskScore, reasonCodes);
    }
}
