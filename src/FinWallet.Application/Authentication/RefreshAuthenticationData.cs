using FinWallet.Domain.Authentication;
using FinWallet.Domain.Customers;

namespace FinWallet.Application.Authentication;

/// <summary>
/// TR: Refresh-token doğrulaması için persistence katmanından birlikte yüklenen Customer, CustomerSession ve RefreshToken domain nesnelerini taşır.
/// EN: Carries the Customer, CustomerSession and RefreshToken domain objects loaded together from persistence for refresh-token verification.
/// </summary>
public sealed class RefreshAuthenticationData
{
    /// <summary>
    /// TR: Refresh doğrulama verisini oluşturur.
    /// EN: Creates the refresh-verification data.
    /// </summary>
    /// <param name="customer">
    /// TR: Session'ın bağlı olduğu müşteri domain nesnesi.
    /// EN: Customer domain object associated with the session.
    /// </param>
    /// <param name="session">
    /// TR: Refresh token'ın bağlı olduğu müşteri oturumu.
    /// EN: Customer session associated with the refresh token.
    /// </param>
    /// <param name="refreshToken">
    /// TR: İstemcinin gönderdiği token hash'iyle eşleşen refresh token kaydı.
    /// EN: Refresh-token record matching the token hash submitted by the client.
    /// </param>
    public RefreshAuthenticationData(Customer customer, CustomerSession session, RefreshToken refreshToken)
    {
        Customer = customer ?? throw new ArgumentNullException(nameof(customer));
        Session = session ?? throw new ArgumentNullException(nameof(session));
        RefreshToken = refreshToken ?? throw new ArgumentNullException(nameof(refreshToken));
    }

    /// <summary>
    /// TR: Session'ın bağlı olduğu müşteri domain nesnesini döndürür.
    /// EN: Gets the customer domain object associated with the session.
    /// </summary>
    public Customer Customer { get; }

    /// <summary>
    /// TR: Refresh işleminin bağlı olduğu müşteri session nesnesini döndürür.
    /// EN: Gets the customer-session object associated with the refresh operation.
    /// </summary>
    public CustomerSession Session { get; }

    /// <summary>
    /// TR: Sunulan token hash'iyle eşleşen refresh token domain nesnesini döndürür.
    /// EN: Gets the refresh-token domain object matching the submitted token hash.
    /// </summary>
    public RefreshToken RefreshToken { get; }
}
