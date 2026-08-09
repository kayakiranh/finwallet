namespace FinWallet.Application.Authentication;

/// <summary>
/// TR: Application katmanını kriptografik refresh-token üretimi ve token hashing detaylarından ayıran güvenlik sınırını tanımlar.
/// EN: Defines the security boundary that decouples the Application layer from cryptographic refresh-token generation and token-hashing details.
/// </summary>
public interface IRefreshTokenGenerator
{
    /// <summary>
    /// TR: Yeni kriptografik rastgele refresh token ve yalnızca sunucu tarafında saklanacak hash eşini üretir.
    /// EN: Generates a new cryptographically random refresh token and its server-side persisted hash pair.
    /// </summary>
    /// <returns>
    /// TR: Ham refresh token ve hash materyalini döndürür.
    /// EN: Returns the raw refresh token and hash material.
    /// </returns>
    RefreshTokenMaterial Generate();

    /// <summary>
    /// TR: İstemciden gelen ham refresh token'ı kalıcı lookup için kullanılan deterministik tek yönlü hash biçimine dönüştürür.
    /// EN: Converts a raw refresh token received from the client into the deterministic one-way hash format used for persistent lookup.
    /// </summary>
    /// <param name="rawToken">
    /// TR: İstemciden gelen ham refresh token; loglanmamalıdır.
    /// EN: Raw refresh token received from the client; it must not be logged.
    /// </param>
    /// <returns>
    /// TR: Kalıcı lookup için kullanılacak token hash değerini döndürür.
    /// EN: Returns the token hash used for persistent lookup.
    /// </returns>
    string Hash(string rawToken);
}
