using System.Text;

namespace FinWallet.Infrastructure.Authentication;

/// <summary>
/// TR: JWT issuer, audience, deployment secret ve güvenli sınırlar içinde ayarlanabilen access-token ömrünü taşır; imzalama algoritması gibi temel güvenlik kararlarını yapılandırılabilir hale getirmez.
/// EN: Carries JWT issuer, audience, deployment secret and an access-token lifetime configurable within safe bounds without making core security decisions such as the signing algorithm configurable.
/// </summary>
public sealed class JwtTokenSettings
{
    /// <summary>
    /// TR: Doğrulanmış JWT deployment ayarlarını oluşturur.
    /// EN: Creates validated JWT deployment settings.
    /// </summary>
    /// <param name="issuer">TR: Access token issuer kimliği. EN: Access-token issuer identifier.</param>
    /// <param name="audience">TR: Access token hedef audience değeri. EN: Target audience of access tokens.</param>
    /// <param name="signingKey">TR: Secret store'dan gelen en az 256-bit eşdeğer imzalama anahtarı. EN: Signing key from the secret store with at least 256-bit equivalent length.</param>
    /// <param name="accessTokenLifetimeMinutes">TR: 2-30 dakika arasında izin verilen access-token yaşam süresi. EN: Allowed access-token lifetime between 2 and 30 minutes.</param>
    public JwtTokenSettings(string issuer, string audience, string signingKey, int accessTokenLifetimeMinutes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(issuer);
        ArgumentException.ThrowIfNullOrWhiteSpace(audience);
        ArgumentException.ThrowIfNullOrWhiteSpace(signingKey);

        if (Encoding.UTF8.GetByteCount(signingKey) < 32)
        {
            throw new ArgumentException("JWT signing key must contain at least 32 UTF-8 bytes.", nameof(signingKey));
        }

        if (accessTokenLifetimeMinutes is < 2 or > 30)
        {
            throw new ArgumentOutOfRangeException(nameof(accessTokenLifetimeMinutes), "Access-token lifetime must be between 2 and 30 minutes.");
        }

        Issuer = issuer.Trim();
        Audience = audience.Trim();
        SigningKey = signingKey;
        AccessTokenLifetime = TimeSpan.FromMinutes(accessTokenLifetimeMinutes);
    }

    /// <summary>TR: FinWallet access-token issuer değerini döndürür. EN: Gets the FinWallet access-token issuer.</summary>
    public string Issuer { get; }

    /// <summary>TR: FinWallet access-token audience değerini döndürür. EN: Gets the FinWallet access-token audience.</summary>
    public string Audience { get; }

    /// <summary>TR: Loglanmaması gereken signing key değerini döndürür. EN: Gets the signing key that must never be logged.</summary>
    public string SigningKey { get; }

    /// <summary>TR: Güvenli sınırlar içinde yapılandırılmış access-token yaşam süresini döndürür. EN: Gets the access-token lifetime configured within safe bounds.</summary>
    public TimeSpan AccessTokenLifetime { get; }
}
