namespace FakeCutoff.Api.Models;

/// <summary>
/// TR: FakeCutoff simulatorında ülke, para birimi ve işlem tipine göre kullanılan çalışma timezone'u, cutoff saati ve settlement business-day sayısını temsil eder.
/// EN: Represents the timezone, cutoff time and settlement-business-day count used by FakeCutoff for a country, currency and transaction type.
/// </summary>
public sealed class CutoffRule
{
    /// <summary>
    /// TR: Cutoff kuralını oluşturur.
    /// EN: Creates a cutoff rule.
    /// </summary>
    /// <param name="countryCode">TR: İki harfli ülke kodu. EN: Two-letter country code.</param>
    /// <param name="currency">TR: Para birimi kodu. EN: Currency code.</param>
    /// <param name="transactionType">TR: Dış işlem tipi. EN: External transaction type.</param>
    /// <param name="timeZoneId">TR: IANA timezone kimliği. EN: IANA timezone identifier.</param>
    /// <param name="cutoffTime">TR: Yerel cutoff saati. EN: Local cutoff time.</param>
    /// <param name="settlementBusinessDays">TR: Processing gününden sonra settlement için eklenecek business-day sayısı. EN: Number of business days added after the processing date for settlement.</param>
    public CutoffRule(
        string countryCode,
        string currency,
        string transactionType,
        string timeZoneId,
        TimeOnly cutoffTime,
        int settlementBusinessDays)
    {
        CountryCode = countryCode;
        Currency = currency;
        TransactionType = transactionType;
        TimeZoneId = timeZoneId;
        CutoffTime = cutoffTime;
        SettlementBusinessDays = settlementBusinessDays;
    }

    /// <summary>TR: Ülke kodunu döndürür. EN: Gets the country code.</summary>
    public string CountryCode { get; }

    /// <summary>TR: Para birimi kodunu döndürür. EN: Gets the currency code.</summary>
    public string Currency { get; }

    /// <summary>TR: İşlem tipini döndürür. EN: Gets the transaction type.</summary>
    public string TransactionType { get; }

    /// <summary>TR: Hesaplamada kullanılan IANA timezone kimliğini döndürür. EN: Gets the IANA timezone identifier used for calculation.</summary>
    public string TimeZoneId { get; }

    /// <summary>TR: Yerel cutoff saatini döndürür. EN: Gets the local cutoff time.</summary>
    public TimeOnly CutoffTime { get; }

    /// <summary>TR: Settlement için eklenecek business-day sayısını döndürür. EN: Gets the number of business days added for settlement.</summary>
    public int SettlementBusinessDays { get; }
}
