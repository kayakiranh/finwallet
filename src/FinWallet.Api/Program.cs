using System.Text;
using FinWallet.Api.Endpoints;
using FinWallet.Api.Errors;
using FinWallet.Api.Middleware;
using FinWallet.Application.Authentication;
using FinWallet.Application.Communication;
using FinWallet.Application.Registration;
using FinWallet.Domain.Registration;
using FinWallet.Infrastructure.Authentication;
using FinWallet.Infrastructure.Communication;
using FinWallet.Infrastructure.Persistence.Redis;
using FinWallet.Infrastructure.Persistence.SqlServer;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

var sqlConnectionString = builder.Configuration["FinWallet:Sql:ConnectionString"];
ArgumentException.ThrowIfNullOrWhiteSpace(sqlConnectionString);

var redisConnectionString = builder.Configuration["FinWallet:Redis:ConnectionString"];
ArgumentException.ThrowIfNullOrWhiteSpace(redisConnectionString);

var registrationOtpPepper = builder.Configuration["FinWallet:Security:RegistrationOtpPepper"];
ArgumentException.ThrowIfNullOrWhiteSpace(registrationOtpPepper);

var jwtIssuer = builder.Configuration["FinWallet:Security:Jwt:Issuer"];
ArgumentException.ThrowIfNullOrWhiteSpace(jwtIssuer);

var jwtAudience = builder.Configuration["FinWallet:Security:Jwt:Audience"];
ArgumentException.ThrowIfNullOrWhiteSpace(jwtAudience);

var jwtSigningKey = builder.Configuration["FinWallet:Security:Jwt:SigningKey"];
ArgumentException.ThrowIfNullOrWhiteSpace(jwtSigningKey);

var fakeCommunicationBaseUrl = builder.Configuration["FinWallet:Integrations:FakeCommunication:BaseUrl"];
ArgumentException.ThrowIfNullOrWhiteSpace(fakeCommunicationBaseUrl);

var normalizedFakeCommunicationBaseUrl = fakeCommunicationBaseUrl.EndsWith('/', StringComparison.Ordinal)
    ? fakeCommunicationBaseUrl
    : $"{fakeCommunicationBaseUrl}/";
var fakeCommunicationBaseUri = new Uri(normalizedFakeCommunicationBaseUrl, UriKind.Absolute);

var sqlSettings = new SqlServerSettings(sqlConnectionString);
var otpSecuritySettings = new RegistrationOtpSecuritySettings(registrationOtpPepper);
var jwtSettings = new JwtTokenSettings(jwtIssuer, jwtAudience, jwtSigningKey);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(sqlSettings);
builder.Services.AddSingleton<SqlConnectionFactory>();
builder.Services.AddScoped<ICustomerRegistrationStore, SqlCustomerRegistrationStore>();
builder.Services.AddScoped<IAuthenticationStore, SqlAuthenticationStore>();

builder.Services.AddSingleton(otpSecuritySettings);
builder.Services.AddSingleton<IConnectionMultiplexer>(
    _ => ConnectionMultiplexer.Connect(redisConnectionString));
builder.Services.AddSingleton<IRegistrationOtpService, RedisRegistrationOtpService>();

builder.Services.AddSingleton<RegistrationCountryPolicy>();
builder.Services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
builder.Services.AddSingleton<IRefreshTokenGenerator, SecureRefreshTokenGenerator>();
builder.Services.AddSingleton(jwtSettings);
builder.Services.AddSingleton<IAccessTokenIssuer, JwtAccessTokenIssuer>();

builder.Services.AddScoped<RegisterCustomerHandler>();
builder.Services.AddScoped<ResendRegistrationOtpHandler>();
builder.Services.AddScoped<VerifyRegistrationOtpHandler>();
builder.Services.AddScoped<LoginCustomerHandler>();
builder.Services.AddScoped<RefreshSessionHandler>();

builder.Services.AddHttpClient<ICommunicationGateway, FakeCommunicationGateway>(client =>
{
    client.BaseAddress = fakeCommunicationBaseUri;
    client.Timeout = TimeSpan.FromSeconds(3);
});

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtSettings.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SigningKey)),
            ValidateLifetime = true,
            RequireExpirationTime = true,
            RequireSignedTokens = true,
            ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 },
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });
builder.Services.AddAuthorization();

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health/live", () => Results.Ok(new { status = "ok", service = "FinWallet.Api" }));
app.MapAuthenticationEndpoints();

app.Run();
