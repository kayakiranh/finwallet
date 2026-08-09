using FinWallet.Domain.Authentication;
using FinWallet.Domain.Customers;
using FinWallet.Domain.Registration;

namespace FinWallet.Application.Authentication;

/// <summary>
/// TR: Telefon/parola login doğrulamasını, credential lockout state'ini ve yeni session + refresh token oluşturulmasını orkestre eder.
/// EN: Orchestrates phone/password login verification, credential lockout state and creation of a new session plus refresh token.
/// </summary>
public sealed class LoginCustomerHandler
{
    /// <summary>
    /// TR: Müşteri session'ının mutlak maksimum yaşam süresini tanımlar; runtime configuration ile uzatılamaz.
    /// EN: Defines the absolute maximum customer-session lifetime; it cannot be extended through runtime configuration.
    /// </summary>
    private static readonly TimeSpan SessionLifetime = TimeSpan.FromDays(30);

    /// <summary>
    /// TR: Tek bir refresh token rotation halkasının maksimum yaşam süresini tanımlar.
    /// EN: Defines the maximum lifetime of a single refresh token in the rotation chain.
    /// </summary>
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(14);

    private readonly IAuthenticationStore _authenticationStore;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IRefreshTokenGenerator _refreshTokenGenerator;
    private readonly IAccessTokenIssuer _accessTokenIssuer;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// TR: Login use-case bağımlılıklarıyla handler'ı oluşturur.
    /// EN: Creates the login handler with its use-case dependencies.
    /// </summary>
    /// <param name="authenticationStore">TR: Credential/session/refresh state'ini kalıcı ve concurrency-safe yöneten store. EN: Store managing durable and concurrency-safe credential/session/refresh state.</param>
    /// <param name="passwordHasher">TR: Sabit güvenlik politikasıyla parola doğrulayan servis. EN: Service verifying passwords with the fixed security policy.</param>
    /// <param name="refreshTokenGenerator">TR: Opaque refresh token ve sunucu tarafı hash'i üreten servis. EN: Service generating opaque refresh tokens and server-side hashes.</param>
    /// <param name="accessTokenIssuer">TR: Kısa ömürlü imzalı JWT access token üreten servis. EN: Service issuing short-lived signed JWT access tokens.</param>
    /// <param name="timeProvider">TR: Test edilebilir UTC zaman kaynağı. EN: Testable UTC time source.</param>
    public LoginCustomerHandler(
        IAuthenticationStore authenticationStore,
        IPasswordHasher passwordHasher,
        IRefreshTokenGenerator refreshTokenGenerator,
        IAccessTokenIssuer accessTokenIssuer,
        TimeProvider timeProvider)
    {
        _authenticationStore = authenticationStore ?? throw new ArgumentNullException(nameof(authenticationStore));
        _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
        _refreshTokenGenerator = refreshTokenGenerator ?? throw new ArgumentNullException(nameof(refreshTokenGenerator));
        _accessTokenIssuer = accessTokenIssuer ?? throw new ArgumentNullException(nameof(accessTokenIssuer));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <summary>
    /// TR: Login talebini doğrular, hatalı denemeleri MSSQL tarafında atomik lockout güncellemesine iletir ve başarılı durumda concurrency-safe session oluşturur.
    /// EN: Verifies the login request, delegates failed attempts to an atomic MSSQL lockout update and creates a concurrency-safe session on success.
    /// </summary>
    /// <param name="command">TR: Telefon, parola ve cihaz kimliğini taşıyan login komutu. EN: Login command containing phone, password and device identifier.</param>
    /// <param name="cancellationToken">TR: Kalıcılık işlemlerine iletilecek request iptal sinyali. EN: Request cancellation signal propagated to persistence operations.</param>
    /// <returns>TR: Yeni session'a bağlı access/refresh token çiftini döndürür. EN: Returns the access/refresh token pair associated with the new session.</returns>
    /// <exception cref="InvalidCredentialsException">TR: Telefon/parola eşleşmezse veya customer login kabul etmeyen durumda ise oluşur. EN: Thrown when phone/password verification fails or the customer is in a state that does not allow login.</exception>
    /// <exception cref="AuthenticationTemporarilyLockedException">TR: Credential sabit başarısız-login eşiği nedeniyle geçici kilit altındaysa veya paralel hatalı denemeler başarı doğrulaması sırasında lock oluşturduysa oluşur. EN: Thrown when the credential is temporarily locked or concurrent failed attempts create a lock while a successful password verification is being finalized.</exception>
    public async Task<AuthenticationTokensResult> HandleAsync(
        LoginCustomerCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.DeviceId);

        var phoneNumber = PhoneNumber.Create(command.PhoneNumber);
        var now = _timeProvider.GetUtcNow();
        var loginData = await _authenticationStore.FindLoginDataAsync(phoneNumber.Value, cancellationToken);

        if (loginData is null)
        {
            PerformDummyPasswordWork(command.Password);
            throw new InvalidCredentialsException();
        }

        if (loginData.Credential.IsLocked(now))
        {
            throw new AuthenticationTemporarilyLockedException();
        }

        var passwordMatches = _passwordHasher.Verify(
            command.Password,
            loginData.Credential.PasswordHash,
            loginData.Credential.PasswordSalt,
            loginData.Credential.PasswordHashVersion);

        if (!passwordMatches)
        {
            await _authenticationStore.RegisterFailedLoginAsync(
                loginData.Customer.Id,
                now,
                cancellationToken);
            throw new InvalidCredentialsException();
        }

        if (loginData.Customer.Status != CustomerStatus.Active)
        {
            throw new InvalidCredentialsException();
        }

        var sessionId = Guid.NewGuid();
        var sessionExpiresAt = now.Add(SessionLifetime);
        var refreshExpiresAt = Min(now.Add(RefreshTokenLifetime), sessionExpiresAt);
        var session = CustomerSession.Create(
            sessionId,
            loginData.Customer.Id,
            command.DeviceId.Trim(),
            now,
            sessionExpiresAt);

        var refreshMaterial = _refreshTokenGenerator.Generate();
        var refreshToken = RefreshToken.Create(
            Guid.NewGuid(),
            sessionId,
            refreshMaterial.TokenHash,
            now,
            refreshExpiresAt);

        var sessionCreated = await _authenticationStore.TryCreateSessionAsync(
            loginData.Credential,
            session,
            refreshToken,
            cancellationToken);

        if (!sessionCreated)
        {
            throw new AuthenticationTemporarilyLockedException();
        }

        var accessToken = _accessTokenIssuer.Issue(loginData.Customer.Id, sessionId, now);
        return new AuthenticationTokensResult(
            loginData.Customer.Id,
            sessionId,
            accessToken.Token,
            accessToken.ExpiresAt,
            refreshMaterial.RawToken,
            refreshExpiresAt);
    }

