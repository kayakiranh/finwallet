using FinWallet.Domain.Authentication;
using FinWallet.Domain.Customers;

namespace FinWallet.Application.Authentication;

/// <summary>
/// TR: Opaque refresh token doğrulaması, tek kullanımlık rotation, reuse detection ve yeni access token üretimini orkestre eder.
/// EN: Orchestrates opaque refresh-token verification, single-use rotation, reuse detection and issuance of a new access token.
/// </summary>
public sealed class RefreshSessionHandler
{
    /// <summary>
    /// TR: Rotation ile üretilen her yeni refresh token için sabit maksimum yaşam süresini tanımlar.
    /// EN: Defines the fixed maximum lifetime for each new refresh token issued by rotation.
    /// </summary>
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(14);

    private readonly IAuthenticationStore _authenticationStore;
    private readonly IRefreshTokenGenerator _refreshTokenGenerator;
    private readonly IAccessTokenIssuer _accessTokenIssuer;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// TR: Refresh-session use-case bağımlılıklarıyla handler'ı oluşturur.
    /// EN: Creates the refresh-session handler with its use-case dependencies.
    /// </summary>
    /// <param name="authenticationStore">
    /// TR: Session ve refresh-token state'ini atomik olarak yöneten persistence sınırı.
    /// EN: Persistence boundary managing session and refresh-token state atomically.
    /// </param>
    /// <param name="refreshTokenGenerator">
    /// TR: Ham refresh token'dan lookup hash üreten ve replacement token oluşturan servis.
    /// EN: Service hashing raw refresh tokens for lookup and generating replacement tokens.
    /// </param>
    /// <param name="accessTokenIssuer">
    /// TR: Yeni kısa ömürlü JWT access token üreten servis.
    /// EN: Service issuing the new short-lived JWT access token.
    /// </param>
    /// <param name="timeProvider">
    /// TR: Test edilebilir UTC zaman kaynağı.
    /// EN: Testable UTC time source.
    /// </param>
    public RefreshSessionHandler(
        IAuthenticationStore authenticationStore,
        IRefreshTokenGenerator refreshTokenGenerator,
        IAccessTokenIssuer accessTokenIssuer,
        TimeProvider timeProvider)
    {
        _authenticationStore = authenticationStore ?? throw new ArgumentNullException(nameof(authenticationStore));
        _refreshTokenGenerator = refreshTokenGenerator ?? throw new ArgumentNullException(nameof(refreshTokenGenerator));
        _accessTokenIssuer = accessTokenIssuer ?? throw new ArgumentNullException(nameof(accessTokenIssuer));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <summary>
    /// TR: Sunulan refresh token'ı hash ile lookup eder, reuse tespitinde session'ı revoke eder, geçerli token'ı DB compare-and-set benzeri koşullu işlemle replacement token'a rotate eder ve yalnızca başarılı rotation sonrasında yeni token çiftini döndürür.
    /// EN: Looks up the submitted refresh token by hash, revokes the session on reuse detection, rotates a valid token to a replacement through a database compare-and-set-like conditional operation and returns a new token pair only after successful rotation.
    /// </summary>
    /// <param name="command">
    /// TR: Ham refresh token değerini taşıyan refresh komutu.
    /// EN: Refresh command carrying the raw refresh-token value.
    /// </param>
    /// <param name="cancellationToken">
    /// TR: Kalıcılık işlemlerine iletilecek request iptal sinyali.
    /// EN: Request cancellation signal propagated to persistence operations.
    /// </param>
    /// <returns>
    /// TR: Aynı session'a bağlı yeni access/refresh token çiftini döndürür.
    /// EN: Returns a new access/refresh token pair associated with the same session.
    /// </returns>
    /// <exception cref="InvalidRefreshTokenException">
    /// TR: Token bilinmiyor, geçersiz, süresi dolmuş, revoke edilmiş veya session/customer yenilemeye uygun değilse oluşur.
    /// EN: Thrown when the token is unknown, invalid, expired, revoked or the session/customer is not eligible for refresh.
    /// </exception>
    /// <exception cref="RefreshTokenReuseDetectedException">
    /// TR: Daha önce consume edilmiş token tekrar kullanılırsa veya aynı token ile eş zamanlı ikinci rotation isteği yarışmayı kaybederse session revoke edilerek oluşur.
    /// EN: Thrown with session revocation when a previously consumed token is reused or when a concurrent second rotation using the same token loses the atomic race.
    /// </exception>
    public async Task<AuthenticationTokensResult> HandleAsync(
        RefreshSessionCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.RefreshToken);

        var now = _timeProvider.GetUtcNow();
        var tokenHash = _refreshTokenGenerator.Hash(command.RefreshToken);
        var refreshData = await _authenticationStore.FindRefreshDataAsync(tokenHash, cancellationToken);

        if (refreshData is null)
        {
            throw new InvalidRefreshTokenException();
        }

        if (refreshData.RefreshToken.IndicatesReuse())
        {
            await RevokeForReuseAsync(refreshData.Session.Id, now, cancellationToken);
        }

        if (refreshData.Customer.Status != CustomerStatus.Active
            || !refreshData.Session.IsActive(now)
            || !refreshData.RefreshToken.IsUsable(now))
        {
            throw new InvalidRefreshTokenException();
        }

        var replacementExpiresAt = Min(now.Add(RefreshTokenLifetime), refreshData.Session.ExpiresAt);
        if (replacementExpiresAt <= now)
        {
            throw new InvalidRefreshTokenException();
        }

        var replacementMaterial = _refreshTokenGenerator.Generate();
        var replacementTokenId = Guid.NewGuid();
        var replacementToken = RefreshToken.Create(
            replacementTokenId,
            refreshData.Session.Id,
            replacementMaterial.TokenHash,
            now,
            replacementExpiresAt);

        refreshData.RefreshToken.Consume(now, replacementTokenId);
        refreshData.Session.Touch(now);

        var rotated = await _authenticationStore.TryRotateRefreshTokenAsync(
            refreshData.Session,
            refreshData.RefreshToken,
            replacementToken,
            cancellationToken);

        if (!rotated)
        {
            await RevokeForReuseAsync(refreshData.Session.Id, now, cancellationToken);
        }

        var accessToken = _accessTokenIssuer.Issue(
            refreshData.Customer.Id,
            refreshData.Session.Id,
            now);

        return new AuthenticationTokensResult(
            refreshData.Customer.Id,
            refreshData.Session.Id,
            accessToken.Token,
            accessToken.ExpiresAt,
            replacementMaterial.RawToken,
            replacementExpiresAt);
    }

