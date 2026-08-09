using FinWallet.Application.Authentication;
using FinWallet.Application.Registration;
using FinWallet.Domain.Registration;
using FinWallet.Shared.Contracts;
using Microsoft.AspNetCore.Diagnostics;

namespace FinWallet.Api.Errors;

/// <summary>
/// TR: Beklenen registration/authentication hatalarını stabil HTTP status ve ServiceResult hata kodlarına dönüştürür; beklenmeyen exception ayrıntılarını istemciye sızdırmaz.
/// EN: Converts expected registration/authentication errors into stable HTTP statuses and ServiceResult error codes while preventing unexpected exception details from leaking to clients.
/// </summary>
public sealed class ApiExceptionHandler : IExceptionHandler
{
    /// <summary>
    /// TR: Exception tipini güvenli ServiceResult hata sözleşmesine map eder ve HTTP cevabını yazar.
    /// EN: Maps an exception type to the safe ServiceResult error contract and writes the HTTP response.
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

        var result = ServiceResult<object>.Failure(descriptor.Code, descriptor.Message);
        await httpContext.Response.WriteAsJsonAsync(result, cancellationToken);
        return true;
    }

    /// <summary>
    /// TR: Exception tipini istemciye açıklanması güvenli HTTP status, error code ve mesaj değerlerine dönüştürür.
    /// EN: Converts an exception type into HTTP status, error-code and message values that are safe to expose to clients.
    /// </summary>
    /// <param name="exception">TR: Sınıflandırılacak exception. EN: Exception to classify.</param>
    /// <returns>TR: Güvenli API hata descriptor değerini döndürür. EN: Returns the safe API-error descriptor.</returns>
    private static ApiErrorDescriptor Describe(Exception exception)
    {
        return exception switch
        {
            RegistrationNotAllowedException => new ApiErrorDescriptor(
                StatusCodes.Status400BadRequest,
                "REGISTRATION_NOT_ALLOWED",
                "The registration country or phone number is not eligible."),
            RegistrationConflictException => new ApiErrorDescriptor(
                StatusCodes.Status409Conflict,
                "REGISTRATION_CONFLICT",
                "A customer registration already exists for the supplied identity."),
            RegistrationOtpRateLimitException => new ApiErrorDescriptor(
                StatusCodes.Status429TooManyRequests,
                "OTP_RESEND_RATE_LIMIT",
                "A new verification code cannot be issued yet."),
            InvalidRegistrationOtpException => new ApiErrorDescriptor(
                StatusCodes.Status400BadRequest,
                "INVALID_REGISTRATION_OTP",
                "The verification code is invalid, expired or already consumed."),
            AuthenticationTemporarilyLockedException => new ApiErrorDescriptor(
                StatusCodes.Status429TooManyRequests,
                "AUTH_TEMPORARILY_LOCKED",
                "Authentication is temporarily unavailable for this credential."),
            InvalidCredentialsException => new ApiErrorDescriptor(
                StatusCodes.Status401Unauthorized,
                "INVALID_CREDENTIALS",
                "The supplied credentials are invalid."),
            RefreshTokenReuseDetectedException => new ApiErrorDescriptor(
                StatusCodes.Status401Unauthorized,
                "REFRESH_TOKEN_REUSE_DETECTED",
                "The session was revoked because refresh-token reuse was detected."),
            InvalidRefreshTokenException => new ApiErrorDescriptor(
                StatusCodes.Status401Unauthorized,
                "INVALID_REFRESH_TOKEN",
                "The refresh token is invalid or no longer usable."),
            ArgumentException => new ApiErrorDescriptor(
                StatusCodes.Status400BadRequest,
                "INVALID_REQUEST",
                "One or more request values are invalid."),
            HttpRequestException => new ApiErrorDescriptor(
                StatusCodes.Status503ServiceUnavailable,
                "DEPENDENCY_UNAVAILABLE",
                "A required external service is temporarily unavailable."),
            _ => new ApiErrorDescriptor(
                StatusCodes.Status500InternalServerError,
                "UNEXPECTED_ERROR",
                "An unexpected server error occurred.")
        };
    }

    /// <summary>
    /// TR: Exception mapping sonucunda istemciye yazılacak güvenli HTTP status, code ve message alanlarını taşır.
    /// EN: Carries safe HTTP status, code and message fields written to the client after exception mapping.
    /// </summary>
    private sealed class ApiErrorDescriptor
    {
        /// <summary>
        /// TR: API hata descriptor nesnesini oluşturur.
        /// EN: Creates the API-error descriptor.
        /// </summary>
        /// <param name="statusCode">TR: HTTP status code. EN: HTTP status code.</param>
        /// <param name="code">TR: İstemcinin logic için kullanabileceği stabil machine-readable code. EN: Stable machine-readable code usable by client logic.</param>
        /// <param name="message">TR: Hassas iç detay içermeyen güvenli açıklama. EN: Safe message containing no sensitive internal information.</param>
        public ApiErrorDescriptor(int statusCode, string code, string message)
        {
            StatusCode = statusCode;
            Code = code;
            Message = message;
        }

        /// <summary>TR: HTTP status code değerini döndürür. EN: Gets the HTTP status code.</summary>
        public int StatusCode { get; }

        /// <summary>TR: Machine-readable error code değerini döndürür. EN: Gets the machine-readable error code.</summary>
        public string Code { get; }

        /// <summary>TR: İstemciye gösterilmesi güvenli hata mesajını döndürür. EN: Gets the failure message safe to expose to the client.</summary>
        public string Message { get; }
    }
}
