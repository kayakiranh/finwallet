namespace FinWallet.Api.Contracts.Authentication;

/// <summary>
/// TR: Access token yenileme endpoint'ine gönderilen opaque refresh token alanını tanımlar.
/// EN: Defines the opaque refresh-token field submitted to the access-token refresh endpoint.
/// </summary>
public sealed class RefreshSessionRequest
{
    /// <summary>
    /// TR: İstemcinin sahip olduğu ham opaque refresh token değerini döndürür veya ayarlar; loglanmamalıdır.
    /// EN: Gets or sets the raw opaque refresh-token value held by the client; it must not be logged.
    /// </summary>
    public string RefreshToken { get; set; } = string.Empty;
}
