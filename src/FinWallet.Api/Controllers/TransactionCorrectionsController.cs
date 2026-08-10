using FinWallet.Api.Contracts.Corrections;
using FinWallet.Application.Corrections;
using FinWallet.Shared.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinWallet.Api.Controllers;

/// <summary>TR: Purchase refund ve güvenli internal wallet-transfer reversal use-case'lerini public Web API olarak sunar. EN: Exposes purchase-refund and safe internal wallet-transfer-reversal use cases as public Web API.</summary>
[ApiController]
[Authorize]
[Route("api/v1/transactions")]
public sealed class TransactionCorrectionsController : ControllerBase
{
    private readonly ExecuteTransactionCorrectionHandler _handler;

    /// <summary>TR: Correction handler bağımlılığıyla controller'ı oluşturur. EN: Creates controller with correction-handler dependency.</summary>
    /// <param name="handler">TR: Atomic correction handler'ı. EN: Atomic correction handler.</param>
    public TransactionCorrectionsController(ExecuteTransactionCorrectionHandler handler) => _handler = handler ?? throw new ArgumentNullException(nameof(handler));

    /// <summary>TR: Completed Purchase işlemini tam refund ile ters journal ve wallet credit kullanarak geri alır. EN: Fully refunds a completed Purchase using an opposite journal and wallet credit.</summary>
    /// <param name="transactionId">TR: Original Purchase transaction kimliği. EN: Original Purchase transaction identifier.</param>
    /// <param name="idempotencyKey">TR: Zorunlu durable idempotency anahtarı. EN: Required durable-idempotency key.</param>
    /// <param name="cancellationToken">TR: MSSQL iptal sinyali. EN: MSSQL cancellation signal.</param>
    /// <returns>TR: Completed refund sonucunu döndürür. EN: Returns completed refund result.</returns>
    [HttpPost("{transactionId:guid}/refund")]
    [ProducesResponseType(typeof(ServiceResult<TransactionCorrectionResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ServiceResult<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ServiceResult<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ServiceResult<object>), StatusCodes.Status409Conflict)]
    public Task<ActionResult<ServiceResult<TransactionCorrectionResponse>>> RefundAsync(Guid transactionId, [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey, CancellationToken cancellationToken) => ExecuteAsync(transactionId, TransactionCorrectionType.Refund, idempotencyKey, cancellationToken);

    /// <summary>TR: Completed internal WalletTransfer işlemini destination→source ters bakiye hareketi ve opposite ledger journal ile geri alır; external-bank işlemleri bu endpoint'ten reverse edilmez. EN: Reverses a completed internal WalletTransfer using destination→source balance movement and an opposite ledger journal; external-bank operations are not reversed by this endpoint.</summary>
    /// <param name="transactionId">TR: Original WalletTransfer transaction kimliği. EN: Original WalletTransfer transaction identifier.</param>
    /// <param name="idempotencyKey">TR: Zorunlu durable idempotency anahtarı. EN: Required durable-idempotency key.</param>
    /// <param name="cancellationToken">TR: MSSQL iptal sinyali. EN: MSSQL cancellation signal.</param>
    /// <returns>TR: Completed reversal sonucunu döndürür. EN: Returns completed reversal result.</returns>
    [HttpPost("{transactionId:guid}/reversal")]
    [ProducesResponseType(typeof(ServiceResult<TransactionCorrectionResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ServiceResult<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ServiceResult<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ServiceResult<object>), StatusCodes.Status409Conflict)]
    public Task<ActionResult<ServiceResult<TransactionCorrectionResponse>>> ReverseAsync(Guid transactionId, [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey, CancellationToken cancellationToken) => ExecuteAsync(transactionId, TransactionCorrectionType.Reversal, idempotencyKey, cancellationToken);

    private async Task<ActionResult<ServiceResult<TransactionCorrectionResponse>>> ExecuteAsync(Guid transactionId, TransactionCorrectionType type, string? idempotencyKey, CancellationToken cancellationToken)
    {
        if (!Guid.TryParseExact(User.FindFirst("sub")?.Value, "N", out var customerId))
        {
            return Unauthorized(ServiceResult<TransactionCorrectionResponse>.Failure("INVALID_ACCESS_TOKEN", "The access token customer identity is invalid."));
        }
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return BadRequest(ServiceResult<TransactionCorrectionResponse>.Failure("IDEMPOTENCY_KEY_REQUIRED", "Idempotency-Key header is required for financial corrections."));
        }

        var result = await _handler.HandleAsync(new TransactionCorrectionCommand(customerId, transactionId, type, idempotencyKey, HttpContext.TraceIdentifier), cancellationToken);
        return Ok(ServiceResult<TransactionCorrectionResponse>.Success(
            new TransactionCorrectionResponse(result),
            result.WasReplay ? $"{type.ToString().ToUpperInvariant()}_REPLAYED" : $"{type.ToString().ToUpperInvariant()}_COMPLETED",
            result.WasReplay ? "The previously completed correction result was replayed." : $"{type} completed successfully."));
    }
}
