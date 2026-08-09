using FinWallet.Domain.Customers;

namespace FinWallet.Application.Registration;

/// <summary>
/// TR: Pending müşteri kaydının OTP doğrulamasını ve Customer aggregate aktivasyonunu orkestre eder; başarılı aktivasyon tekrar çağrıldığında idempotent davranır.
/// EN: Orchestrates OTP verification and Customer aggregate activation for a pending registration and behaves idempotently when an already successful activation is repeated.
/// </summary>
public sealed class VerifyRegistrationOtpHandler
{
    private readonly ICustomerRegistrationStore _registrationStore;
    private readonly IRegistrationOtpService _otpService;

    /// <summary>
    /// TR: OTP verification use-case bağımlılıklarıyla handler'ı oluşturur.
    /// EN: Creates the OTP-verification handler with its use-case dependencies.
    /// </summary>
    /// <param name="registrationStore">
    /// TR: Pending customer kaydını yükleyen ve activation state'ini kalıcılaştıran store.
    /// EN: Store loading pending customer records and persisting activation state.
    /// </param>
    /// <param name="otpService">
    /// TR: OTP'yi doğrulayıp başarılıysa atomik olarak tüketen servis.
    /// EN: Service verifying the OTP and atomically consuming it on success.
    /// </param>
    public VerifyRegistrationOtpHandler(
        ICustomerRegistrationStore registrationStore,
        IRegistrationOtpService otpService)
    {
        _registrationStore = registrationStore ?? throw new ArgumentNullException(nameof(registrationStore));
        _otpService = otpService ?? throw new ArgumentNullException(nameof(otpService));
    }

    /// <summary>
    /// TR: OTP kodunu doğrular ve pending Customer'ı aktif hale getirir; customer zaten aktifse network retry senaryosu için başarılı kabul eder.
    /// EN: Verifies the OTP code and activates the pending Customer; if the customer is already active, it treats the call as successful for network-retry idempotency.
    /// </summary>
    /// <param name="command">
    /// TR: Müşteri kimliği ve ham OTP kodunu taşıyan doğrulama komutu.
    /// EN: Verification command carrying the customer identifier and raw OTP code.
    /// </param>
    /// <param name="cancellationToken">
    /// TR: Persistence ve OTP işlemlerine iletilecek request iptal sinyali.
    /// EN: Request cancellation signal propagated to persistence and OTP operations.
    /// </param>
    /// <exception cref="InvalidRegistrationOtpException">
    /// TR: Müşteri bulunamazsa, registration state uygun değilse veya OTP geçersiz/süresi dolmuş/tüketilmişse oluşur.
    /// EN: Thrown when the customer is not found, registration state is invalid or the OTP is invalid, expired or already consumed.
    /// </exception>
    public async Task HandleAsync(
        VerifyRegistrationOtpCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.CustomerId == Guid.Empty || string.IsNullOrWhiteSpace(command.Code))
        {
            throw new InvalidRegistrationOtpException();
        }

        var customer = await _registrationStore.FindCustomerAsync(command.CustomerId, cancellationToken);
        if (customer is null)
        {
            throw new InvalidRegistrationOtpException();
        }

        if (customer.Status == CustomerStatus.Active)
        {
            return;
        }

        if (customer.Status != CustomerStatus.PendingVerification)
        {
            throw new InvalidRegistrationOtpException();
        }

        var verified = await _otpService.VerifyAndConsumeAsync(
            customer.Id,
            command.Code,
            cancellationToken);

        if (!verified)
        {
            throw new InvalidRegistrationOtpException();
        }

        customer.Activate();
        await _registrationStore.UpdateCustomerAsync(customer, cancellationToken);
    }
}
