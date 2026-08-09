namespace FinWallet.Domain.Shared;

/// <summary>
/// TR: Bir finansal işlemin farklı para birimlerine ait tutarları doğrudan birleştirmeye çalıştığını belirtir.
/// EN: Indicates that a financial operation attempted to directly combine monetary values with different currencies.
/// </summary>
public sealed class CurrencyMismatchException : InvalidOperationException
{
    /// <summary>
    /// TR: Beklenen ve gelen para birimlerini kullanarak yeni bir para birimi uyumsuzluğu hatası oluşturur.
    /// EN: Creates a new currency mismatch error using the expected and actual currencies.
    /// </summary>
    /// <param name="expectedCurrency">
    /// TR: Finansal işlemin beklediği para birimi.
    /// EN: Currency expected by the financial operation.
    /// </param>
    /// <param name="actualCurrency">
    /// TR: İşleme verilen gerçek para birimi.
    /// EN: Actual currency supplied to the operation.
    /// </param>
    public CurrencyMismatchException(CurrencyCode expectedCurrency, CurrencyCode actualCurrency)
        : base($"Currency mismatch. Expected '{expectedCurrency}', received '{actualCurrency}'.")
    {
        ExpectedCurrency = expectedCurrency;
        ActualCurrency = actualCurrency;
    }

    /// <summary>
    /// TR: İşlem tarafından beklenen para birimini döndürür.
    /// EN: Gets the currency expected by the operation.
    /// </summary>
    public CurrencyCode ExpectedCurrency { get; }

    /// <summary>
    /// TR: İşleme verilen ve beklenen para birimiyle eşleşmeyen para birimini döndürür.
    /// EN: Gets the supplied currency that did not match the expected currency.
    /// </summary>
    public CurrencyCode ActualCurrency { get; }
}