    /// <summary>
    /// TR: Refresh-token replay/reuse tespitinde session token ailesini revoke eder ve güvenlik hatasını üretir.
    /// EN: Revokes the session token family after refresh-token replay/reuse detection and raises the security error.
    /// </summary>
    /// <param name="sessionId">
    /// TR: Token ailesi revoke edilecek session kimliği.
    /// EN: Session identifier whose token family is revoked.
    /// </param>
    /// <param name="detectedAt">
    /// TR: Reuse/replay olayının tespit edildiği UTC zaman bilgisi.
    /// EN: UTC timestamp at which the reuse/replay event was detected.
    /// </param>
    /// <param name="cancellationToken">
    /// TR: Revoke persistence işleminin iptal sinyali.
    /// EN: Cancellation signal for the revocation persistence operation.
    /// </param>
    /// <exception cref="RefreshTokenReuseDetectedException">
    /// TR: Session revoke talebi tamamlandıktan sonra her zaman oluşur.
    /// EN: Always thrown after the session revocation request completes.
    /// </exception>
    private async Task RevokeForReuseAsync(
        Guid sessionId,
        DateTimeOffset detectedAt,
        CancellationToken cancellationToken)
    {
        await _authenticationStore.RevokeSessionAsync(sessionId, detectedAt, cancellationToken);
        throw new RefreshTokenReuseDetectedException();
    }

    /// <summary>
    /// TR: Refresh token'ın session mutlak sona erme zamanını aşmaması için iki UTC zamandan daha erken olanı seçer.
    /// EN: Selects the earlier of two UTC timestamps so a refresh token cannot outlive the session's absolute expiration.
    /// </summary>
    /// <param name="first">
    /// TR: Karşılaştırılacak ilk UTC zaman bilgisi.
    /// EN: First UTC timestamp to compare.
    /// </param>
    /// <param name="second">
    /// TR: Karşılaştırılacak ikinci UTC zaman bilgisi.
    /// EN: Second UTC timestamp to compare.
    /// </param>
    /// <returns>
    /// TR: Daha erken UTC zamanını döndürür.
    /// EN: Returns the earlier UTC timestamp.
    /// </returns>
    private static DateTimeOffset Min(DateTimeOffset first, DateTimeOffset second)
    {
        return first <= second ? first : second;
    }
}
