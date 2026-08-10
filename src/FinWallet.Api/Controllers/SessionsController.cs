using FinWallet.Application.Authentication;
using FinWallet.Shared.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinWallet.Api.Controllers;

/// <summary>
/// TR: Authenticated müşteri session yaşam döngüsü işlemlerini controller tabanlı Web API üzerinden sunar.
/// EN: Exposes authenticated customer-session lifecycle operations through controller-based Web API.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/auth")]
public sealed class SessionsController : ControllerBase
{
    private readonly LogoutSessionHandler _logoutSessionHandler;

    /// <summary>TR: Logout use-case handler bağımlılığıyla controller'ı oluşturur. EN: Creates the controller with its logout use-case handler dependency.</summary>
    /// <param name="logoutSessionHandler">TR: Durable session revoke handler'ı. EN: Durable session-revocation handler.</param>
    public SessionsController(LogoutSessionHandler logoutSessionHandler)
    {
        _logoutSessionHandler = logoutSessionHandler ?? throw new ArgumentNullException(nameof(logoutSessionHandler));
    }

    /// <summary>TR: JWT içindeki mevcut `sid` session'ını durable olarak revoke eder. EN: Durably revokes the current `sid` session carried by the JWT.</summary>
    /// <param name="cancellationToken">TR: MSSQL revoke işleminin iptal sinyali. EN: Cancellation signal for the MSSQL revocation operation.</param>
    /// <returns>TR: Logout sonucunu ServiceResult içinde döndürür. EN: Returns the logout result inside ServiceResult.</returns>
    [HttpPost("logout")]
    [ProducesResponseType(typeof(ServiceResult<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ServiceResult<object>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ServiceResult<object>>> LogoutAsync(CancellationToken cancellationToken)
    {
        var session = User.FindFirst("sid")?.Value;
        if (!Guid.TryParseExact(session, "N", out var sessionId))
        {
            return Unauthorized(ServiceResult<object>.Failure(
                "INVALID_ACCESS_TOKEN",
                "The access token session identity is invalid."));
        }

        await _logoutSessionHandler.HandleAsync(sessionId, cancellationToken);
        return Ok(ServiceResult<object>.Success(null, "LOGGED_OUT", "The current session was revoked successfully."));
    }
}
