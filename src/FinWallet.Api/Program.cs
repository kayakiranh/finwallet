using System.Text;
using FinWallet.Api.Configuration;
using FinWallet.Api.Errors;
using FinWallet.Application.Authentication;
using FinWallet.Application.Banking;
using FinWallet.Application.Communication;
using FinWallet.Application.Fraud;
using FinWallet.Application.Registration;
using FinWallet.Application.Transfers;
using FinWallet.Application.Wallets;
using FinWallet.Domain.Fraud;
using FinWallet.Domain.Fraud.Rules;
using FinWallet.Domain.Registration;
using FinWallet.Infrastructure.Authentication;
using FinWallet.Infrastructure.Banking;
using FinWallet.Infrastructure.Communication;
using FinWallet.Infrastructure.Fraud;
using FinWallet.Infrastructure.Persistence.Redis;
using FinWallet.Infrastructure.Persistence.SqlServer;
using FinWallet.Shared.Contracts;
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

var fakeCommunicationBaseUri = IntegrationUriFactory.CreateRequiredBaseUri(
    builder.Configuration["FinWallet:Integrations:FakeCommunication:BaseUrl"],
    "FinWallet:Integrations:FakeCommunication:BaseUrl");
var fakeFraudBaseUri = IntegrationUriFactory.CreateRequiredBaseUri(
    builder.Configuration["FinWallet:Integrations:FakeFraud:BaseUrl"],
    "FinWallet:Integrations:FakeFraud:BaseUrl");
var fakeBankBaseUri = IntegrationUriFactory.CreateRequiredBaseUri(
    builder.Configuration["FinWallet:Integrations:FakeBank:BaseUrl"],
    "FinWallet:Integrations:FakeBank:BaseUrl");

var sqlSettings = new SqlServerSettings(sqlConnectionString);
var otpSecuritySettings = new RegistrationOtpSecuritySettings(registrationOtpPepper);
var jwtSettings = new JwtTokenSettings(jwtIssuer, jwtAudience, jwtSigningKey);

builder.Services.AddControllers();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(sqlSettings);
builder.Services.AddSingleton<SqlConnectionFactory>();
builder.Services.AddScoped<ICustomerRegistrationStore, SqlCustomerRegistrationStore>();
builder.Services.AddScoped<IAuthenticationStore, SqlAuthenticationStore>();
builder.Services.AddScoped<IWalletStore, SqlWalletStore>();
builder.Services.AddScoped<IBankAccountStore, SqlBankAccountStore>();
builder.Services.AddScoped<IWalletTransferPostingStore, SqlWalletTransferPostingStore>();

builder.Services.AddSingleton(otpSecuritySettings);
builder.Services.AddSingleton<IConnectionMultiplexer>(
    _ => ConnectionMultiplexer.Connect(redisConnectionString));
builder.Services.AddSingleton<IRegistrationOtpService, RedisRegistrationOtpService>();

builder.Services.AddSingleton<RegistrationCountryPolicy>();
builder.Services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
builder.Services.AddSingleton<IRefreshTokenGenerator, SecureRefreshTokenGenerator>();
builder.Services.AddSingleton(jwtSettings);
builder.Services.AddSingleton<IAccessTokenIssuer, JwtAccessTokenIssuer>();

builder.Services.AddSingleton<IInternalFraudRule, TransactionAmountFraudRule>();
builder.Services.AddSingleton<IInternalFraudRule, DailyAmountFraudRule>();
builder.Services.AddSingleton<IInternalFraudRule, VelocityFraudRule>();
builder.Services.AddSingleton<IInternalFraudRule, NewDeviceBeneficiaryFraudRule>();
builder.Services.AddSingleton<InternalFraudEngine>();
builder.Services.AddSingleton<FraudDecisionPolicy>();

builder.Services.AddScoped<RegisterCustomerHandler>();
builder.Services.AddScoped<VerifyRegistrationOtpHandler>();
builder.Services.AddScoped<LoginCustomerHandler>();
builder.Services.AddScoped<RefreshSessionHandler>();
builder.Services.AddScoped<CreateWalletHandler>();
builder.Services.AddScoped<ListWalletsHandler>();
builder.Services.AddScoped<OpenBankAccountHandler>();

builder.Services.AddHttpClient<ICommunicationGateway, FakeCommunicationGateway>(client =>
{
    client.BaseAddress = fakeCommunicationBaseUri;
    client.Timeout = TimeSpan.FromSeconds(3);
});
builder.Services.AddHttpClient<IExternalFraudProvider, FakeFraudProvider>(client =>
{
    client.BaseAddress = fakeFraudBaseUri;
    client.Timeout = TimeSpan.FromSeconds(2);
});
builder.Services.AddHttpClient<IBankProvider, FakeBankProvider>(client =>
{
    client.BaseAddress = fakeBankBaseUri;
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
        options.Events = new JwtBearerEvents
        {
            OnChallenge = async context =>
            {
                context.HandleResponse();
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(
                    ServiceResult<object>.Failure(
                        "UNAUTHORIZED",
                        "A valid access token is required."),
                    context.HttpContext.RequestAborted);
            },
            OnForbidden = async context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(
                    ServiceResult<object>.Failure(
                        "FORBIDDEN",
                        "The authenticated customer is not allowed to perform this operation."),
                    context.HttpContext.RequestAborted);
            }
        };
    });
builder.Services.AddAuthorization();

var app = builder.Build();

app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
