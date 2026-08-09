namespace FakeFraud.Api.Contracts;

/// <summary>
/// TR: FakeFraud sağlayıcısına PII taşımadan finansal işlem, cihaz, velocity, merchant ve ülke risk sinyallerini ileten dış değerlendirme isteğini temsil eder.
/// EN: Represents the external evaluation request sent to FakeFraud containing transaction, device, velocity, merchant and country risk signals without carrying PII.
/// </summary>
public sealed class FraudEvaluationRequest
{
    /// <summary>TR: FinWallet transaction'ını provider tarafında izlemek için kullanılan dış referansı döndürür veya ayarlar. EN: Gets or sets external reference used by the provider to correlate the FinWallet transaction.</summary>
    public Guid TransactionReference { get; init; }

    /// <summary>TR: Müşteriyi PII içermeyen opaque referansla temsil eder. EN: Represents customer using a non-PII opaque reference.</summary>
    public Guid CustomerReference { get; init; }

    /// <summary>TR: Transfer/Purchase/Withdrawal gibi işlem tipini döndürür veya ayarlar. EN: Gets or sets transaction type such as Transfer, Purchase or Withdrawal.</summary>
    public string TransactionType { get; init; } = string.Empty;

    /// <summary>TR: Pozitif finansal işlem tutarını döndürür veya ayarlar. EN: Gets or sets positive financial transaction amount.</summary>
    public decimal Amount { get; init; }

    /// <summary>TR: Finansal işlem currency kodunu döndürür veya ayarlar. EN: Gets or sets financial transaction currency code.</summary>
    public string Currency { get; init; } = string.Empty;

    /// <summary>TR: İşlemin kaynak ülke kodunu döndürür veya ayarlar. EN: Gets or sets source-country code of the transaction.</summary>
    public string CountryCode { get; init; } = string.Empty;

    /// <summary>TR: Cihazı PII içermeyen opaque/hash referansla temsil eder. EN: Represents device using a non-PII opaque/hash reference.</summary>
    public string DeviceReference { get; init; } = string.Empty;

    /// <summary>TR: Cihazın müşteri için yeni olup olmadığını belirtir. EN: Indicates whether device is new for the customer.</summary>
    public bool IsNewDevice { get; init; }

    /// <summary>TR: Son beş dakikadaki işlem sayısını provider velocity sinyali olarak taşır. EN: Carries transaction count in the previous five minutes as provider velocity signal.</summary>
    public int TransactionCountLastFiveMinutes { get; init; }

    /// <summary>TR: Son yirmi dört saatteki toplam işlem tutarını provider velocity sinyali olarak taşır. EN: Carries total transaction amount in previous twenty-four hours as provider velocity signal.</summary>
    public decimal AmountLastTwentyFourHours { get; init; }

    /// <summary>TR: Merchant alışverişlerinde merchant kimliğini; diğer işlem tiplerinde null değeri döndürür veya ayarlar. EN: Gets or sets merchant identifier for merchant purchases, or null for other transaction types.</summary>
    public string? MerchantId { get; init; }
}
