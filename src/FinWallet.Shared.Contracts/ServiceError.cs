namespace FinWallet.Shared.Contracts;

/// <summary>
/// TR: API istemcisine döndürülen makine tarafından okunabilir hata kodu ve güvenli açıklamayı taşıyan ortak HTTP hata sözleşmesini temsil eder.
/// EN: Represents the shared HTTP error contract carrying a machine-readable error code and a safe description returned to API clients.
/// </summary>
public sealed class ServiceError
{
    /// <summary>
    /// TR: Ortak API hata sözleşmesini oluşturur.
    /// EN: Creates the shared API error contract.
    /// </summary>
    /// <param name="code">
    /// TR: İstemcinin programatik karar vermesi için kullanılan kararlı hata kodu.
    /// EN: Stable error code used by the client for programmatic decisions.
    /// </param>
    /// <param name="message">
    /// TR: Hassas iç detay içermeyen kullanıcı/istemci güvenli hata açıklaması.
    /// EN: Client-safe error description that does not expose sensitive internal details.
    /// </param>
    public ServiceError(string code, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        Code = code.Trim();
        Message = message.Trim();
    }

    /// <summary>
    /// TR: İstemcinin parse etmeden kullanabileceği kararlı hata kodunu döndürür.
    /// EN: Gets the stable error code that clients can consume without parsing human-readable text.
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// TR: Hassas iç detay içermeyen güvenli hata açıklamasını döndürür.
    /// EN: Gets the safe error description that does not expose sensitive internal details.
    /// </summary>
    public string Message { get; }
}
