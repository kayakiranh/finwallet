using FinWallet.Api.Contracts.Purchases;
using FinWallet.Application.Purchases;
using FinWallet.Shared.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinWallet.Api.Controllers;

/// <summary>TR: Authenticated customer merchant purchase use-case'ini campaign accounting ve durable idempotency ile public Web API olarak sunar. EN: Exposes authenticated customer merchant-purchase use case with campaign accounting and durable idempotency as public Web API.</summary>
[ApiController]
[Authorize]
[Route("api/v1/purchases")]
public sealed class PurchasesController : ControllerBase
{
    private readonly ExecutePurchaseHandler _handler;

    /// <summary>TR: Purchase use-case handler bağımlılığıyla controller'ı oluşturur. EN: Creates controller with purchase use-case handler dependency.</summary>
    /// <param name="handler">TR: Campaign + atomic posting orchestrator'ı. EN: Campaign + atomic-posting orchestrator.</param>
    public PurchasesController(ExecutePurchaseHandler handler) => _handler = handler ?? throw new ArgumentNullException(nameof(handler));

    /// <summary>TR: Merchant purchase'ı campaign provider ile değerlendirir ve customer/merchant/platform ekonomik etkilerini double-entry ledger'a atomik olarak post eder. EN: Evaluates merchant purchase with campaign provider and atomically posts customer/merchant/platform economic effects to double-entry ledger.</summary>
    /// <param name="request">TR: Wallet, merchant ve original amount request'i. EN: Wallet, merchant and original-amount request.</param>
    /// <param name="idempotencyKey">TR: Zorunlu durable `Idempotency-Key` header değeri. EN: Required durable `Idempotency-Key` header value.</param>
    /// <param name="cancellationToken">TR: Campaign HTTP ve MSSQL iptal sinyali. EN: Campaign HTTP and MSSQL cancellation signal.</param>
    /// <returns>TR: Completed yeni veya replay purchase sonucunu döndürür. EN: Returns completed new or replayed purchase result.</returns>
    [HttpPost]
    [ProducesResponseType(typeof(ServiceResult<PurchaseResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ServiceResult<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ServiceResult<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ServiceResult<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ServiceResult<object>), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<ServiceResult<PurchaseResponse>>> ExecuteAsync(
        [FromBody] PurchaseRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParseExact(User.FindFirst("sub")?.Value, "N", out var customerId))
        {
            return Unauthorized(ServiceResult<PurchaseResponse>.Failure("INVALID_ACCESS_TOKEN", "The access token customer identity is invalid."));
        }
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return BadRequest(ServiceResult<PurchaseResponse>.Failure("IDEMPOTENCY_KEY_REQUIRED", "Idempotency-Key header is required for purchases."));
        }

        var result = await _handler.HandleAsync(
            new PurchaseCommand(customerId, request.WalletId, request.MerchantId, request.Amount, idempotencyKey, HttpContext.TraceIdentifier),
            cancellationToken);
        return Ok(ServiceResult<PurchaseResponse>.Success(
            new PurchaseResponse(result),
            result.WasReplay ? "PURCHASE_REPLAYED" : "PURCHASE_COMPLETED",
            result.WasReplay ? "The previously completed purchase result was replayed." : "Purchase completed successfully."));
    }
}
