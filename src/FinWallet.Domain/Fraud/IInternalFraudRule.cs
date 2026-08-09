namespace FinWallet.Domain.Fraud;

/// <summary>
/// TR: Internal fraud motorunda bağımsız risk kurallarının aynı değerlendirme sözleşmesiyle zincirlenmesini sağlayan domain sınırını tanımlar.
/// EN: Defines the domain boundary that allows independent risk rules to be chained under the same evaluation contract in the internal fraud engine.
/// </summary>
public interface IInternalFraudRule
{
    /// <summary>
    /// TR: Verilen fraud context'ini tek bir risk kuralı açısından değerlendirir.
    /// EN: Evaluates the supplied fraud context from the perspective of one risk rule.
    /// </summary>
    /// <param name="context">TR: Kuralın değerlendireceği internal fraud context'i. EN: Internal fraud context evaluated by the rule.</param>
    /// <returns>TR: Kuralın karar, risk puanı ve reason code sonucunu döndürür. EN: Returns the rule decision, risk points and reason code.</returns>
    FraudRuleResult Evaluate(FraudAssessmentContext context);
}
