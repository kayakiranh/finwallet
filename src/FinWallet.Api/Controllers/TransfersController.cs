using FinWallet.Api.Contracts.Transfers;
using FinWallet.Application.Transfers;
using FinWallet.Shared.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinWallet.Api.Controllers;

/// <summary>
/// TR: Authenticated customer'ın fraud-guarded ve durable idempotent internal wallet transfer use-case'ini controller tabanlı Web API üzerinden sunar.
/// EN: Exposes the fraud-guarded and durably idempotent internal wallet-transfer use case for authenticated customers through controller-based Web API.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/transfers")]
public sealed class TransfersController : ControllerBase
{
    private readonly ExecuteWalletTransferHandler _handler;

    /// <summary>TR: Wallet-transfer use-case handler bağımlılığıyla controller'ı oluşturur. EN: Creates the controller with its wallet-transfer use-case handler dependency.</summary>
    /// <param name="handler">TR: Fraud + atomic posting orchestration handler'ı. EN: Fraud + atomic-posting orchestration handler.</param>
    public TransfersController(ExecuteWalletTransferHandler handler)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
    }

    /// <summary>
    /// TR: Source wallet'tan destination wallet'a para transfer eder; server-side session/risk doğrulaması ve zorunlu durable idempotency uygular.
    /// EN: Transfers money from source wallet to destination wallet with server-side session/risk validation and mandatory durable idempotency.
    /// </summary>
    /// <param name="request">TR: Source/destination wallet ve amount taşıyan request. EN: Request carrying source/destination wallets and amount.</param>
    /// <param name="idempotencyKey">TR: `Idempotency-Key` header değeri. EN: `Idempotency-Key` header value.</param>
    /// <param name="cancellationToken">TR: Fraud/SQL işlemlerine yayılan request iptal sinyali. EN: Request cancellation signal propagated to fraud/SQL operations.</param>
    /// <returns>TR: Completed yeni/replay transferı ServiceResult içinde döndürür. EN: Returns a newly completed or replayed transfer inside ServiceResult.</returns>
    [HttpPost]
    [ProducesResponseType(typeof(ServiceResult<WalletTransferResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ServiceResult<WalletTransferResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ServiceResult<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ServiceResult<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ServiceResult<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ServiceResult<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ServiceResult<object>), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<ServiceResult<WalletTransferResponse>>> ExecuteAsync(
        [FromBody] WalletTransferRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (!TryGetAuthenticatedIdentity(out var customerId, out var sessionId))
        {
            return Unauthorized(ServiceResult<WalletTransferResponse>.Failure(
                "INVALID_ACCESS_TOKEN",
                "The access token customer/session identity is invalid."));
        }

        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return BadRequest(ServiceResult<WalletTransferResponse>.Failure(
                "IDEMPOTENCY_KEY_REQUIRED",
                "Idempotency-Key header is required for wallet transfers."));
        }

        var result = await _handler.HandleAsync(
            new ExecuteWalletTransferCommand(
                customerId,
                sessionId,
                request.SourceWalletId,
                request.DestinationWalletId,
                request.Amount,
                idempotencyKey,
                HttpContext.TraceIdentifier),
            cancellationToken);

        var response = new WalletTransferResponse(result);
        return Ok(ServiceResult<WalletTransferResponse>.Success(
            response,
            result.WasReplay ? "WALLET_TRANSFER_REPLAYED" : "WALLET_TRANSFER_COMPLETED",
            result.WasReplay
                ? "The previously completed wallet transfer result was replayed."
                : "Wallet transfer completed successfully."));
    }

    /// <summary>
    /// TR: Validated JWT içindeki `sub` ve `sid` claim'lerini FinWallet customer/session GUID değerlerine dönüştürür.
    /// EN: Converts validated JWT `sub` and `sid` claims into FinWallet customer/session GUID values.
    /// </summary>
    /// <param name="customerId">TR: Parse başarılıysa authenticated customer kimliğini alır. EN: Receives authenticated customer identifier when parsing succeeds.</param>
    /// <param name="sessionId">TR: Parse başarılıysa authenticated session kimliğini alır. EN: Receives authenticated session identifier when parsing succeeds.</param>
    /// <returns>TR: Her iki claim geçerli N-format GUID ise true döndürür. EN: Returns true when both claims are valid N-format GUIDs.</returns>
    private bool TryGetAuthenticatedIdentity(out Guid customerId, out Guid sessionId)
    {
        customerId = Guid.Empty;
        sessionId = Guid.Empty;

        var subject = User.FindFirst("sub")?.Value;
        var session = User.FindFirst("sid")?.Value;
        return Guid.TryParseExact(subject, "N", out customerId) &&
               Guid.TryParseExact(session, "N", out sessionId);
    }
}
