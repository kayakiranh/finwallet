using System.Net.Mail;
using FinWallet.Application.Authentication;
using FinWallet.Application.Communication;
using FinWallet.Domain.Authentication;
using FinWallet.Domain.Customers;
using FinWallet.Domain.Registration;

namespace FinWallet.Application.Registration;

/// <summary>
/// TR: Yeni müşteri registration akışını validation, durable pending kayıt, OTP üretimi ve FakeCommunication SMS çağrısı sırasıyla orkestre eder; dış HTTP çağrısını DB transaction dışında tutar ve SMS provider kesintisinin durable registration kaydını geri almasına izin vermez.
/// EN: Orchestrates new customer registration through validation, durable pending persistence, OTP issuance and FakeCommunication SMS delivery while keeping the external HTTP call outside the DB transaction and preventing SMS-provider failure from rolling back the durable registration.
/// </summary>
public sealed class RegisterCustomerHandler
{
    private readonly RegistrationCountryPolicy _countryPolicy;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ICustomerRegistrationStore _registrationStore;
    private readonly IRegistrationOtpService _otpService;
    private readonly ICommunicationGateway _communicationGateway;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// TR: Registration use-case bağımlılıklarıyla handler'ı oluşturur.
    /// EN: Creates the registration handler with its use-case dependencies.
    /// </summary>
    /// <param name="countryPolicy">TR: Desteklenen ülke ve telefon eşleşmesini doğrulayan domain policy. EN: Domain policy validating supported countries and country/phone compatibility.</param>
    /// <param name="passwordHasher">TR: Sabit güvenlik politikasıyla parola hash'i üreten servis. EN: Service producing password hashes with the fixed security policy.</param>
    /// <param name="registrationStore">TR: Customer ve credential kayıtlarını atomik olarak kalıcı hale getiren store. EN: Store that atomically persists Customer and credential records.</param>
    /// <param name="otpService">TR: Registration OTP oluşturma ve saklama sınırı. EN: Registration OTP issuance and storage boundary.</param>
    /// <param name="communicationGateway">TR: FakeCommunication SMS provider'ına erişim sınırı. EN: Gateway boundary to the FakeCommunication SMS provider.</param>
    /// <param name="timeProvider">TR: Test edilebilir UTC zaman kaynağı. EN: Testable UTC time source.</param>
    public RegisterCustomerHandler(
        RegistrationCountryPolicy countryPolicy,
        IPasswordHasher passwordHasher,
        ICustomerRegistrationStore registrationStore,
        IRegistrationOtpService otpService,
        ICommunicationGateway communicationGateway,
        TimeProvider timeProvider)
    {
        _countryPolicy = countryPolicy ?? throw new ArgumentNullException(nameof(countryPolicy));
        _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
        _registrationStore = registrationStore ?? throw new ArgumentNullException(nameof(registrationStore));
        _otpService = otpService ?? throw new ArgumentNullException(nameof(otpService));
        _communicationGateway = communicationGateway ?? throw new ArgumentNullException(nameof(communicationGateway));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <summary>
    /// TR: Registration request'ini fail-fast doğrular, pending Customer + Credential kayıtlarını atomik kaydeder, OTP challenge oluşturur ve DB transaction kapandıktan sonra fake SMS provider'ına OTP göndermeyi dener; provider hatasında pending kayıt ve customerId korunur.
    /// EN: Fail-fast validates the registration request, atomically persists pending Customer + Credential records, creates the OTP challenge and attempts delivery through the fake SMS provider after the DB transaction ends; on provider failure the pending registration and customerId are preserved.
    /// </summary>
    /// <param name="command">TR: Ülke, telefon, e-posta, parola ve correlation bilgilerini taşıyan kayıt komutu. EN: Registration command containing country, phone, email, password and correlation information.</param>
    /// <param name="cancellationToken">TR: Persistence, OTP ve provider işlemlerine iletilecek request iptal sinyali. EN: Request cancellation signal propagated to persistence, OTP and provider operations.</param>
    /// <returns>TR: Pending müşteri kimliği, OTP sona erme bilgisi ve ilk SMS delivery sonucunu döndürür. EN: Returns the pending-customer identifier, OTP expiration and initial SMS-delivery outcome.</returns>
    /// <exception cref="RegistrationConflictException">TR: Aynı normalize telefon numarasıyla daha önce müşteri kaydı varsa oluşur. EN: Thrown when a customer registration already exists for the normalized phone number.</exception>
    public async Task<RegisterCustomerResult> HandleAsync(
        RegisterCustomerCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.CorrelationId);

        var phoneNumber = PhoneNumber.Create(command.PhoneNumber);
        var normalizedCountryCode = _countryPolicy.Validate(command.CountryCode, phoneNumber);
        PasswordPolicy.Validate(command.Password);
        var normalizedEmail = NormalizeEmail(command.Email);

        if (await _registrationStore.ExistsByPhoneNumberAsync(phoneNumber.Value, cancellationToken))
        {
            throw new RegistrationConflictException("A registration already exists for the supplied phone number.");
        }

        var passwordHash = _passwordHasher.Hash(command.Password);
        var now = _timeProvider.GetUtcNow();
        var customerId = Guid.NewGuid();

        var customer = Customer.Create(
            customerId,
            normalizedCountryCode,
            phoneNumber,
            normalizedEmail,
            now);

        var credential = CustomerCredential.Create(
            customerId,
            passwordHash.Hash,
            passwordHash.Salt,
            passwordHash.Version,
            now);

        await _registrationStore.CreatePendingCustomerAsync(customer, credential, cancellationToken);

        var otp = await _otpService.IssueAsync(customerId, cancellationToken);
        var deliverySucceeded = await TrySendOtpAsync(
            phoneNumber.Value,
            otp.Code,
            command.CorrelationId,
            cancellationToken);

        return new RegisterCustomerResult(customerId, otp.ExpiresAt, deliverySucceeded);
    }

