using FinWallet.Application.Authentication;
using FinWallet.Application.Banking;
using FinWallet.Application.Registration;
using FinWallet.Application.Wallets;
using FinWallet.Domain.Registration;
using FinWallet.Shared.Contracts;
using Microsoft.AspNetCore.Diagnostics;

namespace FinWallet.Api.Errors;

/// <summary>
/// TR: Beklenen application/domain hatalarını stabil HTTP status ve ServiceResult hata kodlarına dönüştürür; beklenmeyen exception ayrıntılarını istemciye sızdırmaz.
/// EN: Converts expected application/domain failures into stable HTTP statuses and ServiceResult error codes while preventing unexpected exception details from leaking to clients.
/// </summary>
public sealed class ApiExceptionHandler : IExceptionHandler
{
    /// <summary>TR: Exception tipini güvenli ServiceResult hata sözleşmesine map eder ve HTTP cevabını yazar. EN: Maps an exception type to the safe ServiceResult error contract and writes the HTTP response.</summary>
    /// <param name="httpContext">TR: Hata cevabının yazılacağı HTTP context. EN: HTTP context receiving the failure response.</param>
    /// <param name="exception">TR: Request pipeline sırasında oluşan exception. EN: Exception raised during the request pipeline.</param>
    /// <param name="cancellationToken">TR: Hata cevabı yazım iptal sinyali. EN: Cancellation signal for writing the failure response.</param>
    /// <returns>TR: Hata işlendiği için true döndürür. EN: Returns true because the failure is handled.</returns>
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(exception);
        var descriptor = Describe(exception);
        httpContext.Response.StatusCode = descriptor.StatusCode;
        await httpContext.Response.WriteAsJsonAsync(
            ServiceResult<object>.Failure(descriptor.Code, descriptor.Message),
            cancellationToken);
        return true;
    }

    /// <summary>TR: Exception tipini güvenli HTTP status/code/message değerlerine dönüştürür. EN: Converts an exception into safe HTTP status/code/message values.</summary>
    /// <param name="exception">TR: Sınıflandırılacak exception. EN: Exception to classify.</param>
    /// <returns>TR: Güvenli API hata descriptor değerini döndürür. EN: Returns the safe API-error descriptor.</returns>
    private static ApiErrorDescriptor Describe(Exception exception)
    {
        return exception switch
        {
            RegistrationNotAllowedException => new(StatusCodes.Status400BadRequest, "REGISTRATION_NOT_ALLOWED", "The registration country or phone number is not eligible."),
            RegistrationConflictException => new(StatusCodes.Status409Conflict, "REGISTRATION_CONFLICT", "A customer registration already exists for the supplied identity."),
            RegistrationOtpRateLimitException => new(StatusCodes.Status429TooManyRequests, "OTP_RESEND_RATE_LIMIT", "A new verification code cannot be issued yet."),
            InvalidRegistrationOtpException => new(StatusCodes.Status400BadRequest, "INVALID_REGISTRATION_OTP", "The verification code is invalid, expired or already consumed."),
            AuthenticationTemporarilyLockedException => new(StatusCodes.Status429TooManyRequests, "AUTH_TEMPORARILY_LOCKED", "Authentication is temporarily unavailable for this credential."),
            InvalidCredentialsException => new(StatusCodes.Status401Unauthorized, "INVALID_CREDENTIALS", "The supplied credentials are invalid."),
            RefreshTokenReuseDetectedException => new(StatusCodes.Status401Unauthorized, "REFRESH_TOKEN_REUSE_DETECTED", "The session was revoked because refresh-token reuse was detected."),
            InvalidRefreshTokenException => new(StatusCodes.Status401Unauthorized, "INVALID_REFRESH_TOKEN", "The refresh token is invalid or no longer usable."),
            WalletConcurrencyException => new(StatusCodes.Status409Conflict, "WALLET_CONFLICT", "The wallet state changed concurrently. Retry the operation."),
            BankAccountWalletNotFoundException => new(StatusCodes.Status404NotFound, "WALLET_NOT_FOUND", "The wallet was not found."),
            BankAccountConcurrencyException => new(StatusCodes.Status409Conflict, "BANK_ACCOUNT_CONFLICT", "The bank account state changed concurrently. Retry the operation."),
            ExternalBankProviderException providerException when providerException.IsRetryable => new(StatusCodes.Status503ServiceUnavailable, providerException.Code, providerException.Message),
            ExternalBankProviderException providerException => new(StatusCodes.Status502BadGateway, providerException.Code, providerException.Message),
            ArgumentException => new(StatusCodes.Status400BadRequest, "INVALID_REQUEST", "One or more request values are invalid."),
            HttpRequestException => new(StatusCodes.Status503ServiceUnavailable, "DEPENDENCY_UNAVAILABLE", "A required external service is temporarily unavailable."),
            _ => new(StatusCodes.Status500InternalServerError, "UNEXPECTED_ERROR", "An unexpected server error occurred.")
        };
    }

    /// <summary>TR: Güvenli HTTP status, code ve message alanlarını taşır. EN: Carries safe HTTP status, code and message fields.</summary>
    private sealed class ApiErrorDescriptor
    {
        /// <summary>TR: API hata descriptor nesnesini oluşturur. EN: Creates the API-error descriptor.</summary>
        /// <param name="statusCode">TR: HTTP status code. EN: HTTP status code.</param>
        /// <param name="code">TR: Stabil machine-readable code. EN: Stable machine-readable code.</param>
        /// <param name="message">TR: Güvenli hata açıklaması. EN: Safe failure description.</param>
        public ApiErrorDescriptor(int statusCode, string code, string message)
        {
            StatusCode = statusCode;
            Code = code;
            Message = message;
        }

        /// <summary>TR: HTTP status code değerini döndürür. EN: Gets HTTP status code.</summary>
        public int StatusCode { get; }

        /// <summary>TR: Machine-readable error code değerini döndürür. EN: Gets machine-readable error code.</summary>
        public string Code { get; }

        /// <summary>TR: İstemciye güvenle gösterilebilecek mesajı döndürür. EN: Gets the message safe to expose to clients.</summary>
        public string Message { get; }
    }
}
