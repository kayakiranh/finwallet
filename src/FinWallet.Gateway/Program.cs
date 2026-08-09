using System.Text;
using FinWallet.Gateway.Security;
using FinWallet.Shared.Contracts;
using FinWallet.Shared.Web;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using Yarp.ReverseProxy.Transforms;

var builder = WebApplication.CreateBuilder(args);

builder.AddFinWalletWebPlatform("FinWallet.Gateway");
builder.Services.AddControllers();

var jwtIssuer = GetRequired(builder.Configuration, "FinWallet:Security:Jwt:Issuer");
var jwtAudience = GetRequired(builder.Configuration, "FinWallet:Security:Jwt:Audience");
var jwtSigningKey = GetRequired(builder.Configuration, "FinWallet:Security:Jwt:SigningKey");
var downstreamServiceKey = GetRequired(builder.Configuration, "Gateway:Security:DownstreamServiceKey");
if (Encoding.UTF8.GetByteCount(jwtSigningKey) < 32)
{
    throw new InvalidOperationException("JWT signing key must contain at least 32 UTF-8 bytes.");
}
if (Encoding.UTF8.GetByteCount(downstreamServiceKey) < 32)
{
    throw new InvalidOperationException("Gateway downstream service key must contain at least 32 UTF-8 bytes.");
}

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSigningKey)),
            ValidateLifetime = true,
            RequireExpirationTime = true,
            RequireSignedTokens = true,
            ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 },
            ClockSkew = TimeSpan.FromSeconds(ReadBoundedInt(builder.Configuration, "Gateway:Security:JwtClockSkewSeconds", 30, 0, 120))
        };
        options.Events = new JwtBearerEvents
        {
            OnChallenge = async context =>
            {
                context.HandleResponse();
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(
                    ServiceResult<object>.Failure("GATEWAY_UNAUTHORIZED", "A valid access token is required by the gateway."),
                    context.HttpContext.RequestAborted);
            },
            OnForbidden = async context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(
                    ServiceResult<object>.Failure("GATEWAY_FORBIDDEN", "The gateway denied this request."),
                    context.HttpContext.RequestAborted);
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("GatewayAuthenticated", policy => policy.RequireAuthenticatedUser());
    options.AddPolicy("InternalService", policy => policy.AddRequirements(new InternalServiceKeyRequirement()));
});
builder.Services.AddSingleton<IAuthorizationHandler, InternalServiceKeyAuthorizationHandler>();

builder.Services
    .AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddTransforms(transformBuilderContext =>
    {
        transformBuilderContext.AddRequestHeader("X-Internal-Service-Key", downstreamServiceKey, append: false);
    });

var app = builder.Build();

app.UseFinWalletWebPlatform();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapReverseProxy();

app.Run();

static string GetRequired(IConfiguration configuration, string key)
{
    var value = configuration[key];
    ArgumentException.ThrowIfNullOrWhiteSpace(value, key);
    return value;
}

static int ReadBoundedInt(IConfiguration configuration, string key, int defaultValue, int min, int max)
{
    var value = configuration.GetValue<int?>(key) ?? defaultValue;
    if (value < min || value > max)
    {
        throw new InvalidOperationException($"Configuration '{key}' must be between {min} and {max}.");
    }

    return value;
}
