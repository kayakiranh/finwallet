namespace FinWallet.Application.Banking;

/// <summary>
/// TR: Dış banka adapter'ının provider kaynaklı güvenli ve makine-okunabilir hata kodunu Application katmanına taşıdığı exception tipidir; HTTP detaylarını sızdırmaz.
/// EN: Exception used by an external-bank adapter to carry a safe machine-readable provider failure into the Application layer without leaking HTTP details.
/// </summary>
public sealed class ExternalBankProviderException : Exception
{
    /// <summary>
    /// TR: Provider hata kodu ve retry bilgisini taşıyan exception oluşturur.
    /// EN: Creates an exception carrying provider failure code and retry information.
    /// </summary>
    /// <param name="code">TR: Stabil provider/adapter hata kodu. EN: Stable provider/adapter error code.</param>
    /// <param name="message">TR: Hassas detay içermeyen güvenli hata açıklaması. EN: Safe failure description without sensitive details.</param>
    /// <param name="isRetryable">TR: Aynı business operation'ın idempotency garantileri korunarak daha sonra tekrar denenip denenemeyeceğini belirtir. EN: Indicates whether the same business operation may be retried later while preserving idempotency guarantees.</param>
    /// <param name="innerException">TR: Infrastructure tanılama için iç exception; API'ye doğrudan dönülmez. EN: Inner exception for infrastructure diagnostics; never returned directly by the API.</param>
    public ExternalBankProviderException(string code, string message, bool isRetryable, Exception? innerException = null)
        : base(message, innerException)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        Code = code.Trim();
        IsRetryable = isRetryable;
    }

    /// <summary>TR: Makine-okunabilir provider/adapter hata kodunu döndürür. EN: Gets the machine-readable provider/adapter failure code.</summary>
    public string Code { get; }

    /// <summary>TR: Hatanın daha sonra idempotent biçimde tekrar denenebilir olup olmadığını döndürür. EN: Gets whether the failure may be retried later idempotently.</summary>
    public bool IsRetryable { get; }
}
