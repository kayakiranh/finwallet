using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FinWallet.Application.Authentication;
using Microsoft.IdentityModel.Tokens;

namespace FinWallet.Infrastructure.Authentication;

/// <summary>
/// TR: FinWallet müşterileri için sabit HMAC-SHA256 imzalama algoritması ve kısa sabit ömür kullanarak JWT access token üreten infrastructure servisidir.
/// EN: Infrastructure service that issues JWT access tokens for FinWallet customers using a fixed HMAC-SHA256 signing algorithm and a short fixed lifetime.
/// </summary>
public sealed class JwtAccessTokenIssuer : IAccessTokenIssuer
{
    /// <summary>
    /// TR: Access token'ların sabit yaşam süresini tanımlar; runtime configuration ile uzatılamaz.
    /// EN: Defines the fixed access-token lifetime; it cannot be extended through runtime configuration.
    /// </summary>
    private static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromMinutes(10);

    private readonly JwtTokenSettings _settings;
    private readonly JwtSecurityTokenHandler _tokenHandler = new();
    private readonly SigningCredentials _signingCredentials;

    /// <summary>
    /// TR: Doğrulanmış deployment ayarlarıyla JWT access token issuer servisini oluşturur.
    /// EN: Creates the JWT access-token issuer service with validated deployment settings.
    /// </summary>
    /// <param name="settings">
    /// TR: Issuer, audience ve secret store'dan gelen signing key değerlerini içeren JWT ayarları.
    /// EN: JWT settings containing issuer, audience and signing key obtained from the secret store.
    /// </param>
    public JwtAccessTokenIssuer(JwtTokenSettings settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SigningKey));
        _signingCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
    }

    /// <summary>
    /// TR: Müşteri ve aktif session kimliğini minimum claim setiyle taşıyan, benzersiz JTI içeren ve 10 dakika geçerli imzalı access token üretir.
    /// EN: Issues a signed access token valid for ten minutes with a minimal claim set containing customer/session identifiers and a unique JTI.
    /// </summary>
    /// <param name="customerId">
    /// TR: JWT subject claim'ine yazılacak müşteri kimliği.
    /// EN: Customer identifier written to the JWT subject claim.
    /// </param>
    /// <param name="sessionId">
    /// TR: Session revoke kontrolü için özel `sid` claim'ine yazılacak oturum kimliği.
    /// EN: Session identifier written to the custom `sid` claim for session-revocation checks.
    /// </param>
    /// <param name="issuedAt">
    /// TR: Token'ın üretildiği UTC zaman bilgisi.
    /// EN: UTC timestamp at which the token is issued.
    /// </param>
    /// <returns>
    /// TR: Serialize edilmiş JWT access token ve sona erme zamanını döndürür.
    /// EN: Returns the serialized JWT access token and its expiration time.
    /// </returns>
    public AccessTokenResult Issue(Guid customerId, Guid sessionId, DateTimeOffset issuedAt)
    {
        if (customerId == Guid.Empty)
        {
            throw new ArgumentException("Customer identifier cannot be empty.", nameof(customerId));
        }

        if (sessionId == Guid.Empty)
        {
            throw new ArgumentException("Session identifier cannot be empty.", nameof(sessionId));
        }

        var expiresAt = issuedAt.Add(AccessTokenLifetime);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, customerId.ToString("N")),
            new Claim("sid", sessionId.ToString("N")),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new Claim(
                JwtRegisteredClaimNames.Iat,
                issuedAt.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
                ClaimValueTypes.Integer64)
        };

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            notBefore: issuedAt.UtcDateTime,
            expires: expiresAt.UtcDateTime,
            signingCredentials: _signingCredentials);

        return new AccessTokenResult(_tokenHandler.WriteToken(token), expiresAt);
    }
}
