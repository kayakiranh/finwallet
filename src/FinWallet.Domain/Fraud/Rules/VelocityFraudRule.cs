namespace FinWallet.Domain.Fraud.Rules;

/// <summary>
/// TR: Son beş dakikadaki işlem sayısını değerlendirerek kısa süreli transaction velocity artışlarını internal fraud sinyali olarak işler.
/// EN: Evaluates the transaction count during the previous five minutes and treats short-term transaction-velocity increases as an internal fraud signal.
/// </summary>
public sealed class VelocityFraudRule : IInternalFraudRule
{
    /// <summary>
    /// TR: Beş dakikalık işlem sayacını sabit internal fraud threshold'larına göre değerlendirir.
    /// EN: Evaluates the five-minute transaction counter using fixed internal fraud thresholds.
    /// </summary>
    /// <param name="context">TR: Velocity sayacını taşıyan fraud context'i. EN: Fraud context carrying the velocity counter.</param>
    /// <returns>TR: Velocity kuralının karar, risk puanı ve reason code sonucunu döndürür. EN: Returns decision, risk points and reason code for the velocity rule.</returns>
    public FraudRuleResult Evaluate(FraudAssessmentContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.TransactionCountLastFiveMinutes >= 10)
        {
            return new FraudRuleResult(FraudDecision.Deny, 60, "INTERNAL_HIGH_VELOCITY_5M");
        }

        if (context.TransactionCountLastFiveMinutes >= 5)
        {
            return new FraudRuleResult(FraudDecision.Review, 25, "INTERNAL_ELEVATED_VELOCITY_5M");
        }

        return new FraudRuleResult(FraudDecision.Allow, 0, "INTERNAL_VELOCITY_NORMAL");
    }
}
