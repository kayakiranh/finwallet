using System.Text;

namespace FinWallet.Infrastructure.Authentication;

/// <summary>
/// TR: JWT issuer, audience ve deployment secret değerlerini taşır; algoritma veya token ömrü gibi güvenlik kararlarını yapılandırılabilir hale getirmez.
/// EN: Carries JWT issuer, audience and deployment-secret values without making security decisions such as algorithm or token lifetime configurable.
/// </summary>
public sealed class JwtTokenSettings
{
    /// <summary>
    /// TR: Doğrulanmış JWT deployment ayarlarını oluşturur.
    /// EN: Creates validated JWT deployment settings.
    /// </summary>
    /// <param name="issuer">
    /// TR: Access token'ları üreten FinWallet issuer kimliği.
    /// EN: FinWallet issuer identifier that creates access tokens.
    /// </param>
    /// <param name="audience">
    /// TR: Access token'ların hedeflediği FinWallet API audience değeri.
    /// EN: FinWallet API audience targeted by access tokens.
    /// </param>
    /// <param name="signingKey">
    /// TR: Deployment secret store üzerinden sağlanan en az 256-bit eşdeğer uzunlukta imzalama anahtarı.
    /// EN: Signing key supplied by the deployment secret store with at least 256-bit equivalent length.
    /// </param>
    /// <exception cref="ArgumentException">
    /// TR: Issuer, audience veya signing key boşsa ya da signing key 32 UTF-8 byte'tan kısa ise oluşur.
    /// EN: Thrown when issuer, audience or signing key is empty, or when the signing key is shorter than 32 UTF-8 bytes.
    /// </exception>
    public JwtTokenSettings(string issuer, string audience, string signingKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(issuer);
        ArgumentException.ThrowIfNullOrWhiteSpace(audience);
        ArgumentException.ThrowIfNullOrWhiteSpace(signingKey);

        if (Encoding.UTF8.GetByteCount(signingKey) < 32)
        {
            throw new ArgumentException("JWT signing key must contain at least 32 UTF-8 bytes.", nameof(signingKey));
        }

        Issuer = issuer.Trim();
        Audience = audience.Trim();
        SigningKey = signingKey;
    }

    /// <summary>
    /// TR: FinWallet access token issuer değerini döndürür.
    /// EN: Gets the FinWallet access-token issuer value.
    /// </summary>
    public string Issuer { get; }

    /// <summary>
    /// TR: FinWallet access token audience değerini döndürür.
    /// EN: Gets the FinWallet access-token audience value.
    /// </summary>
    public string Audience { get; }

    /// <summary>
    /// TR: Deployment secret store'dan alınan imzalama anahtarını döndürür; uygulama loglarına yazılmamalıdır.
    /// EN: Gets the signing key obtained from the deployment secret store; it must not be written to application logs.
    /// </summary>
    public string SigningKey { get; }
}
