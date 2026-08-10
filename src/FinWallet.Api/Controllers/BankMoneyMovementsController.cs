using FinWallet.Api.Contracts.Banking;
using FinWallet.Application.Banking;
using FinWallet.Shared.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinWallet.Api.Controllers;

/// <summary>TR: Authenticated customer'ın external bank account ile wallet arasındaki deposit/withdrawal use-case'lerini public Web API olarak sunar. EN: Exposes authenticated customer deposit/withdrawal use cases between an external bank account and wallet as public Web API.</summary>
[ApiController]
[Authorize]
[Route("api/v1/bank-movements")]
public sealed class BankMoneyMovementsController : ControllerBase
{
    private readonly ExecuteBankDepositHandler _depositHandler;
    private readonly ExecuteBankWithdrawalHandler _withdrawalHandler;

    /// <summary>TR: Deposit ve withdrawal handler bağımlılıklarıyla controller'ı oluşturur. EN: Creates the controller with deposit and withdrawal handler dependencies.</summary>
    /// <param name="depositHandler">TR: Bank→Wallet deposit handler'ı. EN: Bank→Wallet deposit handler.</param>
    /// <param name="withdrawalHandler">TR: Wallet→Bank withdrawal handler'ı. EN: Wallet→Bank withdrawal handler.</param>
    public BankMoneyMovementsController(ExecuteBankDepositHandler depositHandler, ExecuteBankWithdrawalHandler withdrawalHandler)
    {
        _depositHandler = depositHandler ?? throw new ArgumentNullException(nameof(depositHandler));
        _withdrawalHandler = withdrawalHandler ?? throw new ArgumentNullException(nameof(withdrawalHandler));
    }

    /// <summary>TR: External bank account'tan bağlı FinWallet wallet'a para aktarır; provider tarafında debit/withdrawal, FinWallet tarafında BankDeposit olarak muhasebeleştirilir. EN: Moves money from the external bank account into the linked FinWallet wallet; it is a provider debit/withdrawal and a FinWallet BankDeposit.</summary>
    /// <param name="request">TR: BankAccount ve tutar request'i. EN: BankAccount and amount request.</param>
    /// <param name="idempotencyKey">TR: Zorunlu durable `Idempotency-Key` header değeri. EN: Required durable `Idempotency-Key` header value.</param>
    /// <param name="cancellationToken">TR: SQL/HTTP iptal sinyali. EN: SQL/HTTP cancellation signal.</param>
    /// <returns>TR: Completed veya Pending deposit sonucunu döndürür. EN: Returns Completed or Pending deposit result.</returns>
    [HttpPost("deposits")]
    [ProducesResponseType(typeof(ServiceResult<BankMoneyMovementResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ServiceResult<BankMoneyMovementResponse>), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ServiceResult<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ServiceResult<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ServiceResult<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ServiceResult<object>), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<ServiceResult<BankMoneyMovementResponse>>> DepositAsync(
        [FromBody] BankMoneyMovementRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (!TryGetCustomerId(out var customerId)) return Unauthorized(InvalidIdentity());
        if (string.IsNullOrWhiteSpace(idempotencyKey)) return BadRequest(MissingIdempotency());

        var result = await _depositHandler.HandleAsync(customerId, request.BankAccountId, request.Amount, idempotencyKey, HttpContext.TraceIdentifier, cancellationToken);
        return ToActionResult(result, "BankDeposit");
    }

    /// <summary>TR: FinWallet wallet'tan bağlı external bank account'a para aktarır; cutoff sonrası provider tarafında deposit/credit uygulanır. EN: Moves money from the FinWallet wallet into the linked external bank account; after cutoff it is applied as a provider deposit/credit.</summary>
    /// <param name="request">TR: BankAccount ve tutar request'i. EN: BankAccount and amount request.</param>
    /// <param name="idempotencyKey">TR: Zorunlu durable `Idempotency-Key` header değeri. EN: Required durable `Idempotency-Key` header value.</param>
    /// <param name="cancellationToken">TR: Cutoff/SQL/HTTP iptal sinyali. EN: Cutoff/SQL/HTTP cancellation signal.</param>
    /// <returns>TR: Scheduled, Pending veya Completed withdrawal sonucunu döndürür. EN: Returns Scheduled, Pending or Completed withdrawal result.</returns>
    [HttpPost("withdrawals")]
    [ProducesResponseType(typeof(ServiceResult<BankMoneyMovementResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ServiceResult<BankMoneyMovementResponse>), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ServiceResult<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ServiceResult<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ServiceResult<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ServiceResult<object>), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<ServiceResult<BankMoneyMovementResponse>>> WithdrawAsync(
        [FromBody] BankMoneyMovementRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (!TryGetCustomerId(out var customerId)) return Unauthorized(InvalidIdentity());
        if (string.IsNullOrWhiteSpace(idempotencyKey)) return BadRequest(MissingIdempotency());

        var result = await _withdrawalHandler.HandleAsync(customerId, request.BankAccountId, request.Amount, idempotencyKey, HttpContext.TraceIdentifier, cancellationToken);
        return ToActionResult(result, "BankWithdrawal");
    }

    private ActionResult<ServiceResult<BankMoneyMovementResponse>> ToActionResult(BankMoneyMovementResult result, string operation)
    {
        var response = new BankMoneyMovementResponse(result, operation);
        var code = result.State switch
        {
            BankMoneyMovementState.Completed => $"{operation.ToUpperInvariant()}_COMPLETED",
            BankMoneyMovementState.Failed => $"{operation.ToUpperInvariant()}_FAILED",
            BankMoneyMovementState.Scheduled => $"{operation.ToUpperInvariant()}_SCHEDULED",
            _ => $"{operation.ToUpperInvariant()}_PENDING"
        };
        var envelope = ServiceResult<BankMoneyMovementResponse>.Success(response, code, $"{operation} state is {result.State}.");
        return result.State is BankMoneyMovementState.Pending or BankMoneyMovementState.Scheduled
            ? StatusCode(StatusCodes.Status202Accepted, envelope)
            : Ok(envelope);
    }

    private bool TryGetCustomerId(out Guid customerId)
    {
        var subject = User.FindFirst("sub")?.Value;
        return Guid.TryParseExact(subject, "N", out customerId);
    }

    private static ServiceResult<BankMoneyMovementResponse> InvalidIdentity() => ServiceResult<BankMoneyMovementResponse>.Failure("INVALID_ACCESS_TOKEN", "The access token customer identity is invalid.");
    private static ServiceResult<BankMoneyMovementResponse> MissingIdempotency() => ServiceResult<BankMoneyMovementResponse>.Failure("IDEMPOTENCY_KEY_REQUIRED", "Idempotency-Key header is required for bank money movements.");
}
