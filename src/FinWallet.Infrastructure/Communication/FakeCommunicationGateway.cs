using System.Net.Http.Json;
using FinWallet.Application.Communication;

namespace FinWallet.Infrastructure.Communication;

/// <summary>
/// TR: Application iletişim gateway sözleşmesini FakeCommunication HTTP API'ye uyarlayan adapter'dır; provider DTO ve endpoint detaylarını Infrastructure içinde sınırlar.
/// EN: Adapter that maps the Application communication-gateway contract to the FakeCommunication HTTP API while containing provider DTO and endpoint details inside Infrastructure.
/// </summary>
public sealed class FakeCommunicationGateway : ICommunicationGateway
{
    private readonly HttpClient _httpClient;

    /// <summary>TR: FakeCommunication için önceden BaseAddress/timeout ayarlanmış HttpClient ile adapter'ı oluşturur. EN: Creates the adapter using an HttpClient preconfigured with the FakeCommunication BaseAddress and timeout.</summary>
    /// <param name="httpClient">TR: FakeCommunication endpoint'lerine ayrılmış HttpClient örneği. EN: HttpClient instance dedicated to FakeCommunication endpoints.</param>
    public FakeCommunicationGateway(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    /// <inheritdoc />
    public Task SendRegistrationOtpAsync(string normalizedPhoneNumber, string otpCode, string correlationId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(otpCode);
        return SendSmsAsync(
            normalizedPhoneNumber,
            "RegistrationOtp",
            $"FinWallet verification code: {otpCode}",
            correlationId,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task SendSmsAsync(string normalizedPhoneNumber, string messageType, string body, string correlationId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedPhoneNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(messageType);
        ArgumentException.ThrowIfNullOrWhiteSpace(body);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        var providerRequest = new FakeCommunicationSmsRequest(
            normalizedPhoneNumber,
            messageType.Trim(),
            body,
            correlationId.Trim());

        using var request = new HttpRequestMessage(HttpMethod.Post, "api/v1/communication/sms")
        {
            Content = JsonContent.Create(providerRequest)
        };
        request.Headers.TryAddWithoutValidation("X-Correlation-Id", correlationId);

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
