using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;

namespace FinWallet.Gateway.Security;

internal sealed class InternalServiceKeyRequirement : IAuthorizationRequirement;

internal sealed class InternalServiceKeyAuthorizationHandler : AuthorizationHandler<InternalServiceKeyRequirement>
{
    private readonly byte[] _expectedKey;

    public InternalServiceKeyAuthorizationHandler(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var configuredKey = configuration["Gateway:Security:InternalServiceKey"];
        ArgumentException.ThrowIfNullOrWhiteSpace(configuredKey);
        if (Encoding.UTF8.GetByteCount(configuredKey) < 32)
        {
            throw new InvalidOperationException("Gateway internal service key must contain at least 32 UTF-8 bytes.");
        }

        _expectedKey = Encoding.UTF8.GetBytes(configuredKey);
    }

    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, InternalServiceKeyRequirement requirement)
    {
        if (context.Resource is not HttpContext httpContext)
        {
            return Task.CompletedTask;
        }

        var providedValue = httpContext.Request.Headers["X-Internal-Service-Key"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(providedValue))
        {
            return Task.CompletedTask;
        }

        var providedKey = Encoding.UTF8.GetBytes(providedValue);
        if (providedKey.Length == _expectedKey.Length && CryptographicOperations.FixedTimeEquals(providedKey, _expectedKey))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
