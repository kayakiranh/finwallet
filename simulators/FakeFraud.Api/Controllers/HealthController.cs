using FinWallet.Shared.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace FakeFraud.Api.Controllers;

/// <summary>
/// TR: FakeFraud servisinin liveness durumunu controller tabanlı Web API sözleşmesiyle sunar.
/// EN: Exposes FakeFraud service liveness through the controller-based Web API contract.
/// </summary>
[ApiController]
[Route("health")]
public sealed class HealthController : ControllerBase
{
    /// <summary>
    /// TR: FakeFraud prosesinin çalışır durumda olduğunu ServiceResult ile bildirir.
    /// EN: Reports that the FakeFraud process is alive using ServiceResult.
    /// </summary>
    /// <returns>TR: Servis liveness bilgisini taşıyan başarılı sonucu döndürür. EN: Returns a successful result carrying service liveness information.</returns>
    [HttpGet("live")]
    [ProducesResponseType(typeof(ServiceResult<HealthResponse>), StatusCodes.Status200OK)]
    public ActionResult<ServiceResult<HealthResponse>> GetLive()
    {
        var data = new HealthResponse("FakeFraud.Api", "ok");
        return Ok(ServiceResult<HealthResponse>.Success(data, "HEALTHY", "Service is live."));
    }
}
