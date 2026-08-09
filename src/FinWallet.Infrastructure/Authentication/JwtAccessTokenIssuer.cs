using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FinWallet.Application.Authentication;
using Microsoft.IdentityModel.Tokens;

namespace FinWallet.Infrastructure.Authentication;

/// <summary>
/// TR: FinWallet müşterileri için sabit HMAC-SHA256 imzalama algoritması ve güvenli sınırlar içinde yapılandırılmış kısa ömür kullanarak JWT access token üretir.
/// EN: Issues JWT access tokens for FinWallet customers using the fixed HMAC-SHA256 signing algorithm and a short lifetime configured within safe bounds.
/// </summary>
public sealed class JwtAccessTokenIssuer : IAccessTokenIssuer
{
    private readonly JwtTokenSettings _settings;
    private readonly JwtSecurityTokenHandler _tokenHandler = new();
    private readonly SigningCredentials _signingCredentials;

    /// <summary>
    /// TR: Doğrulanmış deployment ayarlarıyla JWT access token issuer servisini oluşturur.
    /// EN: Creates the JWT access-token issuer service with validated deployment settings.
    /// </summary>
    /// <param name="settings">TR: Issuer, audience, signing key ve güvenli access-token ömrünü içeren JWT ayarları. EN: JWT settings containing issuer, audience, signing key and safe access-token lifetime.</param>
    public JwtAccessTokenIssuer(JwtTokenSettings settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SigningKey));
        _signingCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
    }

    /// <summary>
    /// TR: Müşteri ve aktif session kimliğini minimum claim setiyle taşıyan, benzersiz JTI içeren imzalı access token üretir.
    /// EN: Issues a signed access token with a minimal claim set containing customer/session identifiers and a unique JTI.
    /// </summary>
    /// <param name="customerId">TR: JWT subject claim'ine yazılacak müşteri kimliği. EN: Customer identifier written to the JWT subject claim.</param>
    /// <param name="sessionId">TR: Session revoke kontrolü için `sid` claim'ine yazılacak oturum kimliği. EN: Session identifier written to the `sid` claim for revocation checks.</param>
    /// <param name="issuedAt">TR: Token'ın üretildiği UTC zaman. EN: UTC timestamp at which the token is issued.</param>
    /// <returns>TR: Serialize edilmiş JWT ve sona erme zamanını döndürür. EN: Returns the serialized JWT and expiration time.</returns>
    public AccessTokenResult Issue(Guid customerId, Guid sessionId, DateTimeOffset issuedAt)
    {
        if (customerId == Guid.Empty) throw new ArgumentException("Customer identifier cannot be empty.", nameof(customerId));
        if (sessionId == Guid.Empty) throw new ArgumentException("Session identifier cannot be empty.", nameof(sessionId));

        var expiresAt = issuedAt.Add(_settings.AccessTokenLifetime);
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
