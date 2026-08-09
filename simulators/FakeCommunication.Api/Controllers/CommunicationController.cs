using System.Collections.Concurrent;
using FakeCommunication.Api.Contracts;
using FakeCommunication.Api.Models;
using FinWallet.Shared.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace FakeCommunication.Api.Controllers;

/// <summary>
/// TR: Fake SMS gönderim akışını controller tabanlı Web API üzerinden sunar ve test amaçlı hata/gecikme modlarını simüle eder.
/// EN: Exposes the fake SMS delivery flow through controller-based Web API and simulates failure/delay modes for testing.
/// </summary>
[ApiController]
[Route("api/v1/communication")]
public sealed class CommunicationController : ControllerBase
{
    private readonly ConcurrentDictionary<Guid, FakeMessageRecord> _messages;

    /// <summary>
    /// TR: Fake provider mesaj deposu bağımlılığıyla controller'ı oluşturur.
    /// EN: Creates the controller with the fake-provider message-store dependency.
    /// </summary>
    /// <param name="messages">TR: Test amacıyla kabul edilen mesajları process memory'de tutan thread-safe store. EN: Thread-safe in-memory store retaining accepted messages for testing.</param>
    public CommunicationController(ConcurrentDictionary<Guid, FakeMessageRecord> messages)
    {
        _messages = messages ?? throw new ArgumentNullException(nameof(messages));
    }

    /// <summary>
    /// TR: SMS isteğini doğrular, seçili fake-mode davranışını uygular ve kabul edilen mesajı simulator belleğine kaydeder.
    /// EN: Validates an SMS request, applies the selected fake-mode behavior and stores the accepted message in simulator memory.
    /// </summary>
    /// <param name="request">TR: Alıcı, mesaj tipi, hassas gövde ve correlation bilgisini taşıyan SMS isteği. EN: SMS request carrying recipient, message type, sensitive body and correlation information.</param>
    /// <param name="cancellationToken">TR: Gecikme/timeout simülasyonu sırasında request iptal sinyali. EN: Request cancellation signal used during delay/timeout simulation.</param>
    /// <returns>TR: Kabul edilen provider mesaj referansını ServiceResult içinde döndürür. EN: Returns the accepted provider-message reference inside ServiceResult.</returns>
    [HttpPost("sms")]
    [ProducesResponseType(typeof(ServiceResult<SendMessageResponse>), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ServiceResult<SendMessageResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ServiceResult<SendMessageResponse>), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<ServiceResult<SendMessageResponse>>> SendSmsAsync(
        [FromBody] SendSmsRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Recipient)
            || string.IsNullOrWhiteSpace(request.MessageType)
            || string.IsNullOrWhiteSpace(request.Body)
            || string.IsNullOrWhiteSpace(request.CorrelationId))
        {
            return BadRequest(ServiceResult<SendMessageResponse>.Failure(
                "INVALID_MESSAGE_REQUEST",
                "The message request is invalid."));
        }

        var fakeModeResult = await ApplyFakeModeAsync(cancellationToken);
        if (fakeModeResult is not null)
        {
            return fakeModeResult;
        }

        var messageId = Guid.NewGuid();
        var acceptedAt = DateTimeOffset.UtcNow;
        var record = new FakeMessageRecord(
            messageId,
            request.Recipient,
            request.MessageType,
            request.Body,
            request.CorrelationId,
            acceptedAt);

        _messages[messageId] = record;
        var response = new SendMessageResponse(messageId, "Accepted", acceptedAt);
        return StatusCode(
            StatusCodes.Status202Accepted,
            ServiceResult<SendMessageResponse>.Success(response, "MESSAGE_ACCEPTED", "Message accepted by fake provider."));
    }

    /// <summary>
    /// TR: `X-Fake-Mode` header'ına göre fail, delay veya timeout davranışını uygular.
    /// EN: Applies fail, delay or timeout behavior according to the `X-Fake-Mode` header.
    /// </summary>
    /// <param name="cancellationToken">TR: Simüle edilen bekleme işleminin iptal sinyali. EN: Cancellation signal for the simulated delay.</param>
    /// <returns>TR: Fail modunda 503 ServiceResult, diğer modlarda işleme devam etmek için null döndürür. EN: Returns a 503 ServiceResult in fail mode or null to continue processing in other modes.</returns>
    private async Task<ActionResult<ServiceResult<SendMessageResponse>>?> ApplyFakeModeAsync(CancellationToken cancellationToken)
    {
        var fakeMode = Request.Headers["X-Fake-Mode"].ToString();
        if (string.Equals(fakeMode, "fail", StringComparison.OrdinalIgnoreCase))
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                ServiceResult<SendMessageResponse>.Failure(
                    "FAKE_PROVIDER_UNAVAILABLE",
                    "Fake communication provider is unavailable."));
        }

        if (string.Equals(fakeMode, "delay", StringComparison.OrdinalIgnoreCase))
        {
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
        }

        if (string.Equals(fakeMode, "timeout", StringComparison.OrdinalIgnoreCase))
        {
            await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
        }

        return null;
    }
}
