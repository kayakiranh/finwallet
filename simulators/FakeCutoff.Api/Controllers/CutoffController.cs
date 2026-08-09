using FakeCutoff.Api.Contracts;
using FakeCutoff.Api.Services;
using FinWallet.Shared.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace FakeCutoff.Api.Controllers;

/// <summary>
/// TR: Cutoff, çalışma günü ve settlement tarihi hesaplamasını controller tabanlı fake provider Web API'si olarak sunar.
/// EN: Exposes cutoff, business-day and settlement-date calculation as a controller-based fake-provider Web API.
/// </summary>
[ApiController]
[Route("api/v1/cutoffs")]
public sealed class CutoffController : ControllerBase
{
    private readonly CutoffCalendarService _cutoffService;

    /// <summary>
    /// TR: Cutoff hesaplama servisi bağımlılığıyla controller'ı oluşturur.
    /// EN: Creates the controller with its cutoff-calculation service dependency.
    /// </summary>
    /// <param name="cutoffService">TR: Deterministic fake cutoff/business-calendar hesaplama servisi. EN: Deterministic fake cutoff/business-calendar calculation service.</param>
    public CutoffController(CutoffCalendarService cutoffService)
    {
        _cutoffService = cutoffService ?? throw new ArgumentNullException(nameof(cutoffService));
    }

    /// <summary>
    /// TR: Ülke, currency, işlem tipi ve istek zamanına göre cutoff/processing/settlement kararını değerlendirir.
    /// EN: Evaluates cutoff, processing and settlement decisions using country, currency, transaction type and request time.
    /// </summary>
    /// <param name="request">TR: Cutoff değerlendirme request'i. EN: Cutoff-evaluation request.</param>
    /// <param name="cancellationToken">TR: Fake delay/timeout simülasyonunun iptal sinyali. EN: Cancellation signal for fake delay/timeout simulation.</param>
    /// <returns>TR: Provider cutoff kararını ServiceResult içinde döndürür. EN: Returns the provider cutoff decision inside ServiceResult.</returns>
    [HttpPost("evaluate")]
    [ProducesResponseType(typeof(ServiceResult<CutoffEvaluationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ServiceResult<CutoffEvaluationResponse>), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ServiceResult<CutoffEvaluationResponse>), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<ServiceResult<CutoffEvaluationResponse>>> EvaluateAsync(
        [FromBody] CutoffEvaluationRequest request,
        CancellationToken cancellationToken)
    {
        var fakeModeResult = await ApplyFakeModeAsync(cancellationToken);
        if (fakeModeResult is not null)
        {
            return fakeModeResult;
        }

        try
        {
            var response = _cutoffService.Evaluate(request);
            return Ok(ServiceResult<CutoffEvaluationResponse>.Success(
                response,
                "CUTOFF_EVALUATED",
                "Cutoff evaluation completed."));
        }
        catch (ArgumentException)
        {
            return UnprocessableEntity(ServiceResult<CutoffEvaluationResponse>.Failure(
                "CUTOFF_RULE_NOT_AVAILABLE",
                "A cutoff rule is not available for the supplied request."));
        }
    }

    /// <summary>
    /// TR: `X-Fake-Mode` header'ına göre fake provider fail, delay veya timeout davranışını uygular.
    /// EN: Applies fake-provider fail, delay or timeout behavior according to the `X-Fake-Mode` header.
    /// </summary>
    /// <param name="cancellationToken">TR: Simüle edilen bekleme işleminin iptal sinyali. EN: Cancellation signal for the simulated wait.</param>
    /// <returns>TR: Fail modunda 503 sonucu, diğer modlarda null döndürür. EN: Returns a 503 result in fail mode or null otherwise.</returns>
    private async Task<ActionResult<ServiceResult<CutoffEvaluationResponse>>?> ApplyFakeModeAsync(CancellationToken cancellationToken)
    {
        var fakeMode = Request.Headers["X-Fake-Mode"].ToString();
        if (string.Equals(fakeMode, "fail", StringComparison.OrdinalIgnoreCase))
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                ServiceResult<CutoffEvaluationResponse>.Failure(
                    "FAKE_CUTOFF_UNAVAILABLE",
                    "Fake cutoff provider is unavailable."));
        }

        if (string.Equals(fakeMode, "delay", StringComparison.OrdinalIgnoreCase))
        {
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
        }

        if (string.Equals(fakeMode, "timeout", StringComparison.OrdinalIgnoreCase))
        {
            await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
        }

        return null;
    }
}
