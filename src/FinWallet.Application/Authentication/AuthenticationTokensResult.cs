namespace FinWallet.Application.Authentication;

/// <summary>
/// TR: Başarılı login veya refresh işlemi sonucunda istemciye döndürülen access/refresh token çifti ile session ve sona erme bilgilerini taşır.
/// EN: Carries the access/refresh token pair, session identifier and expiration information returned to the client after successful login or refresh.
/// </summary>
public sealed class AuthenticationTokensResult
{
    /// <summary>
    /// TR: Authentication token sonucunu oluşturur; token değerleri hassastır ve loglanmamalıdır.
    /// EN: Creates the authentication-token result; token values are sensitive and must not be logged.
    /// </summary>
    /// <param name="customerId">
    /// TR: Kimliği doğrulanan müşteri kimliği.
    /// EN: Identifier of the authenticated customer.
    /// </param>
    /// <param name="sessionId">
    /// TR: Token çiftinin bağlı olduğu müşteri session kimliği.
    /// EN: Customer-session identifier to which the token pair belongs.
    /// </param>
    /// <param name="accessToken">
    /// TR: Kısa ömürlü imzalı JWT access token.
    /// EN: Short-lived signed JWT access token.
    /// </param>
    /// <param name="accessTokenExpiresAt">
    /// TR: Access token sona erme UTC zamanı.
    /// EN: UTC expiration time of the access token.
    /// </param>
    /// <param name="refreshToken">
    /// TR: Yalnızca istemciye döndürülen opaque refresh token.
    /// EN: Opaque refresh token returned only to the client.
    /// </param>
    /// <param name="refreshTokenExpiresAt">
    /// TR: Refresh token sona erme UTC zamanı.
    /// EN: UTC expiration time of the refresh token.
    /// </param>
    public AuthenticationTokensResult(
        Guid customerId,
        Guid sessionId,
        string accessToken,
        DateTimeOffset accessTokenExpiresAt,
        string refreshToken,
        DateTimeOffset refreshTokenExpiresAt)
    {
        CustomerId = customerId;
        SessionId = sessionId;
        AccessToken = accessToken;
        AccessTokenExpiresAt = accessTokenExpiresAt;
        RefreshToken = refreshToken;
        RefreshTokenExpiresAt = refreshTokenExpiresAt;
    }

    /// <summary>
    /// TR: Kimliği doğrulanan müşteri kimliğini döndürür.
    /// EN: Gets the authenticated customer identifier.
    /// </summary>
    public Guid CustomerId { get; }

    /// <summary>
    /// TR: Token çiftinin bağlı olduğu müşteri session kimliğini döndürür.
    /// EN: Gets the customer-session identifier to which the token pair belongs.
    /// </summary>
    public Guid SessionId { get; }

    /// <summary>
    /// TR: Kısa ömürlü JWT access token değerini döndürür; loglanmamalıdır.
    /// EN: Gets the short-lived JWT access-token value; it must not be logged.
    /// </summary>
    public string AccessToken { get; }

    /// <summary>
    /// TR: Access token sona erme UTC zamanını döndürür.
    /// EN: Gets the UTC expiration time of the access token.
    /// </summary>
    public DateTimeOffset AccessTokenExpiresAt { get; }

    /// <summary>
    /// TR: Opaque refresh token değerini döndürür; yalnızca istemciye verilmeli ve loglanmamalıdır.
    /// EN: Gets the opaque refresh-token value; it must only be returned to the client and must not be logged.
    /// </summary>
    public string RefreshToken { get; }

    /// <summary>
    /// TR: Refresh token sona erme UTC zamanını döndürür.
    /// EN: Gets the UTC expiration time of the refresh token.
    /// </summary>
    public DateTimeOffset RefreshTokenExpiresAt { get; }
}
