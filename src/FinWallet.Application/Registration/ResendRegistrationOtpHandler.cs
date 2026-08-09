using FinWallet.Application.Communication;
using FinWallet.Domain.Customers;

namespace FinWallet.Application.Registration;

/// <summary>
/// TR: Pending customer için Redis'te yeni OTP challenge üretip resend cooldown'u uygular ve OTP'yi FakeCommunication üzerinden yeniden göndermeyi orkestre eder.
/// EN: Orchestrates creation of a new Redis OTP challenge for a pending customer, enforces resend cooldown and retries delivery through FakeCommunication.
/// </summary>
public sealed class ResendRegistrationOtpHandler
{
    private readonly ICustomerRegistrationStore _registrationStore;
    private readonly IRegistrationOtpService _otpService;
    private readonly ICommunicationGateway _communicationGateway;

    /// <summary>
    /// TR: OTP resend use-case bağımlılıklarıyla handler'ı oluşturur.
    /// EN: Creates the OTP-resend handler with its use-case dependencies.
    /// </summary>
    /// <param name="registrationStore">TR: Pending customer state'ini yükleyen persistence sınırı. EN: Persistence boundary loading pending-customer state.</param>
    /// <param name="otpService">TR: Yeni Redis OTP challenge oluşturan servis. EN: Service issuing a new Redis OTP challenge.</param>
    /// <param name="communicationGateway">TR: FakeCommunication SMS provider gateway'i. EN: FakeCommunication SMS-provider gateway.</param>
    public ResendRegistrationOtpHandler(
        ICustomerRegistrationStore registrationStore,
        IRegistrationOtpService otpService,
        ICommunicationGateway communicationGateway)
    {
        _registrationStore = registrationStore ?? throw new ArgumentNullException(nameof(registrationStore));
        _otpService = otpService ?? throw new ArgumentNullException(nameof(otpService));
        _communicationGateway = communicationGateway ?? throw new ArgumentNullException(nameof(communicationGateway));
    }

    /// <summary>
    /// TR: Customer'ın halen SMS doğrulaması beklediğini doğrular, yeni OTP challenge üretir ve provider gönderimini dener; provider hatasını durable registration'ı bozmadan delivery=false sonucuna dönüştürür.
    /// EN: Verifies that the customer is still awaiting SMS verification, issues a new OTP challenge and attempts provider delivery; provider failure becomes delivery=false without damaging durable registration.
    /// </summary>
    /// <param name="command">TR: Customer ve correlation kimliklerini taşıyan resend komutu. EN: Resend command carrying customer and correlation identifiers.</param>
    /// <param name="cancellationToken">TR: Persistence, Redis ve provider operasyonlarının iptal sinyali. EN: Cancellation signal for persistence, Redis and provider operations.</param>
    /// <returns>TR: Yeni challenge expiration ve provider delivery sonucunu döndürür. EN: Returns the new challenge expiration and provider-delivery outcome.</returns>
    /// <exception cref="InvalidRegistrationOtpException">TR: Customer bulunamazsa veya pending verification state'inde değilse oluşur. EN: Thrown when the customer is not found or is not in pending-verification state.</exception>
    public async Task<ResendRegistrationOtpResult> HandleAsync(
        ResendRegistrationOtpCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.CorrelationId);

        if (command.CustomerId == Guid.Empty)
        {
            throw new InvalidRegistrationOtpException();
        }

        var customer = await _registrationStore.FindCustomerAsync(command.CustomerId, cancellationToken);
        if (customer is null || customer.Status != CustomerStatus.PendingVerification)
        {
            throw new InvalidRegistrationOtpException();
        }

        var otp = await _otpService.IssueAsync(customer.Id, cancellationToken);
        var deliverySucceeded = await TrySendAsync(
            customer.PhoneNumber,
            otp.Code,
            command.CorrelationId,
            cancellationToken);

        return new ResendRegistrationOtpResult(otp.ExpiresAt, deliverySucceeded);
    }

    /// <summary>
    /// TR: OTP SMS'ini provider'a göndermeyi dener; HttpRequestException durumunu resend sonucunda false'a çevirir, cancellation'ı ise yutmaz.
    /// EN: Attempts to send the OTP SMS through the provider, converts HttpRequestException into false in the resend result and does not swallow cancellation.
    /// </summary>
    /// <param name="phoneNumber">TR: Normalize hedef telefon numarası. EN: Normalized destination phone number.</param>
    /// <param name="otpCode">TR: Ham OTP kodu; loglanmamalıdır. EN: Raw OTP code; it must not be logged.</param>
    /// <param name="correlationId">TR: Provider correlation kimliği. EN: Provider correlation identifier.</param>
    /// <param name="cancellationToken">TR: Dış HTTP çağrısı iptal sinyali. EN: Cancellation signal for the external HTTP call.</param>
    /// <returns>TR: Provider başarılıysa true, HttpRequestException oluşursa false döndürür. EN: Returns true on provider success and false when HttpRequestException occurs.</returns>
    private async Task<bool> TrySendAsync(
        string phoneNumber,
        string otpCode,
        string correlationId,
        CancellationToken cancellationToken)
    {
        try
        {
            await _communicationGateway.SendRegistrationOtpAsync(
                phoneNumber,
                otpCode,
                correlationId,
                cancellationToken);
            return true;
        }
        catch (HttpRequestException)
        {
            return false;
        }
    }
}
