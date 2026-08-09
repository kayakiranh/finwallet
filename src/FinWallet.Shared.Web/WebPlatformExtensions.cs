using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using FinWallet.Shared.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FinWallet.Shared.Web;

/// <summary>
/// TR: FinWallet HTTP servisleri için Swagger, Kestrel sınırları, CORS, rate limiting ve ortak güvenlik header'larını tek yerde uygular.
/// EN: Applies Swagger, Kestrel limits, CORS, rate limiting and shared security headers in one place for FinWallet HTTP services.
/// </summary>
public static class WebPlatformExtensions
{
    private const string CorsPolicyName = "FinWalletCors";

    /// <summary>
    /// TR: Web host ve servis koleksiyonuna appsettings tabanlı platform güvenlik/performance ayarlarını ekler.
    /// EN: Adds appsettings-driven platform security/performance settings to the web host and service collection.
    /// </summary>
    /// <param name="builder">TR: Yapılandırılacak WebApplicationBuilder. EN: WebApplicationBuilder to configure.</param>
    /// <param name="serviceName">TR: Swagger başlığı ve telemetry için servis adı. EN: Service name used for Swagger title and telemetry.</param>
    /// <returns>TR: Zincirleme kullanım için aynı builder'ı döndürür. EN: Returns the same builder for chaining.</returns>
    public static WebApplicationBuilder AddFinWalletWebPlatform(this WebApplicationBuilder builder, string serviceName)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);

        var configuration = builder.Configuration;
        var maxRequestBodyBytes = ReadLong(configuration, "Platform:Http:MaxRequestBodyBytes", 1_048_576L, 1_024L, 52_428_800L);
        var maxConcurrentConnections = ReadLong(configuration, "Platform:Http:MaxConcurrentConnections", 2_000L, 10L, 1_000_000L);
        var requestHeadersTimeoutSeconds = ReadInt(configuration, "Platform:Http:RequestHeadersTimeoutSeconds", 15, 1, 300);
        var keepAliveSeconds = ReadInt(configuration, "Platform:Http:KeepAliveSeconds", 60, 5, 600);
        var maxRequestHeaderCount = ReadInt(configuration, "Platform:Http:MaxRequestHeaderCount", 64, 16, 256);
        var maxRequestHeadersTotalSize = ReadInt(configuration, "Platform:Http:MaxRequestHeadersTotalSizeBytes", 32_768, 8_192, 262_144);

        builder.WebHost.ConfigureKestrel(options =>
        {
            options.AddServerHeader = false;
            options.Limits.MaxRequestBodySize = maxRequestBodyBytes;
            options.Limits.MaxConcurrentConnections = maxConcurrentConnections;
            options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(requestHeadersTimeoutSeconds);
            options.Limits.KeepAliveTimeout = TimeSpan.FromSeconds(keepAliveSeconds);
            options.Limits.MaxRequestHeaderCount = maxRequestHeaderCount;
            options.Limits.MaxRequestHeadersTotalSize = maxRequestHeadersTotalSize;
        });

        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        var allowedOrigins = configuration.GetSection("Platform:Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
        builder.Services.AddCors(options =>
        {
            options.AddPolicy(CorsPolicyName, policy =>
            {
                if (allowedOrigins.Length == 0)
                {
                    return;
                }

                policy.WithOrigins(allowedOrigins)
                    .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS")
                    .WithHeaders("Authorization", "Content-Type", "Idempotency-Key", "X-Correlation-Id", "X-Internal-Service-Key")
                    .SetPreflightMaxAge(TimeSpan.FromHours(1));
            });
        });

        var permitLimit = ReadInt(configuration, "Platform:RateLimit:PermitLimit", 120, 1, 100_000);
        var windowSeconds = ReadInt(configuration, "Platform:RateLimit:WindowSeconds", 60, 1, 3_600);
        var queueLimit = ReadInt(configuration, "Platform:RateLimit:QueueLimit", 0, 0, 10_000);

        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                var partition = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                return RateLimitPartition.GetFixedWindowLimiter(
                    partition,
                    _ => new FixedWindowRateLimiterOptions
                    {
                        AutoReplenishment = true,
                        PermitLimit = permitLimit,
                        QueueLimit = queueLimit,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        Window = TimeSpan.FromSeconds(windowSeconds)
                    });
            });
            options.OnRejected = async (context, cancellationToken) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers["Retry-After"] = Math.Ceiling(retryAfter.TotalSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture);
                }

                await context.HttpContext.Response.WriteAsJsonAsync(
                    ServiceResult<object>.Failure("RATE_LIMITED", "Too many requests."),
                    cancellationToken);
            };
        });

        return builder;
    }

    /// <summary>
    /// TR: Ortak güvenlik middleware sırasını uygular; auth/authorization çağrıları bu metottan sonra servis Program.cs dosyasında eklenebilir.
    /// EN: Applies the shared security middleware order; authentication/authorization may be added afterwards by each service Program.cs.
    /// </summary>
    /// <param name="app">TR: Yapılandırılacak WebApplication. EN: WebApplication to configure.</param>
    /// <returns>TR: Zincirleme kullanım için aynı uygulamayı döndürür. EN: Returns the same application for chaining.</returns>
    public static WebApplication UseFinWalletWebPlatform(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var configuration = app.Configuration;
        var enableHsts = configuration.GetValue("Platform:Security:EnableHsts", app.Environment.IsProduction());
        var enableHttpsRedirection = configuration.GetValue("Platform:Security:EnableHttpsRedirection", false);
        var enableSwagger = configuration.GetValue("Platform:Swagger:Enabled", !app.Environment.IsProduction());
        var requireJsonForWrites = configuration.GetValue("Platform:Security:RequireJsonForWriteRequests", true);
        var requireInternalServiceKey = configuration.GetValue("Platform:Security:RequireInternalServiceKey", false);
        var internalServiceKey = configuration["Platform:Security:InternalServiceKey"];

        byte[]? expectedInternalKey = null;
        if (requireInternalServiceKey)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(internalServiceKey);
            if (Encoding.UTF8.GetByteCount(internalServiceKey) < 32)
            {
                throw new InvalidOperationException("Platform internal service key must contain at least 32 UTF-8 bytes.");
            }

            expectedInternalKey = Encoding.UTF8.GetBytes(internalServiceKey);
        }

        if (enableHsts)
        {
            app.UseHsts();
        }

        if (enableHttpsRedirection)
        {
            app.UseHttpsRedirection();
        }

        app.Use(async (context, next) =>
        {
            if (HttpMethods.IsTrace(context.Request.Method) || string.Equals(context.Request.Method, "CONNECT", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
                await context.Response.WriteAsJsonAsync(
                    ServiceResult<object>.Failure("METHOD_NOT_ALLOWED", "The HTTP method is not allowed."),
                    context.RequestAborted);
                return;
            }

            if (requireInternalServiceKey &&
                !context.Request.Path.StartsWithSegments("/health") &&
                !context.Request.Path.StartsWithSegments("/swagger") &&
                !HasValidInternalServiceKey(context, expectedInternalKey!))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(
                    ServiceResult<object>.Failure("INTERNAL_SERVICE_UNAUTHORIZED", "A valid internal service credential is required."),
                    context.RequestAborted);
                return;
            }

            if (requireJsonForWrites &&
                context.Request.ContentLength.GetValueOrDefault() > 0 &&
                (HttpMethods.IsPost(context.Request.Method) || HttpMethods.IsPut(context.Request.Method) || HttpMethods.IsPatch(context.Request.Method)) &&
                !(context.Request.ContentType?.StartsWith("application/json", StringComparison.OrdinalIgnoreCase) ?? false))
            {
                context.Response.StatusCode = StatusCodes.Status415UnsupportedMediaType;
                await context.Response.WriteAsJsonAsync(
                    ServiceResult<object>.Failure("UNSUPPORTED_MEDIA_TYPE", "JSON request content is required."),
                    context.RequestAborted);
                return;
            }

            var correlationId = ResolveCorrelationId(context.Request.Headers["X-Correlation-Id"].FirstOrDefault());
            context.TraceIdentifier = correlationId;
            context.Response.Headers["X-Correlation-Id"] = correlationId;
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            context.Response.Headers["X-Frame-Options"] = "DENY";
            context.Response.Headers["Referrer-Policy"] = "no-referrer";
            context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";
            context.Response.Headers["Cross-Origin-Resource-Policy"] = "same-origin";
            context.Response.Headers["X-Permitted-Cross-Domain-Policies"] = "none";
            context.Response.Headers["Cache-Control"] = "no-store, no-cache";
            context.Response.Headers["Pragma"] = "no-cache";

            if (context.Request.Path.StartsWithSegments("/swagger"))
            {
                context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; img-src 'self' data:; style-src 'self' 'unsafe-inline'; script-src 'self' 'unsafe-inline'; frame-ancestors 'none'; base-uri 'none';";
            }
            else
            {
                context.Response.Headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'none';";
            }

            await next();
        });

        app.UseCors(CorsPolicyName);
        app.UseRateLimiter();

        if (enableSwagger)
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        return app;
    }

    private static bool HasValidInternalServiceKey(HttpContext context, byte[] expectedKey)
    {
        var provided = context.Request.Headers["X-Internal-Service-Key"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(provided))
        {
            return false;
        }

        var providedBytes = Encoding.UTF8.GetBytes(provided);
        return providedBytes.Length == expectedKey.Length && CryptographicOperations.FixedTimeEquals(providedBytes, expectedKey);
    }

    private static int ReadInt(IConfiguration configuration, string key, int defaultValue, int min, int max)
    {
        var value = configuration.GetValue<int?>(key) ?? defaultValue;
        if (value < min || value > max)
        {
            throw new InvalidOperationException($"Configuration '{key}' must be between {min} and {max}.");
        }

        return value;
    }

    private static long ReadLong(IConfiguration configuration, string key, long defaultValue, long min, long max)
    {
        var value = configuration.GetValue<long?>(key) ?? defaultValue;
        if (value < min || value > max)
        {
            throw new InvalidOperationException($"Configuration '{key}' must be between {min} and {max}.");
        }

        return value;
    }

    private static string ResolveCorrelationId(string? candidate)
    {
        if (!string.IsNullOrWhiteSpace(candidate) && candidate.Length <= 64 && candidate.All(static character => char.IsLetterOrDigit(character) || character is '-' or '_'))
        {
            return candidate;
        }

        return Guid.NewGuid().ToString("N");
    }
}
