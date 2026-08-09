using FakeBank.Api.Contracts;
using FakeBank.Api.Services;
using FinWallet.Shared.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace FakeBank.Api.Controllers;

/// <summary>
/// TR: Pending hesap açılışlarının polling ile izlenmesi için FakeBank hesap durumunu read-only Web API üzerinden sunar.
/// EN: Exposes read-only FakeBank account state so pending account openings can be monitored by polling.
/// </summary>
[ApiController]
[Route("api/v1/bank/accounts")]
public sealed class BankAccountQueryController : ControllerBase
{
    private readonly FakeBankProviderService _providerService;

    /// <summary>
    /// TR: Provider state servisi bağımlılığıyla hesap sorgu controller'ını oluşturur.
    /// EN: Creates the account-query controller with its provider-state service dependency.
    /// </summary>
    /// <param name="providerService">TR: FakeBank account state servisi. EN: FakeBank account-state service.</param>
    public BankAccountQueryController(FakeBankProviderService providerService)
    {
        _providerService = providerService ?? throw new ArgumentNullException(nameof(providerService));
    }

    /// <summary>
    /// TR: Provider hesap kimliğiyle güncel hesap durumunu finansal state değiştirmeden döndürür.
    /// EN: Returns current account state by provider account identifier without mutating financial state.
    /// </summary>
    /// <param name="accountId">TR: Sorgulanacak provider hesap kimliği. EN: Provider account identifier to query.</param>
    /// <returns>TR: Güncel provider account sonucunu ServiceResult içinde döndürür. EN: Returns current provider-account result inside ServiceResult.</returns>
    [HttpGet("{accountId:guid}")]
    [ProducesResponseType(typeof(ServiceResult<OpenAccountResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ServiceResult<OpenAccountResponse>), StatusCodes.Status404NotFound)]
    public ActionResult<ServiceResult<OpenAccountResponse>> GetAccount(Guid accountId)
    {
        try
        {
            var response = _providerService.GetAccount(accountId);
            return Ok(ServiceResult<OpenAccountResponse>.Success(
                response,
                "BANK_ACCOUNT_FOUND",
                "External bank account found."));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(ServiceResult<OpenAccountResponse>.Failure(
                "BANK_ACCOUNT_NOT_FOUND",
                "External bank account was not found."));
        }
    }
}
