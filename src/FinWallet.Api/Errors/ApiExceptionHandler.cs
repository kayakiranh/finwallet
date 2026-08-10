using FinWallet.Application.Authentication;
using FinWallet.Application.Banking;
using FinWallet.Application.Campaigns;
using FinWallet.Application.Corrections;
using FinWallet.Application.Cutoff;
using FinWallet.Application.Fraud;
using FinWallet.Application.Inbox;
using FinWallet.Application.Purchases;
using FinWallet.Application.Registration;
using FinWallet.Application.Transfers;
using FinWallet.Application.Wallets;
using FinWallet.Domain.Registration;
using FinWallet.Domain.Shared;
using FinWallet.Domain.Wallets;
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

            WalletTransferSessionInvalidException => new(StatusCodes.Status401Unauthorized, "TRANSFER_SESSION_INVALID", "The financial session is invalid or no longer active."),
            WalletTransferFraudDeniedException => new(StatusCodes.Status403Forbidden, "TRANSFER_FRAUD_DENIED", "The transfer was denied by fraud controls."),
            WalletTransferFraudReviewRequiredException => new(StatusCodes.Status202Accepted, "TRANSFER_REVIEW_REQUIRED", "The transfer requires additional review and no money was moved."),
            WalletTransferSourceNotFoundException => new(StatusCodes.Status404NotFound, "SOURCE_WALLET_NOT_FOUND", "The source wallet was not found."),
            WalletTransferDestinationNotFoundException => new(StatusCodes.Status404NotFound, "DESTINATION_WALLET_NOT_FOUND", "The destination wallet was not found."),
            WalletTransferIdempotencyConflictException => new(StatusCodes.Status409Conflict, "IDEMPOTENCY_CONFLICT", "The Idempotency-Key was already used with a different transfer request."),
            WalletTransferInProgressException => new(StatusCodes.Status409Conflict, "TRANSFER_IN_PROGRESS", "An identical transfer request is already in progress."),
            WalletTransferUnavailableException => new(StatusCodes.Status409Conflict, "TRANSFER_UNAVAILABLE", "One or more wallets cannot process this transfer."),
            WalletTransferFraudUnavailableException => new(StatusCodes.Status503ServiceUnavailable, "FRAUD_DEPENDENCY_UNAVAILABLE", "The required fraud service is temporarily unavailable."),

            PurchaseSessionInvalidException => new(StatusCodes.Status401Unauthorized, "PURCHASE_SESSION_INVALID", "The purchase session is invalid or no longer active."),
            PurchaseFraudDeniedException => new(StatusCodes.Status403Forbidden, "PURCHASE_FRAUD_DENIED", "The purchase was denied by fraud controls."),
            PurchaseFraudReviewRequiredException => new(StatusCodes.Status202Accepted, "PURCHASE_REVIEW_REQUIRED", "The purchase requires additional review and no money was moved."),
            PurchaseFraudUnavailableException => new(StatusCodes.Status503ServiceUnavailable, "FRAUD_DEPENDENCY_UNAVAILABLE", "The required fraud service is temporarily unavailable."),
            PurchaseUnavailableException => new(StatusCodes.Status409Conflict, "PURCHASE_UNAVAILABLE", "The wallet or merchant is not available for purchase."),
            PurchaseIdempotencyConflictException => new(StatusCodes.Status409Conflict, "IDEMPOTENCY_CONFLICT", "The Idempotency-Key was already used with a different purchase request."),

            FraudEventIdempotencyConflictException => new(StatusCodes.Status409Conflict, "FRAUD_IDEMPOTENCY_CONFLICT", "The Idempotency-Key was already used with a different fraud-evaluated request."),
            FraudEventNotFoundException => new(StatusCodes.Status404NotFound, "FRAUD_REVIEW_NOT_FOUND", "The fraud review event was not found."),
            FraudEventReviewConflictException => new(StatusCodes.Status409Conflict, "FRAUD_REVIEW_CONFLICT", "The fraud event is no longer pending review."),

            BankAccountWalletNotFoundException => new(StatusCodes.Status404NotFound, "WALLET_NOT_FOUND", "The wallet was not found."),
            BankAccountConcurrencyException => new(StatusCodes.Status409Conflict, "BANK_ACCOUNT_CONFLICT", "The bank account state changed concurrently. Retry the operation."),
            BankMoneyMovementAccountUnavailableException => new(StatusCodes.Status404NotFound, "BANK_ACCOUNT_UNAVAILABLE", "The bank account is not available for this operation."),
            BankMoneyMovementIdempotencyConflictException => new(StatusCodes.Status409Conflict, "IDEMPOTENCY_CONFLICT", "The Idempotency-Key was already used with a different bank movement request."),
            InboxMessageConflictException => new(StatusCodes.Status409Conflict, "BANK_CALLBACK_CONFLICT", "The callback message identifier was already used with a different payload."),
            BankCallbackTransactionNotFoundException => new(StatusCodes.Status404NotFound, "BANK_CALLBACK_TRANSACTION_NOT_FOUND", "The callback transaction was not found in FinWallet."),

            CorrectionTransactionNotFoundException => new(StatusCodes.Status404NotFound, "TRANSACTION_NOT_FOUND", "The original financial transaction was not found."),
            CorrectionNotAllowedException => new(StatusCodes.Status409Conflict, "CORRECTION_NOT_ALLOWED", "The requested correction is not allowed for the original transaction state or type."),
            CorrectionIdempotencyConflictException => new(StatusCodes.Status409Conflict, "IDEMPOTENCY_CONFLICT", "The Idempotency-Key was already used with a different correction request."),

            InsufficientBalanceException => new(StatusCodes.Status409Conflict, "INSUFFICIENT_BALANCE", "The source wallet has insufficient available balance."),
            CurrencyMismatchException => new(StatusCodes.Status400BadRequest, "CURRENCY_MISMATCH", "The wallet currencies do not match."),
            WalletConcurrencyException => new(StatusCodes.Status409Conflict, "WALLET_CONFLICT", "The wallet state changed concurrently. Retry the operation."),
            CutoffProviderException providerException => new(StatusCodes.Status503ServiceUnavailable, providerException.Code, providerException.Message),
            CampaignProviderException providerException => new(StatusCodes.Status503ServiceUnavailable, providerException.Code, providerException.Message),
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
