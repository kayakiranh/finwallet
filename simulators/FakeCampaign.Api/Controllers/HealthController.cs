using FinWallet.Shared.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace FakeCampaign.Api.Controllers;

/// <summary>
/// TR: FakeCampaign servisinin liveness durumunu controller tabanlı Web API sözleşmesiyle sunar.
/// EN: Exposes FakeCampaign service liveness through the controller-based Web API contract.
/// </summary>
[ApiController]
[Route("health")]
public sealed class HealthController : ControllerBase
{
    /// <summary>
    /// TR: FakeCampaign prosesinin çalışır durumda olduğunu ServiceResult ile bildirir.
    /// EN: Reports that the FakeCampaign process is alive using ServiceResult.
    /// </summary>
    /// <returns>TR: Servis liveness bilgisini taşıyan başarılı sonucu döndürür. EN: Returns a successful result carrying service liveness information.</returns>
    [HttpGet("live")]
    [ProducesResponseType(typeof(ServiceResult<HealthResponse>), StatusCodes.Status200OK)]
    public ActionResult<ServiceResult<HealthResponse>> GetLive()
    {
        var data = new HealthResponse("FakeCampaign.Api", "ok");
        return Ok(ServiceResult<HealthResponse>.Success(data, "HEALTHY", "Service is live."));
    }
}
