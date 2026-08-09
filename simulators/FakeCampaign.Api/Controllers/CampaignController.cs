using FakeCampaign.Api.Contracts;
using FakeCampaign.Api.Services;
using FinWallet.Shared.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace FakeCampaign.Api.Controllers;

/// <summary>
/// TR: Merchant kampanya uygunluğu ve indirim hesaplamasını controller tabanlı fake provider Web API'si olarak sunar.
/// EN: Exposes merchant-campaign eligibility and discount calculation as a controller-based fake-provider Web API.
/// </summary>
[ApiController]
[Route("api/v1/campaigns")]
public sealed class CampaignController : ControllerBase
{
    private readonly CampaignEvaluationService _campaignService;

    /// <summary>
    /// TR: Kampanya hesaplama servisi bağımlılığıyla controller'ı oluşturur.
    /// EN: Creates the controller with its campaign-evaluation service dependency.
    /// </summary>
    /// <param name="campaignService">TR: Deterministic fake kampanya değerlendirme servisi. EN: Deterministic fake campaign-evaluation service.</param>
    public CampaignController(CampaignEvaluationService campaignService)
    {
        _campaignService = campaignService ?? throw new ArgumentNullException(nameof(campaignService));
    }

    /// <summary>
    /// TR: Müşteri, merchant, tutar, currency ve zaman bilgisine göre kampanya uygunluğu ile indirimi değerlendirir.
    /// EN: Evaluates campaign eligibility and discount using customer, merchant, amount, currency and time information.
    /// </summary>
    /// <param name="request">TR: Kampanya değerlendirme request'i. EN: Campaign-evaluation request.</param>
    /// <param name="cancellationToken">TR: Fake delay/timeout simülasyonunun iptal sinyali. EN: Cancellation signal for fake delay/timeout simulation.</param>
    /// <returns>TR: Kampanya provider kararını ServiceResult içinde döndürür. EN: Returns the campaign-provider decision inside ServiceResult.</returns>
    [HttpPost("evaluate")]
    [ProducesResponseType(typeof(ServiceResult<CampaignEvaluationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ServiceResult<CampaignEvaluationResponse>), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ServiceResult<CampaignEvaluationResponse>), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<ServiceResult<CampaignEvaluationResponse>>> EvaluateAsync(
        [FromBody] CampaignEvaluationRequest request,
        CancellationToken cancellationToken)
    {
        var fakeModeResult = await ApplyFakeModeAsync(cancellationToken);
        if (fakeModeResult is not null)
        {
            return fakeModeResult;
        }

        try
        {
            var response = _campaignService.Evaluate(request);
            return Ok(ServiceResult<CampaignEvaluationResponse>.Success(
                response,
                "CAMPAIGN_EVALUATED",
                "Campaign evaluation completed."));
        }
        catch (ArgumentException)
        {
            return UnprocessableEntity(ServiceResult<CampaignEvaluationResponse>.Failure(
                "INVALID_CAMPAIGN_REQUEST",
                "The campaign evaluation request is invalid."));
        }
    }

    /// <summary>
    /// TR: `X-Fake-Mode` header'ına göre fake provider fail, delay veya timeout davranışını uygular.
    /// EN: Applies fake-provider fail, delay or timeout behavior according to the `X-Fake-Mode` header.
    /// </summary>
    /// <param name="cancellationToken">TR: Simüle edilen bekleme işleminin iptal sinyali. EN: Cancellation signal for the simulated wait.</param>
    /// <returns>TR: Fail modunda 503 sonucu, diğer modlarda null döndürür. EN: Returns a 503 result in fail mode or null otherwise.</returns>
    private async Task<ActionResult<ServiceResult<CampaignEvaluationResponse>>?> ApplyFakeModeAsync(CancellationToken cancellationToken)
    {
        var fakeMode = Request.Headers["X-Fake-Mode"].ToString();
        if (string.Equals(fakeMode, "fail", StringComparison.OrdinalIgnoreCase))
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                ServiceResult<CampaignEvaluationResponse>.Failure(
                    "FAKE_CAMPAIGN_UNAVAILABLE",
                    "Fake campaign provider is unavailable."));
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
