namespace FinWallet.Application.Authentication;

/// <summary>
/// TR: Access token yenileme akışında istemciden alınan opaque refresh token değerini taşır.
/// EN: Carries the opaque refresh-token value received from the client for the access-token refresh flow.
/// </summary>
public sealed class RefreshSessionCommand
{
    /// <summary>
    /// TR: Refresh-session komutunu oluşturur.
    /// EN: Creates the refresh-session command.
    /// </summary>
    /// <param name="refreshToken">
    /// TR: İstemciden alınan ham opaque refresh token; loglanmamalıdır.
    /// EN: Raw opaque refresh token received from the client; it must not be logged.
    /// </param>
    public RefreshSessionCommand(string refreshToken)
    {
        RefreshToken = refreshToken;
    }

    /// <summary>
    /// TR: İstemciden gelen ham refresh token değerini döndürür; hiçbir log alanına yazılmamalıdır.
    /// EN: Gets the raw refresh-token value received from the client; it must not be written to any log field.
    /// </summary>
    public string RefreshToken { get; }
}
