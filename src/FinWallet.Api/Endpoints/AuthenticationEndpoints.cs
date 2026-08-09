using FinWallet.Api.Contracts.Authentication;
using FinWallet.Application.Authentication;
using FinWallet.Application.Registration;

namespace FinWallet.Api.Endpoints;

/// <summary>
/// TR: Authentication ve registration HTTP endpoint'lerini Application use-case handler'larına bağlayan Minimal API route tanımlarını içerir.
/// EN: Contains Minimal API route definitions that connect authentication and registration HTTP endpoints to Application use-case handlers.
/// </summary>
public static class AuthenticationEndpoints
{
    /// <summary>
    /// TR: Registration, OTP verification, login ve refresh endpoint'lerini `/api/v1/auth` route grubu altında kaydeder.
    /// EN: Registers registration, OTP verification, login and refresh endpoints under the `/api/v1/auth` route group.
    /// </summary>
    /// <param name="endpoints">
    /// TR: Endpoint route'larının ekleneceği ASP.NET Core route builder.
    /// EN: ASP.NET Core route builder to which endpoint routes are added.
    /// </param>
    /// <returns>
    /// TR: Ek endpoint tanımları için aynı route builder örneğini döndürür.
    /// EN: Returns the same route builder instance for additional endpoint registration.
    /// </returns>
    public static IEndpointRouteBuilder MapAuthenticationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup("/api/v1/auth");
        group.MapPost("/register", RegisterAsync);
        group.MapPost("/registration/verify", VerifyRegistrationOtpAsync);
        group.MapPost("/login", LoginAsync);
        group.MapPost("/refresh", RefreshAsync);

        return endpoints;
    }

    /// <summary>
    /// TR: HTTP registration request'ini Application command'a çevirir ve pending müşteri/OTP expiration sonucunu 202 Accepted olarak döndürür.
    /// EN: Converts the HTTP registration request into an Application command and returns the pending-customer/OTP-expiration result as HTTP 202 Accepted.
    /// </summary>
    /// <param name="request">TR: API registration request gövdesi. EN: API registration request body.</param>
    /// <param name="handler">TR: Registration use-case handler'ı. EN: Registration use-case handler.</param>
    /// <param name="httpContext">TR: Request correlation kimliğini sağlayan HTTP context. EN: HTTP context providing the request correlation identifier.</param>
    /// <param name="cancellationToken">TR: İstek bağlantısı kesildiğinde use-case'e taşınan iptal sinyali. EN: Cancellation signal propagated to the use case when the request is aborted.</param>
    /// <returns>TR: Pending müşteri bilgisini içeren 202 Accepted sonucu döndürür. EN: Returns HTTP 202 Accepted containing pending-customer information.</returns>
    private static async Task<IResult> RegisterAsync(
        RegisterCustomerRequest request,
        RegisterCustomerHandler handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var command = new RegisterCustomerCommand(
            request.CountryCode,
            request.PhoneNumber,
            request.Email,
            request.Password,
            httpContext.TraceIdentifier);

        var result = await handler.HandleAsync(command, cancellationToken);
        return Results.Accepted(
            value: new RegisterCustomerResponse(result.CustomerId, result.OtpExpiresAt));
    }

    /// <summary>
    /// TR: Pending müşteri OTP doğrulama request'ini Application handler'a iletir ve başarılı aktivasyonda 204 No Content döndürür.
    /// EN: Forwards a pending-customer OTP verification request to the Application handler and returns HTTP 204 No Content after successful activation.
    /// </summary>
    /// <param name="request">TR: Müşteri kimliği ve ham OTP kodunu taşıyan request. EN: Request carrying the customer identifier and raw OTP code.</param>
    /// <param name="handler">TR: OTP doğrulama use-case handler'ı. EN: OTP-verification use-case handler.</param>
    /// <param name="cancellationToken">TR: İstek iptal sinyali. EN: Request cancellation signal.</param>
    /// <returns>TR: Başarılı doğrulamada 204 No Content sonucu döndürür. EN: Returns HTTP 204 No Content after successful verification.</returns>
    private static async Task<IResult> VerifyRegistrationOtpAsync(
        VerifyRegistrationOtpRequest request,
        VerifyRegistrationOtpHandler handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        await handler.HandleAsync(
            new VerifyRegistrationOtpCommand(request.CustomerId, request.Code),
            cancellationToken);

        return Results.NoContent();
    }

    /// <summary>
    /// TR: Login request'ini Application authentication handler'ına iletir ve başarılı sonuçta access/refresh token response döndürür.
    /// EN: Forwards the login request to the Application authentication handler and returns an access/refresh-token response on success.
    /// </summary>
    /// <param name="request">TR: Telefon, parola ve cihaz kimliğini taşıyan login request. EN: Login request carrying phone, password and device identifier.</param>
    /// <param name="handler">TR: Login use-case handler'ı. EN: Login use-case handler.</param>
    /// <param name="cancellationToken">TR: İstek iptal sinyali. EN: Request cancellation signal.</param>
    /// <returns>TR: Başarılı authentication token çiftini içeren 200 OK sonucu döndürür. EN: Returns HTTP 200 OK containing the successful authentication token pair.</returns>
    private static async Task<IResult> LoginAsync(
        LoginCustomerRequest request,
        LoginCustomerHandler handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await handler.HandleAsync(
            new LoginCustomerCommand(request.PhoneNumber, request.Password, request.DeviceId),
            cancellationToken);

        return Results.Ok(ToResponse(result));
    }

    /// <summary>
    /// TR: Opaque refresh token request'ini rotation/reuse-detection handler'ına iletir ve yeni token çiftini döndürür.
    /// EN: Forwards the opaque refresh-token request to the rotation/reuse-detection handler and returns a new token pair.
    /// </summary>
    /// <param name="request">TR: Ham opaque refresh token'ı taşıyan request. EN: Request carrying the raw opaque refresh token.</param>
    /// <param name="handler">TR: Refresh-session use-case handler'ı. EN: Refresh-session use-case handler.</param>
    /// <param name="cancellationToken">TR: İstek iptal sinyali. EN: Request cancellation signal.</param>
    /// <returns>TR: Yeni access/refresh token çiftini içeren 200 OK sonucu döndürür. EN: Returns HTTP 200 OK containing the new access/refresh-token pair.</returns>
    private static async Task<IResult> RefreshAsync(
        RefreshSessionRequest request,
        RefreshSessionHandler handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await handler.HandleAsync(
            new RefreshSessionCommand(request.RefreshToken),
            cancellationToken);

        return Results.Ok(ToResponse(result));
    }

    /// <summary>
    /// TR: Application authentication sonucunu dış API response sözleşmesine dönüştürür.
    /// EN: Converts an Application authentication result into the external API response contract.
    /// </summary>
    /// <param name="result">TR: Application katmanından gelen hassas token sonucu. EN: Sensitive token result produced by the Application layer.</param>
    /// <returns>TR: API authentication response nesnesini döndürür. EN: Returns the API authentication response object.</returns>
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
