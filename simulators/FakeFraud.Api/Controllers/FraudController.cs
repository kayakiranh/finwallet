using FakeFraud.Api.Contracts;
using FakeFraud.Api.Services;
using FinWallet.Shared.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace FakeFraud.Api.Controllers;

/// <summary>
/// TR: FakeFraud deterministic risk motorunu controller tabanlı Web API üzerinden dış provider davranışı olarak sunar.
/// EN: Exposes the deterministic FakeFraud risk engine as external-provider behavior through controller-based Web API.
/// </summary>
[ApiController]
[Route("api/v1/fraud")]
public sealed class FraudController : ControllerBase
{
    private readonly FraudEvaluationService _fraudEvaluationService;

    /// <summary>
    /// TR: Fraud değerlendirme servisi bağımlılığıyla controller'ı oluşturur.
    /// EN: Creates the controller with its fraud-evaluation service dependency.
    /// </summary>
    /// <param name="fraudEvaluationService">TR: PII içermeyen risk sinyallerini deterministic kurallarla değerlendiren provider servisi. EN: Provider service evaluating PII-free risk signals with deterministic rules.</param>
    public FraudController(FraudEvaluationService fraudEvaluationService)
    {
        _fraudEvaluationService = fraudEvaluationService ?? throw new ArgumentNullException(nameof(fraudEvaluationService));
    }

    /// <summary>
    /// TR: Finansal işlem risk sinyallerini değerlendirir ve provider referansı, Allow/Review/Deny kararı, skor ve reason code listesini döndürür.
    /// EN: Evaluates financial-transaction risk signals and returns provider reference, Allow/Review/Deny decision, score and reason codes.
    /// </summary>
    /// <param name="request">TR: PII içermeyen dış fraud değerlendirme request'i. EN: External fraud-evaluation request without PII.</param>
    /// <param name="cancellationToken">TR: Fake delay/timeout simülasyonu sırasında kullanılan request iptal sinyali. EN: Request cancellation signal used during fake delay/timeout simulation.</param>
    /// <returns>TR: Fraud provider kararını ServiceResult içinde döndürür. EN: Returns the fraud-provider decision inside ServiceResult.</returns>
    [HttpPost("evaluate")]
    [ProducesResponseType(typeof(ServiceResult<FraudEvaluationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ServiceResult<FraudEvaluationResponse>), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ServiceResult<FraudEvaluationResponse>), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<ServiceResult<FraudEvaluationResponse>>> EvaluateAsync(
        [FromBody] FraudEvaluationRequest request,
        CancellationToken cancellationToken)
    {
        var fakeModeResult = await ApplyFakeModeAsync(cancellationToken);
        if (fakeModeResult is not null)
        {
            return fakeModeResult;
        }

        try
        {
            var response = _fraudEvaluationService.Evaluate(request);
            return Ok(ServiceResult<FraudEvaluationResponse>.Success(
                response,
                "FRAUD_EVALUATED",
                "External fraud evaluation completed."));
        }
        catch (ArgumentException)
        {
            return UnprocessableEntity(ServiceResult<FraudEvaluationResponse>.Failure(
                "INVALID_FRAUD_REQUEST",
                "The external fraud evaluation request is invalid."));
        }
    }

    /// <summary>
    /// TR: `X-Fake-Mode` header'ına göre provider fail, delay veya timeout test davranışını uygular.
    /// EN: Applies provider fail, delay or timeout test behavior according to the `X-Fake-Mode` header.
    /// </summary>
    /// <param name="cancellationToken">TR: Simüle edilen gecikmenin iptal sinyali. EN: Cancellation signal for the simulated delay.</param>
    /// <returns>TR: Fail modunda 503 ServiceResult, diğer modlarda değerlendirmeye devam etmek için null döndürür. EN: Returns a 503 ServiceResult in fail mode or null to continue evaluation in other modes.</returns>
    private async Task<ActionResult<ServiceResult<FraudEvaluationResponse>>?> ApplyFakeModeAsync(CancellationToken cancellationToken)
    {
        var fakeMode = Request.Headers["X-Fake-Mode"].ToString();

        if (string.Equals(fakeMode, "fail", StringComparison.OrdinalIgnoreCase))
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                ServiceResult<FraudEvaluationResponse>.Failure(
                    "FAKE_FRAUD_UNAVAILABLE",
                    "Fake fraud provider is unavailable."));
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