    /// <summary>
    /// TR: Registration OTP'sini FakeCommunication provider'a göndermeyi dener; provider/network hatasını durable registration'ı bozmayacak şekilde false sonucuna dönüştürür, request cancellation'ını ise aynen taşır.
    /// EN: Attempts to send the registration OTP through FakeCommunication, converts provider/network failure into a false delivery result without damaging durable registration, and still propagates request cancellation.
    /// </summary>
    /// <param name="phoneNumber">TR: Normalize hedef telefon numarası. EN: Normalized destination phone number.</param>
    /// <param name="otpCode">TR: Provider'a gönderilecek ham OTP; loglanmamalıdır. EN: Raw OTP sent to the provider; it must not be logged.</param>
    /// <param name="correlationId">TR: Provider çağrısına taşınacak correlation kimliği. EN: Correlation identifier propagated to the provider call.</param>
    /// <param name="cancellationToken">TR: Dış HTTP çağrısı iptal sinyali. EN: Cancellation signal for the external HTTP call.</param>
    /// <returns>TR: Provider çağrısı başarılıysa true, HttpRequestException ile başarısızsa false döndürür. EN: Returns true when the provider call succeeds and false when it fails with HttpRequestException.</returns>
    private async Task<bool> TrySendOtpAsync(
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

    /// <summary>
    /// TR: İsteğe bağlı e-posta girdisini tek adres olacak şekilde doğrular ve karşılaştırma/bildirim kullanımında tutarlı olması için normalize eder.
    /// EN: Validates the optional email input as a single address and normalizes it for consistent comparison/notification use.
    /// </summary>
    /// <param name="email">TR: Kullanıcının girdiği isteğe bağlı e-posta adresi. EN: Optional email address entered by the user.</param>
    /// <returns>TR: E-posta yoksa null, varsa normalize küçük harfli adresi döndürür. EN: Returns null when no email is supplied; otherwise returns the normalized lower-case address.</returns>
    /// <exception cref="ArgumentException">TR: E-posta formatı geçersizse veya display-name içeren tek adres dışı biçim kullanılmışsa oluşur. EN: Thrown when the email format is invalid or contains a display-name form instead of a single address.</exception>
    private static string? NormalizeEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        var trimmed = email.Trim();
        MailAddress parsedAddress;

        try
        {
            parsedAddress = new MailAddress(trimmed);
        }
        catch (FormatException exception)
        {
            throw new ArgumentException("Email address format is invalid.", nameof(email), exception);
        }

        if (!string.Equals(parsedAddress.Address, trimmed, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Email must contain a single address without a display name.", nameof(email));
        }

        return parsedAddress.Address.ToLowerInvariant();
    }
}
