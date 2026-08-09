namespace FinWallet.Api.Middleware;

/// <summary>
/// TR: Her HTTP request için güvenli bir correlation kimliği belirler, ASP.NET TraceIdentifier olarak kullanır ve aynı kimliği response header'ında döndürür.
/// EN: Establishes a safe correlation identifier for every HTTP request, uses it as the ASP.NET TraceIdentifier and returns the same identifier in the response header.
/// </summary>
public sealed class CorrelationIdMiddleware
{
    /// <summary>
    /// TR: Correlation kimliğinin taşındığı standart uygulama header adını tanımlar.
    /// EN: Defines the application header name used to carry the correlation identifier.
    /// </summary>
    private const string HeaderName = "X-Correlation-Id";

    /// <summary>
    /// TR: Header abuse ve gereksiz log/storage büyümesini önlemek için correlation kimliğinin maksimum uzunluğunu tanımlar.
    /// EN: Defines the maximum correlation-identifier length to prevent header abuse and unnecessary log/storage growth.
    /// </summary>
    private const int MaximumLength = 128;

    private readonly RequestDelegate _next;

    /// <summary>
    /// TR: Pipeline'daki bir sonraki middleware delegate'i ile correlation middleware'i oluşturur.
    /// EN: Creates the correlation middleware with the next middleware delegate in the pipeline.
    /// </summary>
    /// <param name="next">TR: Correlation bilgisi ayarlandıktan sonra çağrılacak sonraki request delegate. EN: Next request delegate invoked after correlation state is established.</param>
    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
    }

    /// <summary>
    /// TR: İstemcinin güvenli header değerini kullanır veya yeni GUID üretir, TraceIdentifier'ı günceller ve response header'ını ekler.
    /// EN: Uses a safe client header value or generates a new GUID, updates TraceIdentifier and adds the response header.
    /// </summary>
    /// <param name="context">TR: Correlation state'i uygulanacak HTTP context. EN: HTTP context to which correlation state is applied.</param>
    /// <returns>TR: Request pipeline tamamlandığında sona eren task döndürür. EN: Returns the task that completes when the request pipeline finishes.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var suppliedValue = context.Request.Headers[HeaderName].FirstOrDefault();
        var correlationId = IsSafe(suppliedValue)
            ? suppliedValue!
            : Guid.NewGuid().ToString("N");

        context.TraceIdentifier = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        await _next(context);
    }

    /// <summary>
    /// TR: Correlation header değerinin boş olmadığını, maksimum uzunluğu aşmadığını ve yalnızca güvenli ASCII identifier karakterleri içerdiğini doğrular.
    /// EN: Validates that the correlation header is non-empty, within the maximum length and contains only safe ASCII identifier characters.
    /// </summary>
    /// <param name="value">TR: İstemciden gelen correlation header değeri. EN: Correlation-header value supplied by the client.</param>
    /// <returns>TR: Değer güvenli identifier formatındaysa true döndürür. EN: Returns true when the value follows the safe identifier format.</returns>
    private static bool IsSafe(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > MaximumLength)
        {
            return false;
        }

        foreach (var character in value)
        {
            var valid = character is >= 'a' and <= 'z'
                or >= 'A' and <= 'Z'
                or >= '0' and <= '9'
                or '-' or '_' or '.' or ':';

            if (!valid)
            {
                return false;
            }
        }

        return true;
    }
}
