using FinWallet.Api.Contracts.Purchases;
using FinWallet.Application.Purchases;
using FinWallet.Shared.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinWallet.Api.Controllers;

/// <summary>TR: Authenticated customer merchant purchase use-case'ini durable internal/external fraud, campaign accounting ve idempotency ile public Web API olarak sunar. EN: Exposes authenticated customer merchant-purchase use case with durable internal/external fraud, campaign accounting and idempotency as public Web API.</summary>
[ApiController]
[Authorize]
[Route("api/v1/purchases")]
public sealed class PurchasesController : ControllerBase
{
    private readonly ExecuteFraudProtectedPurchaseHandler _handler;

    /// <summary>TR: Fraud-protected purchase use-case handler bağımlılığıyla controller'ı oluşturur. EN: Creates controller with fraud-protected purchase use-case handler dependency.</summary>
    /// <param name="handler">TR: Durable fraud + campaign + atomic posting orchestrator'ı. EN: Durable-fraud + campaign + atomic-posting orchestrator.</param>
    public PurchasesController(ExecuteFraudProtectedPurchaseHandler handler) => _handler = handler ?? throw new ArgumentNullException(nameof(handler));

    /// <summary>TR: Merchant purchase'ı server-side fraud sinyalleriyle değerlendirir; Allow/Approved sonrasında campaign provider ve double-entry posting çalışır. EN: Evaluates merchant purchase using server-side fraud signals; campaign provider and double-entry posting run only after Allow/Approved.</summary>
    /// <param name="request">TR: Wallet, merchant ve original amount request'i. EN: Wallet, merchant and original-amount request.</param>
    /// <param name="idempotencyKey">TR: Zorunlu durable `Idempotency-Key` header değeri. EN: Required durable `Idempotency-Key` header value.</param>
    /// <param name="cancellationToken">TR: Fraud/Campaign HTTP ve MSSQL iptal sinyali. EN: Fraud/Campaign HTTP and MSSQL cancellation signal.</param>
    /// <returns>TR: Completed yeni/replay purchase veya 202 manual-review sonucu döndürür. EN: Returns completed new/replayed purchase or a 202 manual-review result.</returns>
    [HttpPost]
    [ProducesResponseType(typeof(ServiceResult<PurchaseResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ServiceResult<object>), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ServiceResult<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ServiceResult<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ServiceResult<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ServiceResult<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ServiceResult<object>), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<ServiceResult<PurchaseResponse>>> ExecuteAsync(
        [FromBody] PurchaseRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParseExact(User.FindFirst("sub")?.Value, "N", out var customerId)
            || !Guid.TryParseExact(User.FindFirst("sid")?.Value, "N", out var sessionId))
        {
            return Unauthorized(ServiceResult<PurchaseResponse>.Failure("INVALID_ACCESS_TOKEN", "The access token customer or session identity is invalid."));
        }
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return BadRequest(ServiceResult<PurchaseResponse>.Failure("IDEMPOTENCY_KEY_REQUIRED", "Idempotency-Key header is required for purchases."));
        }

        var result = await _handler.HandleAsync(
            new PurchaseCommand(customerId, request.WalletId, request.MerchantId, request.Amount, idempotencyKey, HttpContext.TraceIdentifier),
            sessionId,
            cancellationToken);
        return Ok(ServiceResult<PurchaseResponse>.Success(
            new PurchaseResponse(result),
            result.WasReplay ? "PURCHASE_REPLAYED" : "PURCHASE_COMPLETED",
            result.WasReplay ? "The previously completed purchase result was replayed." : "Purchase completed successfully."));
    }
}
