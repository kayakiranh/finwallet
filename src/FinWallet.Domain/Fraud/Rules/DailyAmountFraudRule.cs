using FinWallet.Domain.Shared;

namespace FinWallet.Domain.Fraud.Rules;

/// <summary>
/// TR: Son yirmi dört saatteki aggregate finansal tutarı currency-specific threshold'larla değerlendirerek günlük hacim artışını internal fraud sinyali olarak işler.
/// EN: Evaluates the aggregate financial amount during the previous twenty-four hours using currency-specific thresholds and treats daily-volume increases as an internal fraud signal.
/// </summary>
public sealed class DailyAmountFraudRule : IInternalFraudRule
{
    /// <summary>
    /// TR: Yirmi dört saatlik aggregate tutarı ilgili currency threshold'larına göre Allow, Review veya Deny olarak değerlendirir.
    /// EN: Evaluates the twenty-four-hour aggregate amount as Allow, Review or Deny according to its currency thresholds.
    /// </summary>
    /// <param name="context">TR: Currency-aware günlük aggregate tutarı taşıyan fraud context'i. EN: Fraud context carrying the currency-aware daily aggregate amount.</param>
    /// <returns>TR: Günlük tutar kuralının karar, risk puanı ve reason code sonucunu döndürür. EN: Returns decision, risk points and reason code for the daily-amount rule.</returns>
    public FraudRuleResult Evaluate(FraudAssessmentContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var thresholds = GetThresholds(context.AmountLastTwentyFourHours.Currency);
        if (context.AmountLastTwentyFourHours.Amount >= thresholds.DenyAmount)
        {
            return new FraudRuleResult(FraudDecision.Deny, 55, "INTERNAL_HIGH_24H_AMOUNT");
        }

        if (context.AmountLastTwentyFourHours.Amount >= thresholds.ReviewAmount)
        {
            return new FraudRuleResult(FraudDecision.Review, 25, "INTERNAL_ELEVATED_24H_AMOUNT");
        }

        return new FraudRuleResult(FraudDecision.Allow, 0, "INTERNAL_24H_AMOUNT_NORMAL");
    }

    /// <summary>
    /// TR: Desteklenen currency için yirmi dört saatlik review/deny threshold çiftini döndürür.
    /// EN: Returns the twenty-four-hour review/deny threshold pair for a supported currency.
    /// </summary>
    /// <param name="currency">TR: Threshold seçilecek desteklenen currency. EN: Supported currency for which thresholds are selected.</param>
    /// <returns>TR: Currency-specific review ve deny aggregate tutarlarını döndürür. EN: Returns currency-specific review and deny aggregate amounts.</returns>
    private static DailyThresholds GetThresholds(CurrencyCode currency)
    {
        return currency switch
        {
            CurrencyCode.TRY => new DailyThresholds(100_000m, 250_000m),
            CurrencyCode.USD => new DailyThresholds(5_000m, 15_000m),
            CurrencyCode.EUR => new DailyThresholds(5_000m, 15_000m),
            _ => throw new ArgumentOutOfRangeException(nameof(currency))
        };
    }

    /// <summary>
    /// TR: Günlük aggregate tutar için currency-specific review ve deny threshold değerlerini birlikte taşır.
    /// EN: Carries currency-specific review and deny thresholds for the daily aggregate amount.
    /// </summary>
    private readonly record struct DailyThresholds
    {
        /// <summary>
        /// TR: Günlük threshold çiftini oluşturur.
        /// EN: Creates the daily threshold pair.
        /// </summary>
        /// <param name="reviewAmount">TR: Review kararı başlayan günlük aggregate tutar. EN: Daily aggregate amount at which Review begins.</param>
        /// <param name="denyAmount">TR: Deny kararı başlayan günlük aggregate tutar. EN: Daily aggregate amount at which Deny begins.</param>
        public DailyThresholds(decimal reviewAmount, decimal denyAmount)
        {
            ReviewAmount = reviewAmount;
            DenyAmount = denyAmount;
        }

        /// <summary>TR: Günlük Review threshold tutarını döndürür. EN: Gets the daily Review threshold amount.</summary>
        public decimal ReviewAmount { get; }

        /// <summary>TR: Günlük Deny threshold tutarını döndürür. EN: Gets the daily Deny threshold amount.</summary>
        public decimal DenyAmount { get; }
    }
}
