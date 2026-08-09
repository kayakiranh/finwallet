using FinWallet.Domain.Authentication;

namespace FinWallet.Application.Authentication;

/// <summary>
/// TR: Login, session ve refresh-token işlemlerini MSSQL implementasyonundan ayıran ve güvenlik state değişiklikleri için atomik persistence sınırlarını tanımlar.
/// EN: Defines atomic persistence boundaries for login, session and refresh-token operations while decoupling them from the MSSQL implementation.
/// </summary>
public interface IAuthenticationStore
{
    /// <summary>
    /// TR: Normalize telefon numarasıyla login için Customer ve CustomerCredential state'ini birlikte yükler.
    /// EN: Loads Customer and CustomerCredential state together for login using a normalized phone number.
    /// </summary>
    /// <param name="normalizedPhoneNumber">TR: Login lookup için normalize telefon numarası. EN: Normalized phone number used for login lookup.</param>
    /// <param name="cancellationToken">TR: Persistence sorgusunun iptal sinyali. EN: Cancellation signal for the persistence query.</param>
    /// <returns>TR: Eşleşen login verisini; kayıt yoksa null döndürür. EN: Returns matching login data, or null when no record exists.</returns>
    Task<AuthenticationLoginData?> FindLoginDataAsync(string normalizedPhoneNumber, CancellationToken cancellationToken);

    /// <summary>
    /// TR: Parola doğrulaması başarısız olduğunda credential satırını kısa DB lock altında yeniden yükler, domain lockout kuralını güncel state'e uygular ve sonucu atomik olarak kalıcılaştırır; paralel yanlış login'lerde lost-update oluşmasını engeller.
    /// EN: When password verification fails, reloads the credential row under a short database lock, applies the domain lockout rule to current state and persists it atomically, preventing lost updates across concurrent failed logins.
    /// </summary>
    /// <param name="customerId">TR: Başarısız login'in ait olduğu müşteri kimliği. EN: Customer identifier associated with the failed login.</param>
    /// <param name="failedAt">TR: Başarısız login'in UTC zamanı. EN: UTC timestamp of the failed login.</param>
    /// <param name="cancellationToken">TR: Atomik persistence işleminin iptal sinyali. EN: Cancellation signal for the atomic persistence operation.</param>
    Task RegisterFailedLoginAsync(Guid customerId, DateTimeOffset failedAt, CancellationToken cancellationToken);

    /// <summary>
    /// TR: Başarılı parola doğrulaması sonrasında credential satırını kısa DB lock altında kontrol eder; halen geçici lock yoksa failed-login state'ini temizleyip yeni session ve ilk refresh token'ı tek transaction içinde oluşturur.
    /// EN: After successful password verification, checks the credential row under a short database lock and, only when no temporary lock is currently active, clears failed-login state and creates the new session plus initial refresh token in one transaction.
    /// </summary>
    /// <param name="credential">TR: Parola doğrulamasında kullanılan credential snapshot'ı. EN: Credential snapshot used for password verification.</param>
    /// <param name="session">TR: Oluşturulmak istenen yeni müşteri session'ı. EN: New customer session intended to be created.</param>
    /// <param name="refreshToken">TR: Session'a ait ilk refresh token kaydı. EN: Initial refresh-token record associated with the session.</param>
    /// <param name="cancellationToken">TR: Atomik persistence işleminin iptal sinyali. EN: Cancellation signal for the atomic persistence operation.</param>
    /// <returns>TR: Session transaction'ı commit edildiyse true; paralel denemeler credential'ı geçici lock'a sokmuşsa false döndürür. EN: Returns true when the session transaction commits; false when concurrent attempts have placed the credential under a temporary lock.</returns>
    Task<bool> TryCreateSessionAsync(
        CustomerCredential credential,
        CustomerSession session,
        RefreshToken refreshToken,
        CancellationToken cancellationToken);

    /// <summary>
    /// TR: Ham token'dan türetilmiş hash ile Customer, Session ve RefreshToken state'ini refresh doğrulaması için birlikte yükler.
    /// EN: Loads Customer, Session and RefreshToken state together for refresh verification using a hash derived from the raw token.
    /// </summary>
    /// <param name="tokenHash">TR: Refresh-token lookup hash'i. EN: Refresh-token lookup hash.</param>
    /// <param name="cancellationToken">TR: Persistence sorgusunun iptal sinyali. EN: Cancellation signal for the persistence query.</param>
    /// <returns>TR: Eşleşen refresh state'ini; token bilinmiyorsa null döndürür. EN: Returns matching refresh state, or null when the token is unknown.</returns>
    Task<RefreshAuthenticationData?> FindRefreshDataAsync(string tokenHash, CancellationToken cancellationToken);

    /// <summary>
    /// TR: Eski refresh token'ın DB'de halen kullanılmamış olduğunu koşullu doğrular; yalnızca koşul sağlanırsa consume state'i, replacement token ve session aktivitesini tek transaction içinde yazar.
    /// EN: Conditionally verifies that the old refresh token is still unused in the database and writes its consumed state, replacement token and session activity in one transaction only when that condition succeeds.
    /// </summary>
    /// <param name="session">TR: Aktivite zamanı güncellenmiş session state'i. EN: Session state with updated activity time.</param>
    /// <param name="consumedToken">TR: Consume edilmek istenen eski refresh token. EN: Old refresh token intended to be consumed.</param>
    /// <param name="replacementToken">TR: Rotation replacement refresh token. EN: Replacement refresh token created by rotation.</param>
    /// <param name="cancellationToken">TR: Atomik persistence işleminin iptal sinyali. EN: Cancellation signal for the atomic persistence operation.</param>
    /// <returns>TR: Bu request rotation yarışını kazanıp commit ettiyse true; başka request token'ı önce consume/revoke ettiyse false döndürür. EN: Returns true when this request wins and commits the rotation; false when another request already consumed or revoked the token.</returns>
    Task<bool> TryRotateRefreshTokenAsync(
        CustomerSession session,
        RefreshToken consumedToken,
        RefreshToken replacementToken,
        CancellationToken cancellationToken);

    /// <summary>
    /// TR: Refresh-token reuse veya başka güvenlik olayı tespit edildiğinde session ve token ailesini atomik revoke eder.
    /// EN: Atomically revokes a session and its token family when refresh-token reuse or another security event is detected.
    /// </summary>
    /// <param name="sessionId">TR: Revoke edilecek session kimliği. EN: Session identifier to revoke.</param>
    /// <param name="revokedAt">TR: Güvenlik revoke UTC zamanı. EN: UTC security-revocation time.</param>
    /// <param name="cancellationToken">TR: Atomik revoke işleminin iptal sinyali. EN: Cancellation signal for the atomic revocation operation.</param>
    Task RevokeSessionAsync(Guid sessionId, DateTimeOffset revokedAt, CancellationToken cancellationToken);
}
