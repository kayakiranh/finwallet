using FinWallet.Api.Contracts.Authentication;
using FinWallet.Application.Authentication;
using FinWallet.Application.Registration;
using FinWallet.Shared.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace FinWallet.Api.Controllers;

/// <summary>
/// TR: Müşteri registration, OTP doğrulama, login ve refresh-token rotation use-case'lerini controller tabanlı Web API sözleşmesine bağlar.
/// EN: Connects customer registration, OTP verification, login and refresh-token rotation use cases to the controller-based Web API contract.
/// </summary>
[ApiController]
[Route("api/v1/auth")]
public sealed class AuthenticationController : ControllerBase
{
    private readonly RegisterCustomerHandler _registerCustomerHandler;
    private readonly VerifyRegistrationOtpHandler _verifyRegistrationOtpHandler;
    private readonly LoginCustomerHandler _loginCustomerHandler;
    private readonly RefreshSessionHandler _refreshSessionHandler;

    /// <summary>
    /// TR: Authentication use-case handler bağımlılıklarıyla controller'ı oluşturur.
    /// EN: Creates the controller with its authentication use-case handler dependencies.
    /// </summary>
    /// <param name="registerCustomerHandler">TR: Pending müşteri registration use-case handler'ı. EN: Pending-customer registration use-case handler.</param>
    /// <param name="verifyRegistrationOtpHandler">TR: Registration OTP doğrulama handler'ı. EN: Registration OTP-verification handler.</param>
    /// <param name="loginCustomerHandler">TR: Telefon/parola login handler'ı. EN: Phone/password login handler.</param>
    /// <param name="refreshSessionHandler">TR: Refresh-token rotation/reuse detection handler'ı. EN: Refresh-token rotation/reuse-detection handler.</param>
    public AuthenticationController(
        RegisterCustomerHandler registerCustomerHandler,
        VerifyRegistrationOtpHandler verifyRegistrationOtpHandler,
        LoginCustomerHandler loginCustomerHandler,
        RefreshSessionHandler refreshSessionHandler)
    {
        _registerCustomerHandler = registerCustomerHandler ?? throw new ArgumentNullException(nameof(registerCustomerHandler));
        _verifyRegistrationOtpHandler = verifyRegistrationOtpHandler ?? throw new ArgumentNullException(nameof(verifyRegistrationOtpHandler));
        _loginCustomerHandler = loginCustomerHandler ?? throw new ArgumentNullException(nameof(loginCustomerHandler));
        _refreshSessionHandler = refreshSessionHandler ?? throw new ArgumentNullException(nameof(refreshSessionHandler));
    }

    /// <summary>
    /// TR: Registration request'ini işler, pending müşteri oluşturur ve OTP gönderim sürecini başlatır.
    /// EN: Processes a registration request, creates a pending customer and starts OTP delivery.
    /// </summary>
    /// <param name="request">TR: Ülke, telefon, e-posta ve parola registration request'i. EN: Registration request containing country, phone, email and password.</param>
    /// <param name="cancellationToken">TR: Registration use-case iptal sinyali. EN: Cancellation signal for the registration use case.</param>
    /// <returns>TR: Pending müşteri ve OTP expiration bilgisini ServiceResult içinde 202 olarak döndürür. EN: Returns pending-customer and OTP-expiration information inside ServiceResult with HTTP 202.</returns>
    [HttpPost("register")]
    [ProducesResponseType(typeof(ServiceResult<RegisterCustomerResponse>), StatusCodes.Status202Accepted)]
    public async Task<ActionResult<ServiceResult<RegisterCustomerResponse>>> RegisterAsync(
        [FromBody] RegisterCustomerRequest request,
        CancellationToken cancellationToken)
    {
        var command = new RegisterCustomerCommand(
            request.CountryCode,
            request.PhoneNumber,
            request.Email,
            request.Password,
            HttpContext.TraceIdentifier);

        var result = await _registerCustomerHandler.HandleAsync(command, cancellationToken);
        var response = new RegisterCustomerResponse(result.CustomerId, result.OtpExpiresAt);

        return StatusCode(
            StatusCodes.Status202Accepted,
            ServiceResult<RegisterCustomerResponse>.Success(
                response,
                "REGISTRATION_ACCEPTED",
                "Registration accepted and verification is pending."));
    }

