using System.Net;
using System.Text;
using FinWallet.Api.BackgroundJobs;
using FinWallet.Api.Configuration;
using FinWallet.Api.Errors;
using FinWallet.Application.Authentication;
using FinWallet.Application.Banking;
using FinWallet.Application.Campaigns;
using FinWallet.Application.Communication;
using FinWallet.Application.Corrections;
using FinWallet.Application.Cutoff;
using FinWallet.Application.Fraud;
using FinWallet.Application.Purchases;
using FinWallet.Application.Registration;
using FinWallet.Application.Transfers;
using FinWallet.Application.Wallets;
using FinWallet.Domain.Fraud;
using FinWallet.Domain.Fraud.Rules;
using FinWallet.Domain.Registration;
using FinWallet.Infrastructure.Authentication;
using FinWallet.Infrastructure.Banking;
using FinWallet.Infrastructure.Campaigns;
using FinWallet.Infrastructure.Communication;
using FinWallet.Infrastructure.Cutoff;
using FinWallet.Infrastructure.Fraud;
using FinWallet.Infrastructure.Persistence.Redis;
using FinWallet.Infrastructure.Persistence.SqlServer;
using FinWallet.Shared.Contracts;
using FinWallet.Shared.Web;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options =>
{
    options.IncludeScopes = true;
    options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
    options.UseUtcTimestamp = true;
});

builder.AddFinWalletWebPlatform("FinWallet.Api");

var sqlConnectionString = GetRequired(builder.Configuration, "FinWallet:Sql:ConnectionString");
var redisConnectionString = GetRequired(builder.Configuration, "FinWallet:Redis:ConnectionString");
var registrationOtpPepper = GetRequired(builder.Configuration, "FinWallet:Security:RegistrationOtpPepper");
var jwtIssuer = GetRequired(builder.Configuration, "FinWallet:Security:Jwt:Issuer");
var jwtAudience = GetRequired(builder.Configuration, "FinWallet:Security:Jwt:Audience");
var jwtSigningKey = GetRequired(builder.Configuration, "FinWallet:Security:Jwt:SigningKey");
var jwtLifetimeMinutes = ReadBoundedInt(builder.Configuration, "FinWallet:Security:Jwt:AccessTokenLifetimeMinutes", 10, 2, 30);
var jwtClockSkewSeconds = ReadBoundedInt(builder.Configuration, "FinWallet:Security:Jwt:ClockSkewSeconds", 30, 0, 120);

var fakeCommunicationBaseUri = IntegrationUriFactory.CreateRequiredBaseUri(
    GetRequired(builder.Configuration, "FinWallet:Integrations:FakeCommunication:BaseUrl"),
    "FinWallet:Integrations:FakeCommunication:BaseUrl");
var fakeFraudBaseUri = IntegrationUriFactory.CreateRequiredBaseUri(
    GetRequired(builder.Configuration, "FinWallet:Integrations:FakeFraud:BaseUrl"),
    "FinWallet:Integrations:FakeFraud:BaseUrl");
var fakeBankBaseUri = IntegrationUriFactory.CreateRequiredBaseUri(
    GetRequired(builder.Configuration, "FinWallet:Integrations:FakeBank:BaseUrl"),
    "FinWallet:Integrations:FakeBank:BaseUrl");
var fakeCutoffBaseUri = IntegrationUriFactory.CreateRequiredBaseUri(
    GetRequired(builder.Configuration, "FinWallet:Integrations:FakeCutoff:BaseUrl"),
    "FinWallet:Integrations:FakeCutoff:BaseUrl");
var fakeCampaignBaseUri = IntegrationUriFactory.CreateRequiredBaseUri(
    GetRequired(builder.Configuration, "FinWallet:Integrations:FakeCampaign:BaseUrl"),
    "FinWallet:Integrations:FakeCampaign:BaseUrl");

var sqlSettings = new SqlServerSettings(sqlConnectionString);
var otpSecuritySettings = new RegistrationOtpSecuritySettings(registrationOtpPepper);
var jwtSettings = new JwtTokenSettings(jwtIssuer, jwtAudience, jwtSigningKey, jwtLifetimeMinutes);

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
builder.Services.AddScoped<IWalletTransferReplayStore, SqlWalletTransferReplayStore>();
builder.Services.AddScoped<IWalletTransferRiskSignalStore, SqlWalletTransferRiskSignalStore>();
builder.Services.AddScoped<IBankMoneyMovementStore, SqlBankMoneyMovementStore>();
builder.Services.AddScoped<IPurchaseStore, SqlPurchaseStore>();
builder.Services.AddScoped<ITransactionCorrectionStore, SqlTransactionCorrectionStore>();

