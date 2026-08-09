using System.Text;

namespace FinWallet.Api.Configuration;

internal sealed class InternalServiceHeaderHandler : DelegatingHandler
{
    private readonly string _serviceKey;

    public InternalServiceHeaderHandler(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var serviceKey = configuration["FinWallet:Gateway:InternalServiceKey"];
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceKey);
        if (Encoding.UTF8.GetByteCount(serviceKey) < 32)
        {
            throw new InvalidOperationException("Internal service key must contain at least 32 UTF-8 bytes.");
        }

        _serviceKey = serviceKey;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.Remove("X-Internal-Service-Key");
        request.Headers.TryAddWithoutValidation("X-Internal-Service-Key", _serviceKey);
        return base.SendAsync(request, cancellationToken);
    }
}
