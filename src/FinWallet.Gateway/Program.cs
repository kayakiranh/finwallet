using System.Text;
using FinWallet.Gateway.Security;
using FinWallet.Shared.Contracts;
using FinWallet.Shared.Web;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.AddFinWalletWebPlatform("FinWallet.Gateway");
builder.Services.AddControllers();

var jwtIssuer = builder.Configuration["FinWallet:Security:Jwt:Issuer"];
var jwtAudience = builder.Configuration["FinWallet:Security:Jwt:Audience"];
var jwtSigningKey = builder.Configuration["FinWallet:Security:Jwt:SigningKey"];
ArgumentException.ThrowIfNullOrWhiteSpace(jwtIssuer);
ArgumentException.ThrowIfNullOrWhiteSpace(jwtAudience);
ArgumentException.ThrowIfNullOrWhiteSpace(jwtSigningKey);
if (Encoding.UTF8.GetByteCount(jwtSigningKey) < 32)
{
    throw new InvalidOperationException("JWT signing key must contain at least 32 UTF-8 bytes.");
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
            ClockSkew = TimeSpan.FromSeconds(builder.Configuration.GetValue("Gateway:Security:JwtClockSkewSeconds", 30))
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
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

app.UseFinWalletWebPlatform();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapReverseProxy();

app.Run();
