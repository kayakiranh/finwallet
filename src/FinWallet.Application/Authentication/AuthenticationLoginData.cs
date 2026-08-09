using FinWallet.Domain.Authentication;
using FinWallet.Domain.Customers;

namespace FinWallet.Application.Authentication;

/// <summary>
/// TR: Login doğrulaması için persistence katmanından birlikte yüklenen Customer ve CustomerCredential domain nesnelerini taşır.
/// EN: Carries the Customer and CustomerCredential domain objects loaded together from persistence for login verification.
/// </summary>
public sealed class AuthenticationLoginData
{
    /// <summary>
    /// TR: Login doğrulama verisini oluşturur.
    /// EN: Creates the login-verification data.
    /// </summary>
    /// <param name="customer">
    /// TR: Login talebinin eşleştiği müşteri domain nesnesi.
    /// EN: Customer domain object matched by the login request.
    /// </param>
    /// <param name="credential">
    /// TR: Müşteriye ait parola ve lockout güvenlik kaydı.
    /// EN: Password and lockout security record belonging to the customer.
    /// </param>
    public AuthenticationLoginData(Customer customer, CustomerCredential credential)
    {
        Customer = customer ?? throw new ArgumentNullException(nameof(customer));
        Credential = credential ?? throw new ArgumentNullException(nameof(credential));
    }

    /// <summary>
    /// TR: Login talebiyle eşleşen müşteri domain nesnesini döndürür.
    /// EN: Gets the customer domain object matched by the login request.
    /// </summary>
    public Customer Customer { get; }

    /// <summary>
    /// TR: Müşterinin parola hash ve lockout bilgilerini taşıyan credential nesnesini döndürür.
    /// EN: Gets the credential object containing the customer's password-hash and lockout data.
    /// </summary>
    public CustomerCredential Credential { get; }
}
