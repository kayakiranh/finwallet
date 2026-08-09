namespace FakeCutoff.Api.Contracts;

/// <summary>
/// TR: Dış cutoff sağlayıcısının bir banka işlemi için çalışma günü, cutoff saati ve settlement günü hesaplamasında kullandığı isteği temsil eder.
/// EN: Represents the request used by the external cutoff provider to calculate business-day, cutoff-time and settlement-date behavior for a banking operation.
/// </summary>
public sealed class CutoffEvaluationRequest
{
    /// <summary>
    /// TR: İşlemin business calendar kurallarının değerlendirileceği iki harfli ülke kodunu döndürür veya ayarlar.
    /// EN: Gets or sets the two-letter country code whose business-calendar rules are evaluated for the operation.
    /// </summary>
    public string CountryCode { get; init; } = string.Empty;

    /// <summary>
    /// TR: İşlemin para birimi kodunu döndürür veya ayarlar.
    /// EN: Gets or sets the currency code of the operation.
    /// </summary>
    public string Currency { get; init; } = string.Empty;

    /// <summary>
    /// TR: `Withdrawal` veya `BankTransfer` gibi cutoff kuralı seçmekte kullanılan dış işlem tipini döndürür veya ayarlar.
    /// EN: Gets or sets the external operation type such as `Withdrawal` or `BankTransfer` used to select a cutoff rule.
    /// </summary>
    public string TransactionType { get; init; } = string.Empty;

    /// <summary>
    /// TR: FinWallet tarafından gözlenen ve provider'ın ülke timezone'una çevireceği istek zamanını döndürür veya ayarlar.
    /// EN: Gets or sets the request timestamp observed by FinWallet and converted by the provider into the country's timezone.
    /// </summary>
    public DateTimeOffset RequestedAt { get; init; }
}
