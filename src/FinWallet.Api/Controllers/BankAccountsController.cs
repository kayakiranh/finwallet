using FinWallet.Api.Contracts.Banking;
using FinWallet.Application.Banking;
using FinWallet.Domain.BankAccounts;
using FinWallet.Shared.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinWallet.Api.Controllers;

/// <summary>
/// TR: Authenticated customer'ın owned wallet'ları için FinWallet BankAccount açılış akışını controller tabanlı Web API üzerinden sunar.
/// EN: Exposes FinWallet BankAccount opening for authenticated customers' owned wallets through controller-based Web API.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/bank-accounts")]
public sealed class BankAccountsController : ControllerBase
{
    private readonly OpenBankAccountHandler _openBankAccountHandler;

    /// <summary>
    /// TR: BankAccount opening use-case handler bağımlılığıyla controller'ı oluşturur.
    /// EN: Creates the controller with its BankAccount-opening use-case handler dependency.
    /// </summary>
    /// <param name="openBankAccountHandler">TR: Durable internal/provider account-opening orchestration handler'ı. EN: Handler orchestrating durable internal/provider account opening.</param>
    public BankAccountsController(OpenBankAccountHandler openBankAccountHandler)
    {
        _openBankAccountHandler = openBankAccountHandler ?? throw new ArgumentNullException(nameof(openBankAccountHandler));
    }

    /// <summary>
    /// TR: JWT subject customer'a ait wallet için banka hesabı açar veya daha önce başlatılmış pending akışa idempotent biçimde devam eder.
    /// EN: Opens a bank account for a wallet owned by the JWT-subject customer or idempotently resumes an existing pending flow.
    /// </summary>
    /// <param name="request">TR: Bağlanacak internal wallet kimliğini taşıyan request. EN: Request carrying the internal wallet identifier to link.</param>
    /// <param name="cancellationToken">TR: DB ve dış provider işlemlerine yayılan request iptal sinyali. EN: Request cancellation signal propagated to DB and external-provider operations.</param>
    /// <returns>TR: Active/final hesap için 200, provider sonucu bekleniyorsa 202 ve tüm durumlarda ServiceResult body döndürür. EN: Returns 200 for active/final account state, 202 while provider completion is pending, always with a ServiceResult body.</returns>
    [HttpPost]
    [ProducesResponseType(typeof(ServiceResult<BankAccountResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ServiceResult<BankAccountResponse>), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ServiceResult<BankAccountResponse>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ServiceResult<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ServiceResult<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ServiceResult<object>), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ServiceResult<object>), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<ServiceResult<BankAccountResponse>>> OpenAsync(
        [FromBody] OpenBankAccountRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetAuthenticatedCustomerId(out var customerId))
        {
            return Unauthorized(ServiceResult<BankAccountResponse>.Failure(
                "INVALID_ACCESS_TOKEN",
                "The access token subject is invalid."));
        }

        var result = await _openBankAccountHandler.HandleAsync(
            new OpenBankAccountCommand(customerId, request.WalletId, HttpContext.TraceIdentifier),
            cancellationToken);
        var response = new BankAccountResponse(result);
        var serviceResult = ServiceResult<BankAccountResponse>.Success(
            response,
            result.Status == BankAccountStatus.Opening ? "BANK_ACCOUNT_PENDING" : "BANK_ACCOUNT_READY",
            result.Status == BankAccountStatus.Opening
                ? "Bank account opening is pending at the external provider."
                : "Bank account state is available.");

        return result.Status == BankAccountStatus.Opening
            ? StatusCode(StatusCodes.Status202Accepted, serviceResult)
            : Ok(serviceResult);
    }

    /// <summary>
    /// TR: Validated JWT içindeki `sub` claim'ini FinWallet customer GUID değerine dönüştürür.
    /// EN: Converts the `sub` claim from the validated JWT into a FinWallet customer GUID.
    /// </summary>
    /// <param name="customerId">TR: Parse başarılıysa authenticated customer kimliğini alır. EN: Receives authenticated customer identifier when parsing succeeds.</param>
    /// <returns>TR: `sub` claim'i geçerli FinWallet GUID ise true döndürür. EN: Returns true when the `sub` claim is a valid FinWallet GUID.</returns>
    private bool TryGetAuthenticatedCustomerId(out Guid customerId)
    {
        var subject = User.FindFirst("sub")?.Value;
        return Guid.TryParseExact(subject, "N", out customerId);
    }
}
