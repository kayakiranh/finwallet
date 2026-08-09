using FinWallet.Shared.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace FinWallet.Api.Controllers;

/// <summary>
/// TR: FinWallet ana API servisinin liveness durumunu controller tabanlı Web API sözleşmesiyle sunar.
/// EN: Exposes the liveness state of the main FinWallet API through the controller-based Web API contract.
/// </summary>
[ApiController]
[Route("health")]
public sealed class HealthController : ControllerBase
{
    /// <summary>
    /// TR: Servisin proses seviyesinde çalışır durumda olduğunu ServiceResult envelope'u ile bildirir.
    /// EN: Reports that the service is alive at process level using the ServiceResult envelope.
    /// </summary>
    /// <returns>TR: FinWallet.Api liveness bilgisini taşıyan başarılı sonucu döndürür. EN: Returns a successful result carrying FinWallet.Api liveness information.</returns>
    [HttpGet("live")]
    [ProducesResponseType(typeof(ServiceResult<HealthResponse>), StatusCodes.Status200OK)]
    public ActionResult<ServiceResult<HealthResponse>> GetLive()
    {
        var data = new HealthResponse("FinWallet.Api", "ok");
        return Ok(ServiceResult<HealthResponse>.Success(data, "HEALTHY", "Service is live."));
    }
}
