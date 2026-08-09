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
    /// <param name="id">TR: Müşterinin sistem içindeki benzersiz kimliği. EN: Unique identifier of the customer inside the system.</param>
    /// <param name="countryCode">TR: Kayıt policy'si tarafından kabul edilmiş iki harfli ülke kodu. EN: Two-letter country code already accepted by the registration policy.</param>
    /// <param name="phoneNumber">TR: Formatı ve ülke uyumluluğu doğrulanmış normalize telefon numarası. EN: Normalized phone number with validated format and country compatibility.</param>
    /// <param name="email">TR: İsteğe bağlı normalize e-posta adresi. EN: Optional normalized email address.</param>
    /// <param name="createdAt">TR: Müşteri kaydının oluşturulduğu UTC zaman bilgisi. EN: UTC timestamp at which the registration was created.</param>
    /// <returns>TR: SMS doğrulaması bekleyen yeni müşteri nesnesini döndürür. EN: Returns a new customer awaiting SMS verification.</returns>
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
    /// TR: Kalıcı MSSQL kaydından Customer aggregate'ini mevcut lifecycle state'iyle yeniden oluşturur; yeni kayıt business akışlarında kullanılmamalıdır.
    /// EN: Rehydrates a Customer aggregate from durable MSSQL state using its existing lifecycle state; it must not be used for new-registration business flows.
    /// </summary>
    /// <param name="id">TR: Kalıcı müşteri kimliği. EN: Persisted customer identifier.</param>
    /// <param name="countryCode">TR: Kalıcı normalize ülke kodu. EN: Persisted normalized country code.</param>
    /// <param name="phoneNumber">TR: Kalıcı normalize telefon numarası. EN: Persisted normalized phone number.</param>
    /// <param name="email">TR: Kalıcı isteğe bağlı e-posta adresi. EN: Persisted optional email address.</param>
    /// <param name="status">TR: Kalıcı müşteri lifecycle durumu. EN: Persisted customer lifecycle state.</param>
    /// <param name="createdAt">TR: Kalıcı oluşturulma UTC zamanı. EN: Persisted UTC creation time.</param>
    /// <returns>TR: Kalıcı state'i taşıyan Customer aggregate'ini döndürür. EN: Returns the Customer aggregate carrying persisted state.</returns>
    public static Customer Restore(
        Guid id,
        string countryCode,
        PhoneNumber phoneNumber,
        string? email,
        CustomerStatus status,
        DateTimeOffset createdAt)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Customer identifier cannot be empty.", nameof(id));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(countryCode);
        ArgumentNullException.ThrowIfNull(phoneNumber);

        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }

        return new Customer
        {
            Id = id,
            CountryCode = countryCode.Trim().ToUpperInvariant(),
            PhoneNumber = phoneNumber.Value,
            Email = email,
            Status = status,
            CreatedAt = createdAt
        };
    }

    /// <summary>TR: Müşterinin sistem içindeki benzersiz kimliğini döndürür. EN: Gets the customer's unique system identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>TR: Kayıt ülkesini iki harfli kod olarak döndürür. EN: Gets the registration country as a two-letter code.</summary>
    public string CountryCode { get; private set; }

    /// <summary>TR: Normalize telefon numarasını döndürür. EN: Gets the normalized phone number.</summary>
    public string PhoneNumber { get; private set; }

    /// <summary>TR: İsteğe bağlı müşteri e-posta adresini döndürür. EN: Gets the optional customer email address.</summary>
    public string? Email { get; private set; }

    /// <summary>TR: Müşterinin mevcut lifecycle durumunu döndürür. EN: Gets the customer's current lifecycle state.</summary>
    public CustomerStatus Status { get; private set; }

    /// <summary>TR: Müşteri kaydının oluşturulduğu UTC zamanı döndürür. EN: Gets the UTC timestamp at which the customer record was created.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// TR: Başarılı SMS doğrulamasından sonra bekleyen müşteriyi aktif hale getirir; yalnızca bekleyen kayıtlar aktive edilebilir.
    /// EN: Activates a pending customer after successful SMS verification; only pending registrations may be activated.
    /// </summary>
    /// <exception cref="InvalidOperationException">TR: Müşteri pending durumda değilse oluşur. EN: Thrown when the customer is not pending.</exception>
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
    /// <exception cref="InvalidOperationException">TR: Müşteri aktif değilse oluşur. EN: Thrown when the customer is not active.</exception>
    public void Block()
    {
        if (Status != CustomerStatus.Active)
        {
            throw new InvalidOperationException($"Customer in '{Status}' state cannot be blocked.");
        }

        Status = CustomerStatus.Blocked;
    }
}