    /// <summary>
    /// TR: Pending müşterinin SMS OTP kodunu doğrular ve uygun durumda müşteriyi aktive eder.
    /// EN: Verifies the pending customer's SMS OTP and activates the customer when eligible.
    /// </summary>
    /// <param name="request">TR: Müşteri kimliği ve ham OTP kodunu taşıyan doğrulama request'i. EN: Verification request carrying customer identifier and raw OTP code.</param>
    /// <param name="cancellationToken">TR: OTP doğrulama use-case iptal sinyali. EN: Cancellation signal for the OTP-verification use case.</param>
    /// <returns>TR: Başarılı aktivasyonu body içeren ServiceResult olarak döndürür. EN: Returns successful activation as a body-bearing ServiceResult.</returns>
    [HttpPost("registration/verify")]
    [ProducesResponseType(typeof(ServiceResult<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ServiceResult<object>>> VerifyRegistrationOtpAsync(
        [FromBody] VerifyRegistrationOtpRequest request,
        CancellationToken cancellationToken)
    {
        await _verifyRegistrationOtpHandler.HandleAsync(
            new VerifyRegistrationOtpCommand(request.CustomerId, request.Code),
            cancellationToken);

        return Ok(ServiceResult<object>.Success(
            null,
            "REGISTRATION_VERIFIED",
            "Registration verification completed."));
    }

    /// <summary>
    /// TR: Müşteri telefon/parola bilgilerini doğrular ve yeni session için access/refresh token çifti üretir.
    /// EN: Verifies customer phone/password credentials and issues an access/refresh-token pair for a new session.
    /// </summary>
    /// <param name="request">TR: Telefon, parola ve cihaz kimliğini taşıyan login request'i. EN: Login request carrying phone, password and device identifier.</param>
    /// <param name="cancellationToken">TR: Login use-case iptal sinyali. EN: Cancellation signal for the login use case.</param>
    /// <returns>TR: Authentication token çiftini ServiceResult içinde döndürür. EN: Returns the authentication token pair inside ServiceResult.</returns>
    [HttpPost("login")]
    [ProducesResponseType(typeof(ServiceResult<AuthenticationTokensResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ServiceResult<AuthenticationTokensResponse>>> LoginAsync(
        [FromBody] LoginCustomerRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _loginCustomerHandler.HandleAsync(
            new LoginCustomerCommand(request.PhoneNumber, request.Password, request.DeviceId),
            cancellationToken);

        var response = ToResponse(result);
        return Ok(ServiceResult<AuthenticationTokensResponse>.Success(
            response,
            "AUTHENTICATED",
            "Authentication completed successfully."));
    }

    /// <summary>
    /// TR: Tek kullanımlık refresh token'ı rotate eder ve yeni access/refresh token çiftini üretir.
    /// EN: Rotates the single-use refresh token and issues a new access/refresh-token pair.
    /// </summary>
    /// <param name="request">TR: Ham opaque refresh token taşıyan request. EN: Request carrying the raw opaque refresh token.</param>
    /// <param name="cancellationToken">TR: Refresh use-case iptal sinyali. EN: Cancellation signal for the refresh use case.</param>
    /// <returns>TR: Yeni token çiftini ServiceResult içinde döndürür. EN: Returns the new token pair inside ServiceResult.</returns>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(ServiceResult<AuthenticationTokensResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ServiceResult<AuthenticationTokensResponse>>> RefreshAsync(
        [FromBody] RefreshSessionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _refreshSessionHandler.HandleAsync(
            new RefreshSessionCommand(request.RefreshToken),
            cancellationToken);

        var response = ToResponse(result);
        return Ok(ServiceResult<AuthenticationTokensResponse>.Success(
            response,
            "TOKEN_REFRESHED",
            "Authentication tokens refreshed successfully."));
    }

    /// <summary>
    /// TR: Application authentication sonucunu dış Web API response modeline dönüştürür.
    /// EN: Converts an Application authentication result into the external Web API response model.
    /// </summary>
    /// <param name="result">TR: Application katmanından gelen hassas authentication token sonucu. EN: Sensitive authentication-token result produced by the Application layer.</param>
    /// <returns>TR: Dış API authentication response modelini döndürür. EN: Returns the external API authentication response model.</returns>
    private static AuthenticationTokensResponse ToResponse(AuthenticationTokensResult result)
    {
        return new AuthenticationTokensResponse(
            result.CustomerId,
            result.SessionId,
            result.AccessToken,
            result.AccessTokenExpiresAt,
            result.RefreshToken,
            result.RefreshTokenExpiresAt);
    }
}