    /// <summary>
    /// TR: Bilinmeyen telefon numarası için de PBKDF2 maliyeti oluşturarak kullanıcı varlığına dayalı kaba timing farkını azaltır.
    /// EN: Performs PBKDF2 work for unknown phone numbers as well to reduce coarse timing differences based on user existence.
    /// </summary>
    /// <param name="password">TR: Login talebinde sağlanan ham parola. EN: Raw password supplied by the login request.</param>
    private void PerformDummyPasswordWork(string password)
    {
        try
        {
            _passwordHasher.Hash(password);
        }
        catch (ArgumentException)
        {
            // Invalid-format passwords are rejected generically without exposing account existence.
        }
    }

    /// <summary>
    /// TR: İki UTC zamanından daha erken olanı seçerek refresh token'ın session mutlak sona erme zamanını aşmasını engeller.
    /// EN: Selects the earlier of two UTC timestamps so a refresh token cannot outlive the session's absolute expiration.
    /// </summary>
    /// <param name="first">TR: Karşılaştırılacak ilk UTC zaman bilgisi. EN: First UTC timestamp to compare.</param>
    /// <param name="second">TR: Karşılaştırılacak ikinci UTC zaman bilgisi. EN: Second UTC timestamp to compare.</param>
    /// <returns>TR: Daha erken UTC zamanını döndürür. EN: Returns the earlier UTC timestamp.</returns>
    private static DateTimeOffset Min(DateTimeOffset first, DateTimeOffset second)
    {
        return first <= second ? first : second;
    }
}
