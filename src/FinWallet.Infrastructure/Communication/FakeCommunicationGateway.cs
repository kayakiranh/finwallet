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

    /// <summary>
    /// TR: FakeCommunication için önceden BaseAddress/timeout ayarlanmış HttpClient ile adapter'ı oluşturur.
    /// EN: Creates the adapter using an HttpClient preconfigured with the FakeCommunication BaseAddress and timeout.
    /// </summary>
    /// <param name="httpClient">
    /// TR: FakeCommunication endpoint'lerine ayrılmış HttpClient örneği.
    /// EN: HttpClient instance dedicated to FakeCommunication endpoints.
    /// </param>
    public FakeCommunicationGateway(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    /// <summary>
    /// TR: Registration OTP'sini provider'a özel DTO'ya çevirir, correlation header'ını taşır ve controller tabanlı FakeCommunication SMS endpoint'ine gönderir.
    /// EN: Converts a registration OTP into the provider-specific DTO, propagates the correlation header and sends it to the controller-based FakeCommunication SMS endpoint.
    /// </summary>
    /// <param name="normalizedPhoneNumber">
    /// TR: SMS'in gönderileceği normalize uluslararası telefon numarası.
    /// EN: Normalized international phone number to which the SMS is sent.
    /// </param>
    /// <param name="otpCode">
    /// TR: SMS gövdesine yerleştirilecek ham OTP kodu; loglanmamalıdır.
    /// EN: Raw OTP code inserted into the SMS body; it must not be logged.
    /// </param>
    /// <param name="correlationId">
    /// TR: FinWallet request'iyle provider çağrısını ilişkilendiren correlation kimliği.
    /// EN: Correlation identifier linking the FinWallet request to the provider call.
    /// </param>
    /// <param name="cancellationToken">
    /// TR: Dış HTTP çağrısının iptal sinyali.
    /// EN: Cancellation signal for the external HTTP call.
    /// </param>
    /// <exception cref="HttpRequestException">
    /// TR: Provider ağ hatası veya başarı dışı HTTP status döndürürse oluşur.
    /// EN: Thrown when a provider network error occurs or a non-success HTTP status is returned.
    /// </exception>
    public async Task SendRegistrationOtpAsync(
        string normalizedPhoneNumber,
        string otpCode,
        string correlationId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedPhoneNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(otpCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        var providerRequest = new FakeCommunicationSmsRequest(
            normalizedPhoneNumber,
            "RegistrationOtp",
            $"FinWallet verification code: {otpCode}",
            correlationId);

        using var request = new HttpRequestMessage(HttpMethod.Post, "api/v1/communication/sms")
        {
            Content = JsonContent.Create(providerRequest)
        };
        request.Headers.TryAddWithoutValidation("X-Correlation-Id", correlationId);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
