using System.Security.Cryptography;
using System.Text;
using FinWallet.Application.Authentication;

namespace FinWallet.Infrastructure.Authentication;

/// <summary>
/// TR: Refresh token'ları kriptografik rastgele byte'lardan URL-safe biçimde üretir ve sunucuda yalnızca SHA-256 hash'inin kalıcı saklanmasını sağlar.
/// EN: Generates refresh tokens from cryptographically random bytes in a URL-safe form and ensures that only their SHA-256 hash needs to be persisted server-side.
/// </summary>
public sealed class SecureRefreshTokenGenerator : IRefreshTokenGenerator
{
    /// <summary>
    /// TR: Her refresh token için üretilecek kriptografik rastgele byte sayısını tanımlar.
    /// EN: Defines the number of cryptographically random bytes generated for each refresh token.
    /// </summary>
    private const int TokenByteLength = 64;

    /// <summary>
    /// TR: Yeni opaque refresh token üretir ve ham token ile tek yönlü hash'ini birlikte döndürür.
    /// EN: Generates a new opaque refresh token and returns the raw token together with its one-way hash.
    /// </summary>
    /// <returns>
    /// TR: İstemciye verilecek ham token ve sunucuda saklanacak hash materyalini döndürür.
    /// EN: Returns the raw token for the client and hash material for server-side persistence.
    /// </returns>
    public RefreshTokenMaterial Generate()
    {
        var randomBytes = RandomNumberGenerator.GetBytes(TokenByteLength);
        var rawToken = ToBase64Url(randomBytes);

        return new RefreshTokenMaterial(rawToken, Hash(rawToken));
    }

    /// <summary>
    /// TR: Ham refresh token'ı deterministik SHA-256 hash biçimine dönüştürür; hash değeri lookup için kullanılabilir ancak loglanmamalıdır.
    /// EN: Converts a raw refresh token into a deterministic SHA-256 hash form; the hash may be used for lookup but must not be logged.
    /// </summary>
    /// <param name="rawToken">
    /// TR: İstemciden alınan veya yeni üretilen ham refresh token.
    /// EN: Raw refresh token received from the client or newly generated.
    /// </param>
    /// <returns>
    /// TR: Büyük harf hexadecimal SHA-256 token hash değerini döndürür.
    /// EN: Returns the uppercase hexadecimal SHA-256 token hash value.
    /// </returns>
    public string Hash(string rawToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rawToken);

        var tokenBytes = Encoding.UTF8.GetBytes(rawToken);
        var hashBytes = SHA256.HashData(tokenBytes);
        return Convert.ToHexString(hashBytes);
    }

    /// <summary>
    /// TR: Rastgele byte dizisini padding içermeyen URL-safe Base64 metnine dönüştürür.
    /// EN: Converts a random byte array to URL-safe Base64 text without padding.
    /// </summary>
    /// <param name="bytes">
    /// TR: URL-safe token metnine dönüştürülecek rastgele byte dizisi.
    /// EN: Random bytes to convert into URL-safe token text.
    /// </param>
    /// <returns>
    /// TR: URL-safe ve padding içermeyen Base64 metnini döndürür.
    /// EN: Returns URL-safe Base64 text without padding.
    /// </returns>
    private static string ToBase64Url(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
