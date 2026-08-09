using FinWallet.Shared.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace FinWallet.Gateway.Controllers;

/// <summary>
/// TR: Gateway prosesinin liveness bilgisini ve istemcilerin gateway'e erişebildiğini doğrulayan küçük operasyon endpoint'ini sunar.
/// EN: Exposes a small operational endpoint that reports gateway liveness and confirms clients can reach the gateway process.
/// </summary>
[ApiController]
[Route("gateway")]
public sealed class GatewayController : ControllerBase
{
    /// <summary>
    /// TR: Gateway prosesinin çalışır durumda olduğunu bildirir.
    /// EN: Reports that the gateway process is alive.
    /// </summary>
    /// <returns>TR: Gateway liveness sonucunu döndürür. EN: Returns the gateway liveness result.</returns>
    [HttpGet("health/live")]
    [ProducesResponseType(typeof(ServiceResult<HealthResponse>), StatusCodes.Status200OK)]
    public ActionResult<ServiceResult<HealthResponse>> GetLive()
    {
        return Ok(ServiceResult<HealthResponse>.Success(
            new HealthResponse("FinWallet.Gateway", "ok"),
            "HEALTHY",
            "Gateway is live."));
    }
}
