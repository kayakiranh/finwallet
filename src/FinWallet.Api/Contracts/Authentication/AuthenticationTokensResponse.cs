namespace FinWallet.Api.Contracts.Authentication;

/// <summary>
/// TR: Başarılı login veya refresh işleminden sonra istemciye döndürülen müşteri/session kimlikleri ile access/refresh token bilgilerini tanımlar.
/// EN: Defines customer/session identifiers and access/refresh token information returned after a successful login or refresh operation.
/// </summary>
public sealed class AuthenticationTokensResponse
{
    /// <summary>
    /// TR: Authentication token response nesnesini oluşturur; token değerleri hassastır ve loglanmamalıdır.
    /// EN: Creates the authentication-token response; token values are sensitive and must not be logged.
    /// </summary>
    /// <param name="customerId">TR: Kimliği doğrulanan müşteri kimliği. EN: Authenticated customer identifier.</param>
    /// <param name="sessionId">TR: Token çiftinin bağlı olduğu session kimliği. EN: Session identifier associated with the token pair.</param>
    /// <param name="accessToken">TR: Kısa ömürlü JWT access token. EN: Short-lived JWT access token.</param>
    /// <param name="accessTokenExpiresAt">TR: Access token sona erme UTC zamanı. EN: UTC expiration time of the access token.</param>
    /// <param name="refreshToken">TR: Opaque refresh token. EN: Opaque refresh token.</param>
    /// <param name="refreshTokenExpiresAt">TR: Refresh token sona erme UTC zamanı. EN: UTC expiration time of the refresh token.</param>
    public AuthenticationTokensResponse(
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

    /// <summary>TR: Kimliği doğrulanan müşteri kimliğini döndürür. EN: Gets the authenticated customer identifier.</summary>
    public Guid CustomerId { get; }

    /// <summary>TR: Token çiftinin bağlı olduğu session kimliğini döndürür. EN: Gets the session identifier associated with the token pair.</summary>
    public Guid SessionId { get; }

    /// <summary>TR: Kısa ömürlü JWT access token değerini döndürür; loglanmamalıdır. EN: Gets the short-lived JWT access-token value; it must not be logged.</summary>
    public string AccessToken { get; }

    /// <summary>TR: Access token sona erme UTC zamanını döndürür. EN: Gets the UTC expiration time of the access token.</summary>
    public DateTimeOffset AccessTokenExpiresAt { get; }

    /// <summary>TR: Opaque refresh token değerini döndürür; loglanmamalıdır. EN: Gets the opaque refresh-token value; it must not be logged.</summary>
    public string RefreshToken { get; }

    /// <summary>TR: Refresh token sona erme UTC zamanını döndürür. EN: Gets the UTC expiration time of the refresh token.</summary>
    public DateTimeOffset RefreshTokenExpiresAt { get; }
}
