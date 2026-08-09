using FinWallet.Application.Authentication;
using FinWallet.Application.Registration;
using FinWallet.Domain.Registration;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace FinWallet.Api.Errors;

/// <summary>
/// TR: Beklenen registration/authentication hatalarını stabil HTTP status ve machine-readable error code içeren Problem Details cevabına dönüştürür; beklenmeyen exception ayrıntılarını istemciye sızdırmaz.
/// EN: Converts expected registration/authentication errors into Problem Details responses with stable HTTP status and machine-readable error codes while preventing unexpected exception details from leaking to clients.
/// </summary>
public sealed class ApiExceptionHandler : IExceptionHandler
{
    /// <summary>
    /// TR: Exception tipini güvenli API hata sözleşmesine map eder ve HTTP cevabını yazar.
    /// EN: Maps an exception type to the safe API error contract and writes the HTTP response.
    /// </summary>
    /// <param name="httpContext">TR: Hata cevabının yazılacağı mevcut HTTP context. EN: Current HTTP context to which the error response is written.</param>
    /// <param name="exception">TR: Request pipeline sırasında oluşan exception. EN: Exception raised during the request pipeline.</param>
    /// <param name="cancellationToken">TR: Hata cevabı yazımının iptal sinyali. EN: Cancellation signal for writing the error response.</param>
    /// <returns>TR: Hata bu handler tarafından işlendiği için her zaman true döndürür. EN: Always returns true because the error is handled by this handler.</returns>
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(exception);

        var descriptor = Describe(exception);
        httpContext.Response.StatusCode = descriptor.StatusCode;

        var problem = new ProblemDetails
        {
            Status = descriptor.StatusCode,
            Title = descriptor.Title,
            Detail = descriptor.Detail,
            Instance = httpContext.Request.Path
        };
        problem.Extensions["code"] = descriptor.Code;
        problem.Extensions["traceId"] = httpContext.TraceIdentifier;

        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }

    /// <summary>
    /// TR: Exception tipini istemciye açıklanması güvenli status, title, detail ve error code değerlerine dönüştürür.
    /// EN: Converts an exception type into status, title, detail and error-code values that are safe to expose to the client.
    /// </summary>
    /// <param name="exception">TR: Sınıflandırılacak exception. EN: Exception to classify.</param>
    /// <returns>TR: Güvenli API hata descriptor değerini döndürür. EN: Returns the safe API-error descriptor.</returns>
    private static ApiErrorDescriptor Describe(Exception exception)
    {
        return exception switch
        {
            RegistrationNotAllowedException => new ApiErrorDescriptor(
                StatusCodes.Status400BadRequest,
                "Registration not allowed",
                "REGISTRATION_NOT_ALLOWED",
                "The registration country or phone number is not eligible."),
            RegistrationConflictException => new ApiErrorDescriptor(
                StatusCodes.Status409Conflict,
                "Registration conflict",
                "REGISTRATION_CONFLICT",
                "A customer registration already exists for the supplied identity."),
            RegistrationOtpRateLimitException => new ApiErrorDescriptor(
                StatusCodes.Status429TooManyRequests,
                "OTP request limited",
                "OTP_RESEND_RATE_LIMIT",
                "A new verification code cannot be issued yet."),
            InvalidRegistrationOtpException => new ApiErrorDescriptor(
                StatusCodes.Status400BadRequest,
                "Invalid verification code",
                "INVALID_REGISTRATION_OTP",
                "The verification code is invalid, expired or already consumed."),
            AuthenticationTemporarilyLockedException => new ApiErrorDescriptor(
                StatusCodes.Status429TooManyRequests,
                "Authentication temporarily unavailable",
                "AUTH_TEMPORARILY_LOCKED",
                "Authentication is temporarily unavailable for this credential."),
            InvalidCredentialsException => new ApiErrorDescriptor(
                StatusCodes.Status401Unauthorized,
                "Authentication failed",
                "INVALID_CREDENTIALS",
                "The supplied credentials are invalid."),
            RefreshTokenReuseDetectedException => new ApiErrorDescriptor(
                StatusCodes.Status401Unauthorized,
                "Session revoked",
                "REFRESH_TOKEN_REUSE_DETECTED",
                "The session was revoked because refresh-token reuse was detected."),
            InvalidRefreshTokenException => new ApiErrorDescriptor(
                StatusCodes.Status401Unauthorized,
                "Invalid refresh token",
                "INVALID_REFRESH_TOKEN",
                "The refresh token is invalid or no longer usable."),
            ArgumentException => new ApiErrorDescriptor(
                StatusCodes.Status400BadRequest,
                "Invalid request",
                "INVALID_REQUEST",
                "One or more request values are invalid."),
            HttpRequestException => new ApiErrorDescriptor(
                StatusCodes.Status503ServiceUnavailable,
                "Dependency unavailable",
                "DEPENDENCY_UNAVAILABLE",
                "A required external service is temporarily unavailable."),
            _ => new ApiErrorDescriptor(
                StatusCodes.Status500InternalServerError,
                "Unexpected error",
                "UNEXPECTED_ERROR",
                "An unexpected server error occurred.")
        };
    }

    /// <summary>
    /// TR: Exception mapping sonucunda istemciye yazılacak güvenli HTTP hata alanlarını taşır.
    /// EN: Carries safe HTTP error fields written to the client after exception mapping.
    /// </summary>
    private sealed class ApiErrorDescriptor
    {
        /// <summary>
        /// TR: API hata descriptor nesnesini oluşturur.
        /// EN: Creates the API-error descriptor.
        /// </summary>
        /// <param name="statusCode">TR: HTTP status code. EN: HTTP status code.</param>
        /// <param name="title">TR: İnsan tarafından okunabilen kısa hata başlığı. EN: Short human-readable error title.</param>
        /// <param name="code">TR: İstemcinin logic için kullanabileceği stabil machine-readable code. EN: Stable machine-readable code usable by client logic.</param>
        /// <param name="detail">TR: Hassas iç detay içermeyen güvenli açıklama. EN: Safe detail text containing no sensitive internal information.</param>
        public ApiErrorDescriptor(int statusCode, string title, string code, string detail)
        {
            StatusCode = statusCode;
            Title = title;
            Code = code;
            Detail = detail;
        }

        /// <summary>TR: HTTP status code değerini döndürür. EN: Gets the HTTP status code.</summary>
        public int StatusCode { get; }

        /// <summary>TR: Problem Details title değerini döndürür. EN: Gets the Problem Details title.</summary>
        public string Title { get; }

        /// <summary>TR: Machine-readable error code değerini döndürür. EN: Gets the machine-readable error code.</summary>
        public string Code { get; }

        /// <summary>TR: İstemciye gösterilmesi güvenli detay metnini döndürür. EN: Gets the detail text safe to expose to the client.</summary>
        public string Detail { get; }
    }
}
