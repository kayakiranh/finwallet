namespace FinWallet.Domain.Fraud.Rules;

/// <summary>
/// TR: Yeni cihaz ile daha önce bilinmeyen beneficiary kombinasyonunu davranışsal internal fraud sinyali olarak değerlendirir.
/// EN: Evaluates the combination of a new device and a previously unknown beneficiary as a behavioral internal fraud signal.
/// </summary>
public sealed class NewDeviceBeneficiaryFraudRule : IInternalFraudRule
{
    /// <summary>
    /// TR: Yeni cihaz + bilinmeyen beneficiary durumunda Review üretir; tek başına bu sinyal doğrudan Deny oluşturmaz.
    /// EN: Produces Review for a new-device plus unknown-beneficiary combination; this signal alone does not produce an immediate Deny.
    /// </summary>
    /// <param name="context">TR: Cihaz ve beneficiary davranış sinyallerini taşıyan fraud context'i. EN: Fraud context carrying device and beneficiary behavioral signals.</param>
    /// <returns>TR: Davranışsal kuralın karar, risk puanı ve reason code sonucunu döndürür. EN: Returns decision, risk points and reason code for the behavioral rule.</returns>
    public FraudRuleResult Evaluate(FraudAssessmentContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.IsNewDevice && !context.IsKnownBeneficiary)
        {
            return new FraudRuleResult(FraudDecision.Review, 30, "INTERNAL_NEW_DEVICE_UNKNOWN_BENEFICIARY");
        }

        if (context.IsNewDevice)
        {
            return new FraudRuleResult(FraudDecision.Review, 10, "INTERNAL_NEW_DEVICE");
        }

        return new FraudRuleResult(FraudDecision.Allow, 0, "INTERNAL_DEVICE_NORMAL");
    }
}
