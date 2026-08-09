using FinWallet.Domain.Fraud;
using FinWallet.Domain.Shared;
using FinWallet.Domain.Transactions;

namespace FinWallet.Application.Fraud;

/// <summary>
/// TR: Finansal posting öncesi fraud değerlendirmesinin PII içermeyen durable audit snapshot'ını taşır.
/// EN: Carries a PII-free durable audit snapshot of fraud evaluation performed before financial posting.
/// </summary>
public sealed class FraudEvaluationAuditRecord
{
    /// <summary>
    /// TR: Fraud audit kaydını oluşturur ve external evaluation state alanlarının birbiriyle tutarlı olmasını zorunlu kılar.
    /// EN: Creates a fraud-audit record and requires external-evaluation state fields to be mutually consistent.
    /// </summary>
    /// <param name="id">TR: FraudEvent ve external evaluation için kullanılan benzersiz referans. EN: Unique reference used for FraudEvent and external evaluation.</param>
    /// <param name="customerId">TR: Authenticated customer kimliği. EN: Authenticated customer identifier.</param>
    /// <param name="sessionId">TR: Server-side session kimliği. EN: Server-side session identifier.</param>
    /// <param name="transactionType">TR: Değerlendirilen finansal işlem türü. EN: Evaluated financial-transaction type.</param>
    /// <param name="sourceWalletId">TR: İsteğe bağlı source wallet kimliği. EN: Optional source-wallet identifier.</param>
    /// <param name="destinationWalletId">TR: İsteğe bağlı destination wallet kimliği. EN: Optional destination-wallet identifier.</param>
    /// <param name="amount">TR: Currency-aware işlem tutarı. EN: Currency-aware transaction amount.</param>
    /// <param name="countryCode">TR: Server-derived customer ülke kodu. EN: Server-derived customer country code.</param>
    /// <param name="deviceReference">TR: Raw DeviceId yerine kullanılan SHA-256 device reference. EN: SHA-256 device reference used instead of raw DeviceId.</param>
    /// <param name="isNewDevice">TR: New-device risk sinyali. EN: New-device risk signal.</param>
    /// <param name="transactionCountLastFiveMinutes">TR: Son beş dakika velocity sayısı. EN: Five-minute velocity count.</param>
    /// <param name="amountLastTwentyFourHours">TR: Son 24 saat aynı-currency toplamı. EN: Same-currency total over the previous 24 hours.</param>
    /// <param name="isKnownBeneficiary">TR: Known-beneficiary risk sinyali. EN: Known-beneficiary risk signal.</param>
    /// <param name="internalDecision">TR: FinWallet internal fraud kararı. EN: FinWallet internal fraud decision.</param>
    /// <param name="externalEvaluationStatus">TR: External evaluation lifecycle durumu. EN: External-evaluation lifecycle state.</param>
    /// <param name="externalDecision">TR: External provider kararı; uygun değilse null. EN: External-provider decision, or null when not applicable.</param>
    /// <param name="finalDecision">TR: Birleşik final fraud kararı; provider unavailable ise null. EN: Combined final fraud decision, or null when provider is unavailable.</param>
    /// <param name="externalProviderReference">TR: Provider evaluation referansı; uygun değilse null. EN: Provider-evaluation reference, or null when not applicable.</param>
    /// <param name="externalRiskScore">TR: Provider 0..100 risk skoru; uygun değilse null. EN: Provider risk score from 0..100, or null when not applicable.</param>
    /// <param name="externalReasonCodes">TR: Provider reason-code koleksiyonu; uygun değilse null. EN: Provider reason-code collection, or null when not applicable.</param>
    /// <param name="externalFailureCode">TR: Provider unavailable durumunda güvenli machine-readable code; diğer durumlarda null. EN: Safe machine-readable code when provider is unavailable, otherwise null.</param>
    /// <param name="createdAt">TR: Audit UTC oluşturulma zamanı. EN: UTC audit creation time.</param>
    public FraudEvaluationAuditRecord(
        Guid id,
        Guid customerId,
        Guid sessionId,
        FinancialTransactionType transactionType,
        Guid? sourceWalletId,
        Guid? destinationWalletId,
        Money amount,
        string countryCode,
        string deviceReference,
        bool isNewDevice,
        int transactionCountLastFiveMinutes,
        decimal amountLastTwentyFourHours,
        bool isKnownBeneficiary,
        FraudDecision internalDecision,
        ExternalFraudEvaluationStatus externalEvaluationStatus,
        FraudDecision? externalDecision,
        FraudDecision? finalDecision,
        Guid? externalProviderReference,
        int? externalRiskScore,
        IReadOnlyCollection<string>? externalReasonCodes,
        string? externalFailureCode,
        DateTimeOffset createdAt)
    {
        if (id == Guid.Empty) throw new ArgumentException("Fraud-event identifier cannot be empty.", nameof(id));
        if (customerId == Guid.Empty) throw new ArgumentException("Customer identifier cannot be empty.", nameof(customerId));
        if (sessionId == Guid.Empty) throw new ArgumentException("Session identifier cannot be empty.", nameof(sessionId));
        if (!amount.IsPositive) throw new ArgumentOutOfRangeException(nameof(amount));
        ArgumentException.ThrowIfNullOrWhiteSpace(countryCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceReference);
        if (transactionCountLastFiveMinutes < 0) throw new ArgumentOutOfRangeException(nameof(transactionCountLastFiveMinutes));
        if (amountLastTwentyFourHours < 0m) throw new ArgumentOutOfRangeException(nameof(amountLastTwentyFourHours));
        FinancialAmountRules.EnsureStorageCompatible(amountLastTwentyFourHours, nameof(amountLastTwentyFourHours));
        ValidateExternalState(
            internalDecision,
            externalEvaluationStatus,
            externalDecision,
            finalDecision,
            externalProviderReference,
            externalRiskScore,
            externalReasonCodes,
            externalFailureCode);

        Id = id;
        CustomerId = customerId;
        SessionId = sessionId;
        TransactionType = transactionType;
        SourceWalletId = sourceWalletId;
        DestinationWalletId = destinationWalletId;
        Amount = amount;
        CountryCode = countryCode.Trim().ToUpperInvariant();
        DeviceReference = deviceReference.Trim();
        IsNewDevice = isNewDevice;
        TransactionCountLastFiveMinutes = transactionCountLastFiveMinutes;
        AmountLastTwentyFourHours = amountLastTwentyFourHours;
        IsKnownBeneficiary = isKnownBeneficiary;
        InternalDecision = internalDecision;
        ExternalEvaluationStatus = externalEvaluationStatus;
        ExternalDecision = externalDecision;
        FinalDecision = finalDecision;
        ExternalProviderReference = externalProviderReference;
        ExternalRiskScore = externalRiskScore;
        ExternalReasonCodes = externalReasonCodes;
        ExternalFailureCode = string.IsNullOrWhiteSpace(externalFailureCode) ? null : externalFailureCode.Trim();
        CreatedAt = createdAt;
    }

