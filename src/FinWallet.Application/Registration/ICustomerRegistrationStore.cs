using FinWallet.Domain.Authentication;
using FinWallet.Domain.Customers;

namespace FinWallet.Application.Registration;

/// <summary>
/// TR: Registration use-case'ini MSSQL implementasyonundan ayırır ve Customer ile CustomerCredential kayıtlarının atomik olarak oluşturulmasını zorunlu kılan kalıcılık sınırını tanımlar.
/// EN: Decouples registration use cases from the MSSQL implementation and defines a persistence boundary that requires Customer and CustomerCredential records to be created atomically.
/// </summary>
public interface ICustomerRegistrationStore
{
    /// <summary>
    /// TR: Normalize telefon numarasının daha önce kayıtlı herhangi bir müşteri tarafından kullanılıp kullanılmadığını kontrol eder.
    /// EN: Checks whether the normalized phone number is already used by any registered customer.
    /// </summary>
    /// <param name="normalizedPhoneNumber">
    /// TR: Duplicate kontrolü yapılacak normalize uluslararası telefon numarası.
    /// EN: Normalized international phone number to check for duplication.
    /// </param>
    /// <param name="cancellationToken">
    /// TR: Kalıcılık sorgusunun iptal sinyali.
    /// EN: Cancellation signal for the persistence query.
    /// </param>
    /// <returns>
    /// TR: Telefon numarası mevcutsa true döndürür.
    /// EN: Returns true when the phone number already exists.
    /// </returns>
    Task<bool> ExistsByPhoneNumberAsync(string normalizedPhoneNumber, CancellationToken cancellationToken);

    /// <summary>
    /// TR: Pending Customer ile parola credential kaydını tek MSSQL transaction sınırı içinde kalıcı hale getirir.
    /// EN: Persists the pending Customer and password credential within one MSSQL transaction boundary.
    /// </summary>
    /// <param name="customer">
    /// TR: SMS doğrulaması bekleyen yeni müşteri domain nesnesi.
    /// EN: New customer domain object awaiting SMS verification.
    /// </param>
    /// <param name="credential">
    /// TR: Aynı müşteriye ait parola hash/salt güvenlik kaydı.
    /// EN: Password hash/salt security record belonging to the same customer.
    /// </param>
    /// <param name="cancellationToken">
    /// TR: Kalıcı yazma işleminin iptal sinyali.
    /// EN: Cancellation signal for the persistence write.
    /// </param>
    Task CreatePendingCustomerAsync(Customer customer, CustomerCredential credential, CancellationToken cancellationToken);

    /// <summary>
    /// TR: SMS doğrulaması veya registration resume işlemi için müşteri kaydını kimliğiyle yükler.
    /// EN: Loads a customer record by identifier for SMS verification or registration-resume operations.
    /// </summary>
    /// <param name="customerId">
    /// TR: Yüklenecek müşteri kimliği.
    /// EN: Identifier of the customer to load.
    /// </param>
    /// <param name="cancellationToken">
    /// TR: Kalıcılık sorgusunun iptal sinyali.
    /// EN: Cancellation signal for the persistence query.
    /// </param>
    /// <returns>
    /// TR: Müşteri bulunduysa domain nesnesini, bulunamadıysa null döndürür.
    /// EN: Returns the customer domain object when found; otherwise returns null.
    /// </returns>
    Task<Customer?> FindCustomerAsync(Guid customerId, CancellationToken cancellationToken);

    /// <summary>
    /// TR: Customer aggregate üzerindeki registration durum değişikliğini kalıcı hale getirir.
    /// EN: Persists a registration state change made on the Customer aggregate.
    /// </summary>
    /// <param name="customer">
    /// TR: Durumu değiştirilmiş müşteri domain nesnesi.
    /// EN: Customer domain object whose state has changed.
    /// </param>
    /// <param name="cancellationToken">
    /// TR: Kalıcı güncelleme işleminin iptal sinyali.
    /// EN: Cancellation signal for the persistence update.
    /// </param>
    Task UpdateCustomerAsync(Customer customer, CancellationToken cancellationToken);
}
