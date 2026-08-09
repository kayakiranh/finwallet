namespace FinWallet.Application.Authentication;

/// <summary>
/// TR: Başarılı kimlik doğrulama veya refresh sonucunda istemciye döndürülecek kısa ömürlü JWT access token ve sona erme bilgisini taşır.
/// EN: Carries the short-lived JWT access token and expiration information returned to the client after successful authentication or refresh.
/// </summary>
public sealed class AccessTokenResult
{
    /// <summary>
    /// TR: Access token sonucunu oluşturur; token değeri hassas güvenlik verisidir ve loglanmamalıdır.
    /// EN: Creates an access-token result; the token value is sensitive security data and must not be logged.
    /// </summary>
    /// <param name="token">
    /// TR: İmzalanmış JWT access token metni.
    /// EN: Signed JWT access-token text.
    /// </param>
    /// <param name="expiresAt">
    /// TR: Access token'ın sona ereceği UTC zaman bilgisi.
    /// EN: UTC timestamp at which the access token expires.
    /// </param>
    public AccessTokenResult(string token, DateTimeOffset expiresAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        Token = token;
        ExpiresAt = expiresAt;
    }

    /// <summary>
    /// TR: İmzalanmış JWT access token değerini döndürür; hiçbir log veya audit kaydına yazılmamalıdır.
    /// EN: Gets the signed JWT access-token value; it must never be written to application or audit logs.
    /// </summary>
    public string Token { get; }

    /// <summary>
    /// TR: Access token'ın sona erme UTC zamanını döndürür.
    /// EN: Gets the UTC expiration time of the access token.
    /// </summary>
    public DateTimeOffset ExpiresAt { get; }
}