    /// <summary>TR: FraudEvent kimliğini döndürür. EN: Gets FraudEvent identifier.</summary>
    public Guid Id { get; }
    /// <summary>TR: Customer kimliğini döndürür. EN: Gets customer identifier.</summary>
    public Guid CustomerId { get; }
    /// <summary>TR: Session kimliğini döndürür. EN: Gets session identifier.</summary>
    public Guid SessionId { get; }
    /// <summary>TR: Financial transaction türünü döndürür. EN: Gets financial-transaction type.</summary>
    public FinancialTransactionType TransactionType { get; }
    /// <summary>TR: Source wallet kimliğini döndürür. EN: Gets source-wallet identifier.</summary>
    public Guid? SourceWalletId { get; }
    /// <summary>TR: Destination wallet kimliğini döndürür. EN: Gets destination-wallet identifier.</summary>
    public Guid? DestinationWalletId { get; }
    /// <summary>TR: Currency-aware tutarı döndürür. EN: Gets currency-aware amount.</summary>
    public Money Amount { get; }
    /// <summary>TR: Country code değerini döndürür. EN: Gets country code.</summary>
    public string CountryCode { get; }
    /// <summary>TR: Hashed device reference değerini döndürür. EN: Gets hashed device reference.</summary>
    public string DeviceReference { get; }
    /// <summary>TR: New-device sinyalini döndürür. EN: Gets new-device signal.</summary>
    public bool IsNewDevice { get; }
    /// <summary>TR: 5 dakikalık velocity sayısını döndürür. EN: Gets five-minute velocity count.</summary>
    public int TransactionCountLastFiveMinutes { get; }
    /// <summary>TR: 24 saat aggregate tutarı döndürür. EN: Gets 24-hour aggregate amount.</summary>
    public decimal AmountLastTwentyFourHours { get; }
    /// <summary>TR: Known-beneficiary sinyalini döndürür. EN: Gets known-beneficiary signal.</summary>
    public bool IsKnownBeneficiary { get; }
    /// <summary>TR: Internal fraud kararını döndürür. EN: Gets internal fraud decision.</summary>
    public FraudDecision InternalDecision { get; }
    /// <summary>TR: External evaluation status değerini döndürür. EN: Gets external-evaluation status.</summary>
    public ExternalFraudEvaluationStatus ExternalEvaluationStatus { get; }
    /// <summary>TR: External fraud kararını döndürür. EN: Gets external fraud decision.</summary>
    public FraudDecision? ExternalDecision { get; }
    /// <summary>TR: Birleşik final fraud kararını döndürür. EN: Gets combined final fraud decision.</summary>
    public FraudDecision? FinalDecision { get; }
    /// <summary>TR: External provider referansını döndürür. EN: Gets external-provider reference.</summary>
    public Guid? ExternalProviderReference { get; }
    /// <summary>TR: External risk score değerini döndürür. EN: Gets external risk score.</summary>
    public int? ExternalRiskScore { get; }
    /// <summary>TR: External reason-code koleksiyonunu döndürür. EN: Gets external reason-code collection.</summary>
    public IReadOnlyCollection<string>? ExternalReasonCodes { get; }
    /// <summary>TR: External failure code değerini döndürür. EN: Gets external failure code.</summary>
    public string? ExternalFailureCode { get; }
    /// <summary>TR: Audit UTC oluşturulma zamanını döndürür. EN: Gets audit UTC creation time.</summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>TR: External fraud state alanlarının status ile tutarlı olduğunu doğrular. EN: Validates external-fraud state fields against the evaluation status.</summary>
    private static void ValidateExternalState(
        FraudDecision internalDecision,
        ExternalFraudEvaluationStatus status,
        FraudDecision? externalDecision,
        FraudDecision? finalDecision,
        Guid? providerReference,
        int? riskScore,
        IReadOnlyCollection<string>? reasonCodes,
        string? failureCode)
    {
        if (riskScore is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(riskScore));

        switch (status)
        {
            case ExternalFraudEvaluationStatus.NotRequired:
                if (internalDecision != FraudDecision.Deny || finalDecision != FraudDecision.Deny || externalDecision is not null || providerReference is not null || riskScore is not null || reasonCodes is not null || !string.IsNullOrWhiteSpace(failureCode))
                    throw new ArgumentException("NotRequired external fraud state is inconsistent.");
                break;
            case ExternalFraudEvaluationStatus.Completed:
                if (externalDecision is null || finalDecision is null || providerReference is null || riskScore is null || reasonCodes is null || !string.IsNullOrWhiteSpace(failureCode))
                    throw new ArgumentException("Completed external fraud state is inconsistent.");
                break;
            case ExternalFraudEvaluationStatus.Unavailable:
                if (externalDecision is not null || finalDecision is not null || providerReference is not null || riskScore is not null || reasonCodes is not null || string.IsNullOrWhiteSpace(failureCode))
                    throw new ArgumentException("Unavailable external fraud state is inconsistent.");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(status));
        }
    }
}
