namespace FakeBank.Api.Contracts;

/// <summary>
/// TR: FakeBank üzerinde currency bazlı harici hesap açılışı başlatmak için gereken müşteri referansı, currency ve provider request anahtarını taşır.
/// EN: Carries the customer reference, currency and provider request key required to initiate currency-specific external account opening in FakeBank.
/// </summary>
public sealed class OpenAccountRequest
{
    /// <summary>TR: FinWallet müşterisini FakeBank'te temsil eden dış müşteri referansını döndürür veya ayarlar. EN: Gets or sets the external customer reference representing the FinWallet customer in FakeBank.</summary>
    public Guid ExternalCustomerReference { get; init; }

    /// <summary>TR: Açılacak harici banka hesabının para birimini döndürür veya ayarlar. EN: Gets or sets the currency of the external bank account to open.</summary>
    public string Currency { get; init; } = string.Empty;

    /// <summary>TR: Aynı account-opening isteğinin duplicate etkisini engellemek için provider tarafında kullanılan request anahtarını döndürür veya ayarlar. EN: Gets or sets the provider-side request key used to prevent duplicate effects from the same account-opening request.</summary>
    public string RequestKey { get; init; } = string.Empty;
}
