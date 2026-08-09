using FinWallet.Domain.Registration;

namespace FinWallet.Domain.Customers;

/// <summary>
/// TR: FinWallet içerisinde kimlik doğrulaması yapılan son kullanıcıyı temsil eder; finansal ve güvenlik detayları ayrı modellerde tutulduğu için müşteri çekirdeğini küçük tutar.
/// EN: Represents an authenticated end customer in FinWallet and intentionally keeps the customer core small because financial and security details are stored in separate models.
/// </summary>
public sealed class Customer
{
    /// <summary>
    /// TR: Kalıcılık araçlarının nesneyi yeniden oluşturabilmesi için kullanılan ve iş akışlarında doğrudan çağrılmaması gereken kurucudur.
    /// EN: Constructor reserved for persistence materialization and not intended to be called directly by business workflows.
    /// </summary>
    private Customer()
    {
        CountryCode = string.Empty;
        PhoneNumber = string.Empty;
    }

    /// <summary>
    /// TR: Yeni müşteri kaydını SMS doğrulaması bekleyen başlangıç durumunda oluşturur.
    /// EN: Creates a new customer registration in the initial state awaiting SMS verification.
    /// </summary>
    /// <param name="id">
    /// TR: Müşterinin sistem içindeki benzersiz kimliği.
    /// EN: Unique identifier of the customer inside the system.
    /// </param>
    /// <param name="countryCode">
    /// TR: Kayıt policy'si tarafından daha önce kabul edilmiş iki harfli ülke kodu.
    /// EN: Two-letter country code already accepted by the registration policy.
    /// </param>
    /// <param name="phoneNumber">
    /// TR: Formatı ve ülke uyumluluğu daha önce doğrulanmış normalize telefon numarası.
    /// EN: Normalized phone number whose format and country compatibility have already been validated.
    /// </param>
    /// <param name="email">
    /// TR: Finansal bildirimlerde kullanılabilen isteğe bağlı normalize e-posta adresi.
    /// EN: Optional normalized email address that may be used for financial notifications.
    /// </param>
    /// <param name="createdAt">
    /// TR: Müşteri kaydının oluşturulduğu UTC zaman bilgisi.
    /// EN: UTC timestamp at which the customer registration was created.
    /// </param>
    /// <returns>
    /// TR: SMS doğrulaması bekleyen yeni müşteri nesnesini döndürür.
    /// EN: Returns a new customer awaiting SMS verification.
    /// </returns>
    public static Customer Create(
        Guid id,
        string countryCode,
        PhoneNumber phoneNumber,
        string? email,
        DateTimeOffset createdAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(countryCode);
        ArgumentNullException.ThrowIfNull(phoneNumber);

        return new Customer
        {
            Id = id,
            CountryCode = countryCode,
            PhoneNumber = phoneNumber.Value,
            Email = email,
            Status = CustomerStatus.PendingVerification,
            CreatedAt = createdAt
        };
    }

    /// <summary>
    /// TR: Müşterinin sistem içindeki benzersiz kimliğini döndürür; kimlik bilgileri veya oturum verileri bu nesnede tutulmaz.
    /// EN: Gets the customer's unique system identifier; credentials and session data are not stored in this object.
    /// </summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// TR: Kayıt ülkesini ISO benzeri iki harfli kod olarak döndürür ve kayıt uygunluğu kontrolünün sonucunu temsil eder.
    /// EN: Gets the registration country as a two-letter ISO-like code and represents the result of registration eligibility validation.
    /// </summary>
    public string CountryCode { get; private set; }

    /// <summary>
    /// TR: SMS doğrulaması ve müşteri iletişimi için kullanılan normalize telefon numarasını döndürür.
    /// EN: Gets the normalized phone number used for SMS verification and customer communication.
    /// </summary>
    public string PhoneNumber { get; private set; }

    /// <summary>
    /// TR: Finansal e-posta bildirimlerinde kullanılabilen isteğe bağlı müşteri e-posta adresini döndürür.
    /// EN: Gets the optional customer email address that may be used for financial email notifications.
    /// </summary>
    public string? Email { get; private set; }

    /// <summary>
    /// TR: Müşterinin kayıt ve erişim yaşam döngüsündeki mevcut durumunu döndürür.
    /// EN: Gets the customer's current state in the registration and access lifecycle.
    /// </summary>
    public CustomerStatus Status { get; private set; }

    /// <summary>
    /// TR: Müşteri kaydının oluşturulduğu UTC zaman bilgisini döndürür.
    /// EN: Gets the UTC timestamp at which the customer record was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// TR: Başarılı SMS doğrulamasından sonra bekleyen müşteriyi aktif hale getirir; yalnızca bekleyen kayıtlar aktive edilebilir.
    /// EN: Activates a pending customer after successful SMS verification; only pending registrations may be activated.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// TR: Müşteri SMS doğrulaması bekleyen durumda değilse oluşur.
    /// EN: Thrown when the customer is not awaiting SMS verification.
    /// </exception>
    public void Activate()
    {
        if (Status != CustomerStatus.PendingVerification)
        {
            throw new InvalidOperationException($"Customer in '{Status}' state cannot be activated.");
        }

        Status = CustomerStatus.Active;
    }

    /// <summary>
    /// TR: Güvenlik veya operasyonel inceleme sırasında aktif müşterinin yeni işlem başlatmasını engeller.
    /// EN: Blocks an active customer from initiating new operations during a security or operational review.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// TR: Müşteri aktif durumda değilse oluşur.
    /// EN: Thrown when the customer is not active.
    /// </exception>
    public void Block()
    {
        if (Status != CustomerStatus.Active)
        {
            throw new InvalidOperationException($"Customer in '{Status}' state cannot be blocked.");
        }

        Status = CustomerStatus.Blocked;
    }
}
