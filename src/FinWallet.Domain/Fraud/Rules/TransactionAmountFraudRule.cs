using FinWallet.Domain.Shared;

namespace FinWallet.Domain.Fraud.Rules;

/// <summary>
/// TR: Tek işlem tutarını currency-specific sabit simulator threshold'larıyla değerlendirerek aynı nominal sayının farklı para birimlerinde aynı risk kabul edilmesini engeller.
/// EN: Evaluates a single transaction amount using currency-specific fixed simulator thresholds so the same nominal number is not treated as equal risk across different currencies.
/// </summary>
public sealed class TransactionAmountFraudRule : IInternalFraudRule
{
    /// <summary>
    /// TR: İşlem tutarını ilgili currency threshold'larına göre Allow, Review veya Deny olarak değerlendirir.
    /// EN: Evaluates the transaction amount as Allow, Review or Deny according to its currency thresholds.
    /// </summary>
    /// <param name="context">TR: Currency-aware transaction tutarını taşıyan fraud context'i. EN: Fraud context carrying the currency-aware transaction amount.</param>
    /// <returns>TR: Tutar kuralının karar, risk puanı ve reason code sonucunu döndürür. EN: Returns decision, risk points and reason code for the amount rule.</returns>
    public FraudRuleResult Evaluate(FraudAssessmentContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var thresholds = GetThresholds(context.Amount.Currency);
        if (context.Amount.Amount >= thresholds.DenyAmount)
        {
            return new FraudRuleResult(FraudDecision.Deny, 70, "INTERNAL_VERY_HIGH_TRANSACTION_AMOUNT");
        }

        if (context.Amount.Amount >= thresholds.ReviewAmount)
        {
            return new FraudRuleResult(FraudDecision.Review, 30, "INTERNAL_HIGH_TRANSACTION_AMOUNT");
        }

        return new FraudRuleResult(FraudDecision.Allow, 0, "INTERNAL_AMOUNT_NORMAL");
    }

    /// <summary>
    /// TR: Desteklenen currency için internal fraud review/deny threshold çiftini döndürür.
    /// EN: Returns the internal fraud review/deny threshold pair for a supported currency.
    /// </summary>
    /// <param name="currency">TR: Threshold seçilecek desteklenen currency. EN: Supported currency for which thresholds are selected.</param>
    /// <returns>TR: Currency-specific review ve deny tutarlarını döndürür. EN: Returns currency-specific review and deny amounts.</returns>
    private static AmountThresholds GetThresholds(CurrencyCode currency)
    {
        return currency switch
        {
            CurrencyCode.TRY => new AmountThresholds(20_000m, 75_000m),
            CurrencyCode.USD => new AmountThresholds(1_000m, 5_000m),
            CurrencyCode.EUR => new AmountThresholds(1_000m, 5_000m),
            _ => throw new ArgumentOutOfRangeException(nameof(currency))
        };
    }

    /// <summary>
    /// TR: Tek işlem tutarı için currency-specific review ve deny threshold değerlerini birlikte taşır.
    /// EN: Carries currency-specific review and deny thresholds for a single transaction amount.
    /// </summary>
    private readonly record struct AmountThresholds
    {
        /// <summary>
        /// TR: Threshold çiftini oluşturur.
        /// EN: Creates the threshold pair.
        /// </summary>
        /// <param name="reviewAmount">TR: Review kararı başlayan tutar. EN: Amount at which Review begins.</param>
        /// <param name="denyAmount">TR: Deny kararı başlayan tutar. EN: Amount at which Deny begins.</param>
        public AmountThresholds(decimal reviewAmount, decimal denyAmount)
        {
            ReviewAmount = reviewAmount;
            DenyAmount = denyAmount;
        }

        /// <summary>TR: Review threshold tutarını döndürür. EN: Gets the Review threshold amount.</summary>
        public decimal ReviewAmount { get; }

        /// <summary>TR: Deny threshold tutarını döndürür. EN: Gets the Deny threshold amount.</summary>
        public decimal DenyAmount { get; }
    }
}