builder.Services.AddSingleton(otpSecuritySettings);
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
{
    var options = ConfigurationOptions.Parse(redisConnectionString);
    options.AbortOnConnectFail = builder.Configuration.GetValue("FinWallet:Redis:AbortOnConnectFail", false);
    options.ConnectRetry = ReadBoundedInt(builder.Configuration, "FinWallet:Redis:ConnectRetry", 3, 0, 20);
    options.ConnectTimeout = ReadBoundedInt(builder.Configuration, "FinWallet:Redis:ConnectTimeoutMilliseconds", 3000, 250, 60_000);
    options.SyncTimeout = ReadBoundedInt(builder.Configuration, "FinWallet:Redis:SyncTimeoutMilliseconds", 3000, 250, 60_000);
    options.KeepAlive = ReadBoundedInt(builder.Configuration, "FinWallet:Redis:KeepAliveSeconds", 60, 10, 600);
    options.ClientName = "FinWallet.Api";
    return ConnectionMultiplexer.Connect(options);
});
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
builder.Services.AddScoped<LogoutSessionHandler>();
builder.Services.AddScoped<CreateWalletHandler>();
builder.Services.AddScoped<ListWalletsHandler>();
builder.Services.AddScoped<OpenBankAccountHandler>();
builder.Services.AddScoped<ExecuteWalletTransferHandler>();
builder.Services.AddScoped<BankMoneyMovementProcessor>();
builder.Services.AddScoped<ExecuteBankDepositHandler>();
builder.Services.AddScoped<ExecuteBankWithdrawalHandler>();
builder.Services.AddScoped<ExecutePurchaseHandler>();
builder.Services.AddScoped<ExecuteTransactionCorrectionHandler>();
builder.Services.AddHostedService<BankMoneyMovementBackgroundService>();

builder.Services.AddTransient<InternalServiceHeaderHandler>();

var pooledConnectionLifetimeMinutes = ReadBoundedInt(builder.Configuration, "FinWallet:HttpClient:PooledConnectionLifetimeMinutes", 5, 1, 60);
var pooledConnectionIdleMinutes = ReadBoundedInt(builder.Configuration, "FinWallet:HttpClient:PooledConnectionIdleMinutes", 2, 1, 30);
var maxConnectionsPerServer = ReadBoundedInt(builder.Configuration, "FinWallet:HttpClient:MaxConnectionsPerServer", 256, 8, 4096);

builder.Services
    .AddHttpClient<ICommunicationGateway, FakeCommunicationGateway>(client =>
    {
        client.BaseAddress = fakeCommunicationBaseUri;
        client.Timeout = TimeSpan.FromSeconds(ReadBoundedInt(builder.Configuration, "FinWallet:Integrations:FakeCommunication:TimeoutSeconds", 3, 1, 30));
    })
    .ConfigurePrimaryHttpMessageHandler(CreatePrimaryHandler)
    .AddHttpMessageHandler<InternalServiceHeaderHandler>();

builder.Services
    .AddHttpClient<IExternalFraudProvider, FakeFraudProvider>(client =>
    {
        client.BaseAddress = fakeFraudBaseUri;
        client.Timeout = TimeSpan.FromSeconds(ReadBoundedInt(builder.Configuration, "FinWallet:Integrations:FakeFraud:TimeoutSeconds", 2, 1, 30));
    })
    .ConfigurePrimaryHttpMessageHandler(CreatePrimaryHandler)
    .AddHttpMessageHandler<InternalServiceHeaderHandler>();

builder.Services
    .AddHttpClient<IBankProvider, FakeBankProvider>(client =>
    {
        client.BaseAddress = fakeBankBaseUri;
        client.Timeout = TimeSpan.FromSeconds(ReadBoundedInt(builder.Configuration, "FinWallet:Integrations:FakeBank:TimeoutSeconds", 3, 1, 60));
    })
    .ConfigurePrimaryHttpMessageHandler(CreatePrimaryHandler)
    .AddHttpMessageHandler<InternalServiceHeaderHandler>();

builder.Services
    .AddHttpClient<ICutoffProvider, FakeCutoffProvider>(client =>
    {
        client.BaseAddress = fakeCutoffBaseUri;
        client.Timeout = TimeSpan.FromSeconds(ReadBoundedInt(builder.Configuration, "FinWallet:Integrations:FakeCutoff:TimeoutSeconds", 3, 1, 30));
    })
    .ConfigurePrimaryHttpMessageHandler(CreatePrimaryHandler)
    .AddHttpMessageHandler<InternalServiceHeaderHandler>();

builder.Services
    .AddHttpClient<ICampaignProvider, FakeCampaignProvider>(client =>
    {
        client.BaseAddress = fakeCampaignBaseUri;
        client.Timeout = TimeSpan.FromSeconds(ReadBoundedInt(builder.Configuration, "FinWallet:Integrations:FakeCampaign:TimeoutSeconds", 3, 1, 30));
    })
    .ConfigurePrimaryHttpMessageHandler(CreatePrimaryHandler)
    .AddHttpMessageHandler<InternalServiceHeaderHandler>();

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
            ClockSkew = TimeSpan.FromSeconds(jwtClockSkewSeconds)
        };
        options.Events = new JwtBearerEvents
        {
            OnChallenge = async context =>
            {
                context.HandleResponse();
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(
                    ServiceResult<object>.Failure("UNAUTHORIZED", "A valid access token is required."),
                    context.HttpContext.RequestAborted);
            },
            OnForbidden = async context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(
                    ServiceResult<object>.Failure("FORBIDDEN", "The authenticated customer is not allowed to perform this operation."),
                    context.HttpContext.RequestAborted);
            }
        };
    });
builder.Services.AddAuthorization();

var app = builder.Build();

app.UseExceptionHandler();
app.UseFinWalletWebPlatform();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

SocketsHttpHandler CreatePrimaryHandler()
{
    return new SocketsHttpHandler
    {
        AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli,
        MaxConnectionsPerServer = maxConnectionsPerServer,
        PooledConnectionLifetime = TimeSpan.FromMinutes(pooledConnectionLifetimeMinutes),
        PooledConnectionIdleTimeout = TimeSpan.FromMinutes(pooledConnectionIdleMinutes),
        UseCookies = false
    };
}

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
