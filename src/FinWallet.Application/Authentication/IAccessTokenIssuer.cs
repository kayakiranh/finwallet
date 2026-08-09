namespace FinWallet.Application.Authentication;

/// <summary>
/// TR: Application katmanını somut JWT kütüphanesi ve imzalama detaylarından ayıran kısa ömürlü access token üretim sınırını tanımlar.
/// EN: Defines the short-lived access-token issuance boundary that decouples the Application layer from the concrete JWT library and signing details.
/// </summary>
public interface IAccessTokenIssuer
{
    /// <summary>
    /// TR: Müşteri ve aktif oturum kimliğine bağlı imzalı access token üretir.
    /// EN: Issues a signed access token bound to the customer and active session identifiers.
    /// </summary>
    /// <param name="customerId">
    /// TR: Token'ın subject kimliği olarak kullanılacak müşteri kimliği.
    /// EN: Customer identifier to use as the token subject.
    /// </param>
    /// <param name="sessionId">
    /// TR: Token revoke/session kontrolü için token'a bağlanacak oturum kimliği.
    /// EN: Session identifier embedded for revocation/session correlation.
    /// </param>
    /// <param name="issuedAt">
    /// TR: Token'ın üretildiği UTC zaman bilgisi.
    /// EN: UTC timestamp at which the token is issued.
    /// </param>
    /// <returns>
    /// TR: İmzalı access token ve sona erme bilgisini döndürür.
    /// EN: Returns the signed access token and expiration information.
    /// </returns>
    AccessTokenResult Issue(Guid customerId, Guid sessionId, DateTimeOffset issuedAt);
}
