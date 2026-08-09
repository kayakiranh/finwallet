namespace FinWallet.Api.Configuration;

/// <summary>
/// TR: External HTTP provider base URL configuration değerlerini doğrulanmış absolute URI nesnelerine dönüştüren API composition helper'ını sağlar.
/// EN: Provides the API composition helper that converts external HTTP-provider base URL configuration values into validated absolute URI instances.
/// </summary>
public static class IntegrationUriFactory
{
    /// <summary>
    /// TR: Zorunlu integration base URL değerini doğrular, absolute URI'ye dönüştürür ve relative HttpClient route'larının güvenli birleşmesi için son slash karakterini garanti eder.
    /// EN: Validates a required integration base URL, converts it into an absolute URI and guarantees a trailing slash for safe relative HttpClient route composition.
    /// </summary>
    /// <param name="configuredValue">TR: Configuration üzerinden gelen integration base URL değeri. EN: Integration base URL value supplied through configuration.</param>
    /// <param name="configurationKey">TR: Eksik/geçersiz değer durumunda tanılama için kullanılan configuration anahtarı. EN: Configuration key used for diagnostics when the value is missing or invalid.</param>
    /// <returns>TR: Son slash içeren doğrulanmış absolute URI değerini döndürür. EN: Returns a validated absolute URI containing a trailing slash.</returns>
    public static Uri CreateRequiredBaseUri(string? configuredValue, string configurationKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configuredValue, configurationKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(configurationKey);

        var normalized = configuredValue.EndsWith("/", StringComparison.Ordinal)
            ? configuredValue
            : $"{configuredValue}/";

        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri))
        {
            throw new ArgumentException($"Configuration '{configurationKey}' must be an absolute URI.", configurationKey);
        }

        return uri;
    }
}
