namespace FinWallet.Application.Fraud;

/// <summary>
/// TR: FinWallet use-case'lerinin dış fraud provider'ına gönderebileceği PII içermeyen transaction ve risk sinyallerini provider DTO'sundan bağımsız biçimde taşır.
/// EN: Carries PII-free transaction and risk signals that FinWallet use cases may send to an external fraud provider independently of provider DTOs.
/// </summary>
public sealed class ExternalFraudEvaluationContext
{
    /// <summary>
    /// TR: Dış fraud değerlendirme context'ini oluşturur ve temel risk alanlarının geçerli olmasını zorunlu kılar.
    /// EN: Creates an external fraud-evaluation context and requires the basic risk fields to be valid.
    /// </summary>
    /// <param name="transactionReference">TR: Değerlendirilen FinWallet transaction kimliği. EN: FinWallet transaction identifier being evaluated.</param>
    /// <param name="customerReference">TR: PII içermeyen müşteri referansı. EN: Non-PII customer reference.</param>
    /// <param name="transactionType">TR: Transfer, Purchase veya Withdrawal gibi işlem tipi. EN: Transaction type such as Transfer, Purchase or Withdrawal.</param>
    /// <param name="amount">TR: Pozitif işlem tutarı. EN: Positive transaction amount.</param>
    /// <param name="currency">TR: İşlem currency kodu. EN: Transaction currency code.</param>
    /// <param name="countryCode">TR: İşlem kaynak ülke kodu. EN: Transaction source-country code.</param>
    /// <param name="deviceReference">TR: PII içermeyen cihaz referansı. EN: Non-PII device reference.</param>
    /// <param name="isNewDevice">TR: Cihazın müşteri için yeni olup olmadığını belirtir. EN: Indicates whether the device is new for the customer.</param>
    /// <param name="transactionCountLastFiveMinutes">TR: Son beş dakikadaki işlem adedi. EN: Transaction count during the previous five minutes.</param>
    /// <param name="amountLastTwentyFourHours">TR: Son yirmi dört saatteki toplam işlem tutarı. EN: Total transaction amount during the previous twenty-four hours.</param>
    /// <param name="merchantId">TR: Merchant işlemlerinde PII içermeyen merchant referansı; diğer işlemlerde null. EN: Non-PII merchant reference for merchant transactions, otherwise null.</param>
    public ExternalFraudEvaluationContext(
        Guid transactionReference,
        Guid customerReference,
        string transactionType,
        decimal amount,
        string currency,
        string countryCode,
        string deviceReference,
        bool isNewDevice,
        int transactionCountLastFiveMinutes,
        decimal amountLastTwentyFourHours,
        string? merchantId)
    {
        if (transactionReference == Guid.Empty) throw new ArgumentException("Transaction reference cannot be empty.", nameof(transactionReference));
        if (customerReference == Guid.Empty) throw new ArgumentException("Customer reference cannot be empty.", nameof(customerReference));
        ArgumentException.ThrowIfNullOrWhiteSpace(transactionType);
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);
        ArgumentException.ThrowIfNullOrWhiteSpace(countryCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceReference);
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
        if (transactionCountLastFiveMinutes < 0) throw new ArgumentOutOfRangeException(nameof(transactionCountLastFiveMinutes));
        if (amountLastTwentyFourHours < 0) throw new ArgumentOutOfRangeException(nameof(amountLastTwentyFourHours));

        TransactionReference = transactionReference;
        CustomerReference = customerReference;
        TransactionType = transactionType.Trim();
        Amount = amount;
        Currency = currency.Trim().ToUpperInvariant();
        CountryCode = countryCode.Trim().ToUpperInvariant();
        DeviceReference = deviceReference.Trim();
        IsNewDevice = isNewDevice;
        TransactionCountLastFiveMinutes = transactionCountLastFiveMinutes;
        AmountLastTwentyFourHours = amountLastTwentyFourHours;
        MerchantId = string.IsNullOrWhiteSpace(merchantId) ? null : merchantId.Trim();
    }

    /// <summary>TR: FinWallet transaction referansını döndürür. EN: Gets the FinWallet transaction reference.</summary>
    public Guid TransactionReference { get; }

    /// <summary>TR: PII içermeyen müşteri referansını döndürür. EN: Gets the non-PII customer reference.</summary>
    public Guid CustomerReference { get; }

    /// <summary>TR: İşlem tipini döndürür. EN: Gets the transaction type.</summary>
    public string TransactionType { get; }

    /// <summary>TR: Pozitif işlem tutarını döndürür. EN: Gets the positive transaction amount.</summary>
    public decimal Amount { get; }

    /// <summary>TR: Normalize currency kodunu döndürür. EN: Gets the normalized currency code.</summary>
    public string Currency { get; }

    /// <summary>TR: Normalize kaynak ülke kodunu döndürür. EN: Gets the normalized source-country code.</summary>
    public string CountryCode { get; }

    /// <summary>TR: PII içermeyen cihaz referansını döndürür. EN: Gets the non-PII device reference.</summary>
    public string DeviceReference { get; }

    /// <summary>TR: Cihazın müşteri için yeni olup olmadığını döndürür. EN: Gets whether the device is new for the customer.</summary>
    public bool IsNewDevice { get; }

    /// <summary>TR: Son beş dakikadaki işlem sayısını döndürür. EN: Gets the transaction count in the previous five minutes.</summary>
    public int TransactionCountLastFiveMinutes { get; }

    /// <summary>TR: Son yirmi dört saatteki toplam işlem tutarını döndürür. EN: Gets the total transaction amount in the previous twenty-four hours.</summary>
    public decimal AmountLastTwentyFourHours { get; }

    /// <summary>TR: İsteğe bağlı merchant referansını döndürür. EN: Gets the optional merchant reference.</summary>
    public string? MerchantId { get; }
}
