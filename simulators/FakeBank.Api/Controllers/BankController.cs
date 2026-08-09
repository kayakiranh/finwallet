using FakeBank.Api.Contracts;
using FakeBank.Api.Services;
using FinWallet.Shared.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace FakeBank.Api.Controllers;

/// <summary>
/// TR: FakeBank hesap açılışı, provider para hareketleri, pending finalization ve reconciliation statement akışlarını controller tabanlı Web API üzerinden sunar.
/// EN: Exposes FakeBank account opening, provider money movements, pending finalization and reconciliation statements through controller-based Web API.
/// </summary>
[ApiController]
[Route("api/v1/bank")]
public sealed class BankController : ControllerBase
{
    private readonly FakeBankProviderService _providerService;

    /// <summary>
    /// TR: FakeBank provider state servisi bağımlılığıyla controller'ı oluşturur.
    /// EN: Creates the controller with its FakeBank provider-state service dependency.
    /// </summary>
    /// <param name="providerService">TR: External account, transaction, idempotency ve statement state'ini yöneten provider servisi. EN: Provider service managing external account, transaction, idempotency and statement state.</param>
    public BankController(FakeBankProviderService providerService)
    {
        _providerService = providerService ?? throw new ArgumentNullException(nameof(providerService));
    }

    /// <summary>
    /// TR: Currency-specific harici banka hesabı açar; `X-Fake-Mode=pending` ise hesabı Pending durumda bırakır.
    /// EN: Opens a currency-specific external bank account and leaves it Pending when `X-Fake-Mode=pending` is supplied.
    /// </summary>
    /// <param name="request">TR: External customer, currency ve provider idempotency key içeren hesap açılış request'i. EN: Account-opening request containing external customer, currency and provider idempotency key.</param>
    /// <param name="cancellationToken">TR: Fake delay/timeout simülasyonunun iptal sinyali. EN: Cancellation signal for fake delay/timeout simulation.</param>
    /// <returns>TR: Provider hesap açılış sonucunu ServiceResult içinde döndürür. EN: Returns provider account-opening result inside ServiceResult.</returns>
    [HttpPost("accounts")]
    [ProducesResponseType(typeof(ServiceResult<OpenAccountResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ServiceResult<OpenAccountResponse>), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ServiceResult<OpenAccountResponse>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ServiceResult<OpenAccountResponse>), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<ServiceResult<OpenAccountResponse>>> OpenAccountAsync(
        [FromBody] OpenAccountRequest request,
        CancellationToken cancellationToken)
    {
        if (IsFailMode())
        {
            return ProviderUnavailable<OpenAccountResponse>();
        }

        await ApplyDelayModeAsync(cancellationToken);

        try
        {
            var response = _providerService.OpenAccount(request, IsPendingMode());
            return Ok(ServiceResult<OpenAccountResponse>.Success(
                response,
                "BANK_ACCOUNT_ACCEPTED",
                "External bank account request accepted."));
        }
        catch (ArgumentException)
        {
            return UnprocessableEntity(ServiceResult<OpenAccountResponse>.Failure(
                "INVALID_BANK_ACCOUNT_REQUEST",
                "The external bank account request is invalid."));
        }
        catch (InvalidOperationException)
        {
            return Conflict(ServiceResult<OpenAccountResponse>.Failure(
                "BANK_REQUEST_KEY_CONFLICT",
                "The provider request key conflicts with an existing request."));
        }
    }

    /// <summary>
    /// TR: Pending provider hesabını Active duruma getirir ve tekrar çağrılarda mevcut final state'i döndürür.
    /// EN: Activates a Pending provider account and returns the existing final state on repeated calls.
    /// </summary>
    /// <param name="accountId">TR: Aktive edilecek provider hesap kimliği. EN: Provider account identifier to activate.</param>
    /// <returns>TR: Güncel provider hesap sonucunu ServiceResult içinde döndürür. EN: Returns current provider account result inside ServiceResult.</returns>
    [HttpPost("accounts/{accountId:guid}/activate")]
    [ProducesResponseType(typeof(ServiceResult<OpenAccountResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ServiceResult<OpenAccountResponse>), StatusCodes.Status404NotFound)]
    public ActionResult<ServiceResult<OpenAccountResponse>> ActivateAccount(Guid accountId)
    {
        try
        {
            var response = _providerService.ActivateAccount(accountId);
            return Ok(ServiceResult<OpenAccountResponse>.Success(
                response,
                "BANK_ACCOUNT_ACTIVE",
                "External bank account is active."));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(ServiceResult<OpenAccountResponse>.Failure(
                "BANK_ACCOUNT_NOT_FOUND",
                "External bank account was not found."));
        }
    }

    /// <summary>
    /// TR: Provider üzerinde Deposit veya Withdrawal başlatır; `X-Fake-Mode=pending` ise finansal etki finalize aşamasına ertelenir.
    /// EN: Starts a Deposit or Withdrawal at the provider and defers the financial effect until finalization when `X-Fake-Mode=pending` is supplied.
    /// </summary>
    /// <param name="request">TR: Account, amount, currency, transaction type ve provider idempotency key içeren money-movement request'i. EN: Money-movement request containing account, amount, currency, transaction type and provider idempotency key.</param>
    /// <param name="cancellationToken">TR: Fake delay/timeout simülasyonunun iptal sinyali. EN: Cancellation signal for fake delay/timeout simulation.</param>
    /// <returns>TR: Provider transaction sonucunu ServiceResult içinde döndürür. EN: Returns provider transaction result inside ServiceResult.</returns>
    [HttpPost("transactions")]
    [ProducesResponseType(typeof(ServiceResult<BankMoneyMovementResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ServiceResult<BankMoneyMovementResponse>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ServiceResult<BankMoneyMovementResponse>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ServiceResult<BankMoneyMovementResponse>), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ServiceResult<BankMoneyMovementResponse>), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<ServiceResult<BankMoneyMovementResponse>>> StartMoneyMovementAsync(
        [FromBody] BankMoneyMovementRequest request,
        CancellationToken cancellationToken)
    {
        if (IsFailMode())
        {
            return ProviderUnavailable<BankMoneyMovementResponse>();
        }

        await ApplyDelayModeAsync(cancellationToken);

        try
        {
            var response = _providerService.StartMoneyMovement(request, IsPendingMode());
            return Ok(ServiceResult<BankMoneyMovementResponse>.Success(
                response,
                "BANK_TRANSACTION_ACCEPTED",
                "External bank transaction request accepted."));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(ServiceResult<BankMoneyMovementResponse>.Failure(
                "BANK_ACCOUNT_NOT_FOUND",
                "External bank account was not found."));
        }
        catch (ArgumentException)
        {
            return UnprocessableEntity(ServiceResult<BankMoneyMovementResponse>.Failure(
                "INVALID_BANK_TRANSACTION_REQUEST",
                "The external bank transaction request is invalid."));
        }
        catch (InvalidOperationException)
        {
            return Conflict(ServiceResult<BankMoneyMovementResponse>.Failure(
                "BANK_TRANSACTION_CONFLICT",
                "The external bank transaction conflicts with provider state."));
        }
    }

    /// <summary>
    /// TR: Pending provider transaction'ı başarılı veya hatalı final state'e geçirir; tekrar çağrılarda finansal etkiyi ikinci kez uygulamaz.
    /// EN: Finalizes a Pending provider transaction as successful or failed and does not apply the financial effect twice on repeated calls.
    /// </summary>
    /// <param name="transactionId">TR: Finalize edilecek provider transaction kimliği. EN: Provider transaction identifier to finalize.</param>
    /// <param name="succeed">TR: True ise Completed, false ise Failed state hedeflenir. EN: Targets Completed when true and Failed when false.</param>
    /// <returns>TR: Güncel provider transaction sonucunu ServiceResult içinde döndürür. EN: Returns current provider transaction result inside ServiceResult.</returns>
    [HttpPost("transactions/{transactionId:guid}/finalize")]
    [ProducesResponseType(typeof(ServiceResult<BankMoneyMovementResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ServiceResult<BankMoneyMovementResponse>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ServiceResult<BankMoneyMovementResponse>), StatusCodes.Status409Conflict)]
    public ActionResult<ServiceResult<BankMoneyMovementResponse>> FinalizeTransaction(
        Guid transactionId,
        [FromQuery] bool succeed = true)
    {
        try
        {
            var response = _providerService.FinalizeTransaction(transactionId, succeed);
            return Ok(ServiceResult<BankMoneyMovementResponse>.Success(
                response,
                "BANK_TRANSACTION_FINALIZED",
                "External bank transaction finalized."));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(ServiceResult<BankMoneyMovementResponse>.Failure(
                "BANK_TRANSACTION_NOT_FOUND",
                "External bank transaction was not found."));
        }
        catch (InvalidOperationException)
        {
            return Conflict(ServiceResult<BankMoneyMovementResponse>.Failure(
                "BANK_TRANSACTION_FINALIZATION_CONFLICT",
                "External bank transaction could not be finalized in its current state."));
        }
    }

    /// <summary>
    /// TR: Provider transaction'ın güncel state ve account balance snapshot sonucunu kimlikle sorgular.
    /// EN: Queries current provider transaction state and account-balance snapshot by identifier.
    /// </summary>
    /// <param name="transactionId">TR: Sorgulanacak provider transaction kimliği. EN: Provider transaction identifier to query.</param>
    /// <returns>TR: Güncel provider transaction sonucunu ServiceResult içinde döndürür. EN: Returns current provider transaction result inside ServiceResult.</returns>
    [HttpGet("transactions/{transactionId:guid}")]
    [ProducesResponseType(typeof(ServiceResult<BankMoneyMovementResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ServiceResult<BankMoneyMovementResponse>), StatusCodes.Status404NotFound)]
    public ActionResult<ServiceResult<BankMoneyMovementResponse>> GetTransaction(Guid transactionId)
    {
        try
        {
            var response = _providerService.GetTransaction(transactionId);
            return Ok(ServiceResult<BankMoneyMovementResponse>.Success(
                response,
                "BANK_TRANSACTION_FOUND",
                "External bank transaction found."));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(ServiceResult<BankMoneyMovementResponse>.Failure(
                "BANK_TRANSACTION_NOT_FOUND",
                "External bank transaction was not found."));
        }
    }

    /// <summary>
    /// TR: Reconciliation için provider hesabının Completed transaction statement satırlarını kronolojik olarak döndürür.
    /// EN: Returns chronological Completed transaction statement items for provider-account reconciliation.
    /// </summary>
    /// <param name="accountId">TR: Statement sorgulanacak provider hesap kimliği. EN: Provider account identifier whose statement is queried.</param>
    /// <returns>TR: Provider statement satırlarını ServiceResult içinde döndürür. EN: Returns provider statement items inside ServiceResult.</returns>
    [HttpGet("accounts/{accountId:guid}/statement")]
    [ProducesResponseType(typeof(ServiceResult<IReadOnlyCollection<BankStatementItem>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ServiceResult<IReadOnlyCollection<BankStatementItem>>), StatusCodes.Status404NotFound)]
    public ActionResult<ServiceResult<IReadOnlyCollection<BankStatementItem>>> GetStatement(Guid accountId)
    {
        try
        {
            var response = _providerService.GetStatement(accountId);
            return Ok(ServiceResult<IReadOnlyCollection<BankStatementItem>>.Success(
                response,
                "BANK_STATEMENT_READY",
                "External bank statement generated."));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(ServiceResult<IReadOnlyCollection<BankStatementItem>>.Failure(
                "BANK_ACCOUNT_NOT_FOUND",
                "External bank account was not found."));
        }
    }

    /// <summary>
    /// TR: `X-Fake-Mode=fail` durumunda provider unavailable davranışının aktif olup olmadığını belirler.
    /// EN: Determines whether provider-unavailable behavior is active through `X-Fake-Mode=fail`.
    /// </summary>
    /// <returns>TR: Fail mode aktifse true döndürür. EN: Returns true when fail mode is active.</returns>
    private bool IsFailMode() => string.Equals(Request.Headers["X-Fake-Mode"].ToString(), "fail", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// TR: `X-Fake-Mode=pending` durumunda provider işleminin asynchronous Pending davranışını kullanması gerekip gerekmediğini belirler.
    /// EN: Determines whether the provider operation should use asynchronous Pending behavior through `X-Fake-Mode=pending`.
    /// </summary>
    /// <returns>TR: Pending mode aktifse true döndürür. EN: Returns true when pending mode is active.</returns>
    private bool IsPendingMode() => string.Equals(Request.Headers["X-Fake-Mode"].ToString(), "pending", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// TR: `delay` veya `timeout` fake-mode değerlerine göre deterministic bekleme davranışını uygular.
    /// EN: Applies deterministic wait behavior according to `delay` or `timeout` fake-mode values.
    /// </summary>
    /// <param name="cancellationToken">TR: Simüle edilen bekleme işleminin iptal sinyali. EN: Cancellation signal for the simulated wait.</param>
    private async Task ApplyDelayModeAsync(CancellationToken cancellationToken)
    {
        var fakeMode = Request.Headers["X-Fake-Mode"].ToString();
        if (string.Equals(fakeMode, "delay", StringComparison.OrdinalIgnoreCase))
        {
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
        }
        else if (string.Equals(fakeMode, "timeout", StringComparison.OrdinalIgnoreCase))
        {
            await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
        }
    }

    /// <summary>
    /// TR: FakeBank provider unavailable cevabını istenen response data tipi için standart ServiceResult biçiminde üretir.
    /// EN: Creates the FakeBank provider-unavailable response in standard ServiceResult form for the requested response-data type.
    /// </summary>
    /// <typeparam name="T">TR: Endpoint'in normal başarılı response data tipi. EN: Normal successful response-data type of the endpoint.</typeparam>
    /// <returns>TR: HTTP 503 ve FAKE_BANK_UNAVAILABLE hata kodunu taşıyan sonucu döndürür. EN: Returns HTTP 503 with the FAKE_BANK_UNAVAILABLE error code.</returns>
    private ObjectResult ProviderUnavailable<T>()
    {
        return StatusCode(
            StatusCodes.Status503ServiceUnavailable,
            ServiceResult<T>.Failure("FAKE_BANK_UNAVAILABLE", "Fake bank provider is unavailable."));
    }
}
