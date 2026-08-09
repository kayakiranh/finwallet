namespace FinWallet.Application.Authentication;

/// <summary>
/// TR: Yeni refresh token üretiminde yalnızca istemciye verilecek ham token ile kalıcı depoda tutulacak tek yönlü token hash'ini birlikte taşır.
/// EN: Carries both the raw refresh token returned only to the client and the one-way token hash persisted by the server when a new refresh token is generated.
/// </summary>
public sealed class RefreshTokenMaterial
{
    /// <summary>
    /// TR: Refresh token materyalini oluşturur; ham token ve hash hiçbir log kaydına yazılmamalıdır.
    /// EN: Creates refresh-token material; neither the raw token nor its hash may be written to logs.
    /// </summary>
    /// <param name="rawToken">
    /// TR: Yalnızca istemciye döndürülecek kriptografik rastgele refresh token.
    /// EN: Cryptographically random refresh token returned only to the client.
    /// </param>
    /// <param name="tokenHash">
    /// TR: Ham token yerine sunucu tarafında kalıcı olarak saklanacak tek yönlü hash.
    /// EN: One-way hash persisted server-side instead of the raw token.
    /// </param>
    public RefreshTokenMaterial(string rawToken, string tokenHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rawToken);
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);

        RawToken = rawToken;
        TokenHash = tokenHash;
    }

    /// <summary>
    /// TR: İstemciye tek sefer döndürülecek ham refresh token değerini döndürür; loglanmamalı veya DB'ye yazılmamalıdır.
    /// EN: Gets the raw refresh-token value returned once to the client; it must not be logged or written to the database.
    /// </summary>
    public string RawToken { get; }

    /// <summary>
    /// TR: Sunucu tarafında token lookup/doğrulaması için kalıcı olarak saklanacak token hash değerini döndürür.
    /// EN: Gets the token hash persisted server-side for token lookup/verification.
    /// </summary>
    public string TokenHash { get; }
}
