using FinWallet.Domain.Authentication;

namespace FinWallet.Application.Authentication;

/// <summary>
/// TR: Login, session ve refresh-token rotation işlemlerini MSSQL implementasyonundan ayıran ve güvenlik state değişiklikleri için gerekli atomic persistence sınırlarını tanımlar.
/// EN: Defines the atomic persistence boundaries required for login, session and refresh-token rotation while decoupling those flows from the MSSQL implementation.
/// </summary>
public interface IAuthenticationStore
{
    /// <summary>
    /// TR: Normalize telefon numarasıyla login için Customer ve CustomerCredential kayıtlarını birlikte yükler.
    /// EN: Loads Customer and CustomerCredential records together for login using a normalized phone number.
    /// </summary>
    /// <param name="normalizedPhoneNumber">
    /// TR: Login lookup için kullanılacak normalize uluslararası telefon numarası.
    /// EN: Normalized international phone number used for login lookup.
    /// </param>
    /// <param name="cancellationToken">
    /// TR: Kalıcılık sorgusunun iptal sinyali.
    /// EN: Cancellation signal for the persistence query.
    /// </param>
    /// <returns>
    /// TR: Eşleşen login verisini; kayıt yoksa null döndürür.
    /// EN: Returns matching login data, or null when no record exists.
    /// </returns>
    Task<AuthenticationLoginData?> FindLoginDataAsync(string normalizedPhoneNumber, CancellationToken cancellationToken);

    /// <summary>
    /// TR: Başarısız login sonrası değişen credential lockout state'ini kalıcı hale getirir.
    /// EN: Persists credential lockout state changed after a failed login attempt.
    /// </summary>
    /// <param name="credential">
    /// TR: Başarısız login sayacı veya lock bilgisi güncellenmiş credential nesnesi.
    /// EN: Credential object whose failed-login counter or lock information was updated.
    /// </param>
    /// <param name="cancellationToken">
    /// TR: Kalıcı güncelleme işleminin iptal sinyali.
    /// EN: Cancellation signal for the persistence update.
    /// </param>
    Task UpdateCredentialAsync(CustomerCredential credential, CancellationToken cancellationToken);

    /// <summary>
    /// TR: Başarılı login sonrası credential reset state'i, yeni CustomerSession ve ilk RefreshToken kaydını tek MSSQL transaction içinde kalıcı hale getirir.
    /// EN: Persists the reset credential state, new CustomerSession and initial RefreshToken in one MSSQL transaction after successful login.
    /// </summary>
    /// <param name="credential">
    /// TR: Başarılı login nedeniyle failed-login state'i temizlenmiş credential.
    /// EN: Credential whose failed-login state was cleared after successful login.
    /// </param>
    /// <param name="session">
    /// TR: Yeni müşteri session domain nesnesi.
    /// EN: New customer-session domain object.
    /// </param>
    /// <param name="refreshToken">
    /// TR: Session için oluşturulan ilk refresh-token kaydı.
    /// EN: Initial refresh-token record created for the session.
    /// </param>
    /// <param name="cancellationToken">
    /// TR: Atomik persistence işleminin iptal sinyali.
    /// EN: Cancellation signal for the atomic persistence operation.
    /// </param>
    Task CreateSessionAsync(
        CustomerCredential credential,
        CustomerSession session,
        RefreshToken refreshToken,
        CancellationToken cancellationToken);

    /// <summary>
    /// TR: Ham token'dan türetilmiş hash ile Customer, Session ve RefreshToken state'ini refresh doğrulaması için birlikte yükler.
    /// EN: Loads Customer, Session and RefreshToken state together for refresh verification using a hash derived from the raw token.
    /// </summary>
    /// <param name="tokenHash">
    /// TR: İstemcinin ham refresh token'ından türetilmiş lookup hash değeri.
    /// EN: Lookup hash derived from the client's raw refresh token.
    /// </param>
    /// <param name="cancellationToken">
    /// TR: Kalıcılık sorgusunun iptal sinyali.
    /// EN: Cancellation signal for the persistence query.
    /// </param>
    /// <returns>
    /// TR: Eşleşen refresh context'ini; token bilinmiyorsa null döndürür.
    /// EN: Returns the matching refresh context, or null when the token is unknown.
    /// </returns>
    Task<RefreshAuthenticationData?> FindRefreshDataAsync(string tokenHash, CancellationToken cancellationToken);

    /// <summary>
    /// TR: Eski refresh token'ın DB'de halen kullanılmamış olduğunu koşullu olarak doğrular; yalnızca bu koşul sağlanırsa consume state'i, replacement token ve session aktivitesini tek MSSQL transaction içinde yazar. Aynı token ile yarışan iki isteğin yalnızca biri true dönebilmelidir.
    /// EN: Conditionally verifies that the previous refresh token is still unused in the database and, only when that condition succeeds, writes its consumed state, the replacement token and session activity in one MSSQL transaction. Only one of two racing requests using the same token may return true.
    /// </summary>
    /// <param name="session">
    /// TR: Aktivite zamanı güncellenmiş müşteri session nesnesi.
    /// EN: Customer session object with updated activity time.
    /// </param>
    /// <param name="consumedToken">
    /// TR: Rotation sırasında consume edilmek istenen eski refresh token kaydı.
    /// EN: Previous refresh-token record intended to be consumed during rotation.
    /// </param>
    /// <param name="replacementToken">
    /// TR: Rotation sonucunda oluşturulacak yeni refresh token kaydı.
    /// EN: New refresh-token record to be created by rotation.
    /// </param>
    /// <param name="cancellationToken">
    /// TR: Atomik persistence işleminin iptal sinyali.
    /// EN: Cancellation signal for the atomic persistence operation.
    /// </param>
    /// <returns>
    /// TR: DB'deki token koşullu olarak consume edilip rotation commit edildiyse true; başka bir istek token'ı daha önce consume/revoke ettiyse false döndürür.
    /// EN: Returns true when the database token was conditionally consumed and rotation committed; returns false when another request already consumed or revoked the token.
    /// </returns>
    Task<bool> TryRotateRefreshTokenAsync(
        CustomerSession session,
        RefreshToken consumedToken,
        RefreshToken replacementToken,
        CancellationToken cancellationToken);

    /// <summary>
    /// TR: Refresh-token reuse veya güvenlik olayı tespit edildiğinde session'ı ve ona ait kullanılabilir refresh token'ları atomik olarak revoke eder.
    /// EN: Atomically revokes a session and its usable refresh tokens when refresh-token reuse or another security event is detected.
    /// </summary>
    /// <param name="sessionId">
    /// TR: Revoke edilecek müşteri session kimliği.
    /// EN: Customer-session identifier to revoke.
    /// </param>
    /// <param name="revokedAt">
    /// TR: Güvenlik revoke işleminin gerçekleştiği UTC zaman bilgisi.
    /// EN: UTC timestamp at which the security revocation occurred.
    /// </param>
    /// <param name="cancellationToken">
    /// TR: Atomik revoke işleminin iptal sinyali.
    /// EN: Cancellation signal for the atomic revocation operation.
    /// </param>
    Task RevokeSessionAsync(Guid sessionId, DateTimeOffset revokedAt, CancellationToken cancellationToken);
}
