using FakeFraud.Api.Contracts;
using FakeFraud.Api.Models;

namespace FakeFraud.Api.Services;

/// <summary>
/// TR: FakeFraud simulatorında dummy işlem/customer/device/merchant sinyallerini deterministic rule set ile değerlendirip harici Allow/Review/Deny kararı üretir.
/// EN: Evaluates dummy transaction/customer/device/merchant signals using a deterministic rule set in FakeFraud and produces an external Allow/Review/Deny decision.
/// </summary>
public sealed class FraudEvaluationService
{
    private static readonly HashSet<string> BlockedMerchants = new(StringComparer.OrdinalIgnoreCase)
    {
        "MRC-BLOCKED-001",
        "MRC-BLOCKED-002"
    };

    private static readonly HashSet<string> HighRiskCountries = new(StringComparer.OrdinalIgnoreCase)
    {
        "XX",
        "ZZ"
    };

    /// <summary>
    /// TR: Harici fraud sinyallerini değerlendirir; deny sinyalleri review sinyallerinden önceliklidir ve provider skoru yalnızca simulator davranışını görünür kılmak için üretilir.
    /// EN: Evaluates external fraud signals; deny signals take precedence over review signals and the provider score exists only to make simulator behavior observable.
    /// </summary>
    /// <param name="request">TR: PII içermeyen fraud değerlendirme isteği. EN: Fraud-evaluation request without PII.</param>
    /// <returns>TR: Provider referansı, karar, skor ve reason code listesini döndürür. EN: Returns provider reference, decision, score and reason-code list.</returns>
    public FraudEvaluationResponse Evaluate(FraudEvaluationRequest request)
    {
        Validate(request);

        var reasons = new List<string>();
        var score = 0;
        var deny = false;
        var review = false;

        if (request.Amount >= 100_000m)
        {
            score += 70;
            reasons.Add("VERY_HIGH_TRANSACTION_AMOUNT");
            deny = true;
        }
        else if (request.Amount >= 25_000m)
        {
            score += 35;
            reasons.Add("HIGH_TRANSACTION_AMOUNT");
            review = true;
        }

        if (request.TransactionCountLastFiveMinutes >= 10)
        {
            score += 55;
            reasons.Add("HIGH_VELOCITY_5M");
            deny = true;
        }
        else if (request.TransactionCountLastFiveMinutes >= 5)
        {
            score += 25;
            reasons.Add("ELEVATED_VELOCITY_5M");
            review = true;
        }

        if (request.AmountLastTwentyFourHours >= 150_000m)
        {
            score += 45;
            reasons.Add("HIGH_24H_AMOUNT");
            deny = true;
        }
        else if (request.AmountLastTwentyFourHours >= 75_000m)
        {
            score += 20;
            reasons.Add("ELEVATED_24H_AMOUNT");
            review = true;
        }

        if (request.IsNewDevice && request.Amount >= 10_000m)
        {
            score += 30;
            reasons.Add("NEW_DEVICE_HIGH_AMOUNT");
            review = true;
        }

        if (!string.IsNullOrWhiteSpace(request.MerchantId) && BlockedMerchants.Contains(request.MerchantId.Trim()))
        {
            score += 100;
            reasons.Add("BLOCKED_MERCHANT");
            deny = true;
        }

        if (HighRiskCountries.Contains(request.CountryCode.Trim()))
        {
            score += 50;
            reasons.Add("HIGH_RISK_COUNTRY");
            deny = true;
        }

        score = Math.Min(score, 100);
        if (reasons.Count == 0)
        {
            reasons.Add("NO_EXTERNAL_RISK_SIGNAL");
        }

        var decision = deny
            ? ExternalFraudDecision.Deny
            : review
                ? ExternalFraudDecision.Review
                : ExternalFraudDecision.Allow;

        return new FraudEvaluationResponse(Guid.NewGuid(), decision, score, reasons);
    }

    /// <summary>
    /// TR: Harici fraud isteğinde identity, tutar, velocity ve zorunlu opaque referans alanlarının temel tutarlılığını doğrular; parola, telefon, IBAN veya token gibi PII/secrets bu contract'ta bulunmaz.
    /// EN: Validates basic consistency of identity, amount, velocity and required opaque-reference fields in the external fraud request; the contract contains no PII/secrets such as password, phone, IBAN or tokens.
    /// </summary>
    /// <param name="request">TR: Doğrulanacak fraud request. EN: Fraud request to validate.</param>
    private static void Validate(FraudEvaluationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.TransactionReference == Guid.Empty) throw new ArgumentException("Transaction reference cannot be empty.", nameof(request));
        if (request.CustomerReference == Guid.Empty) throw new ArgumentException("Customer reference cannot be empty.", nameof(request));
        ArgumentException.ThrowIfNullOrWhiteSpace(request.TransactionType);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Currency);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.CountryCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.DeviceReference);
        if (request.Amount <= 0) throw new ArgumentOutOfRangeException(nameof(request));
        if (request.TransactionCountLastFiveMinutes < 0) throw new ArgumentOutOfRangeException(nameof(request));
        if (request.AmountLastTwentyFourHours < 0) throw new ArgumentOutOfRangeException(nameof(request));
    }
}
