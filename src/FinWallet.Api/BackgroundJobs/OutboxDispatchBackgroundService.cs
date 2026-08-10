using System.Text.Json;
using FinWallet.Application.Communication;
using FinWallet.Application.Outbox;

namespace FinWallet.Api.BackgroundJobs;

/// <summary>
/// TR: Finansal commit ile aynı MSSQL transaction'da yazılmış Outbox mesajlarını sonradan FakeCommunication'a gönderir; iletişim kesintisi finansal işlemi geri almaz ve mesajlar backoff ile yeniden denenir.
/// EN: Delivers Outbox messages written in the same MSSQL transaction as financial commits to FakeCommunication afterwards; communication outages never roll back money and messages are retried with backoff.
/// </summary>
public sealed class OutboxDispatchBackgroundService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ClaimLease = TimeSpan.FromMinutes(1);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<OutboxDispatchBackgroundService> _logger;

    /// <summary>TR: Scoped store/gateway factory, UTC zaman kaynağı ve güvenli logger ile worker'ı oluşturur. EN: Creates worker with scoped store/gateway factory, UTC time source and safe logger.</summary>
    /// <param name="scopeFactory">TR: Her batch için scoped dependency factory. EN: Scoped-dependency factory for each batch.</param>
    /// <param name="timeProvider">TR: Claim/backoff UTC zaman kaynağı. EN: UTC time source for claim/backoff.</param>
    /// <param name="logger">TR: Telefon, payload veya SMS body yazmayan logger. EN: Logger that never writes phone numbers, payloads or SMS bodies.</param>
    public OutboxDispatchBackgroundService(IServiceScopeFactory scopeFactory, TimeProvider timeProvider, ILogger<OutboxDispatchBackgroundService> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DispatchBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Outbox batch failed without exposing message payloads.");
            }

            await Task.Delay(PollInterval, _timeProvider, stoppingToken);
        }
    }

    /// <summary>TR: Pending Outbox kayıtlarını atomik claim eder ve her provider çağrısını SQL transaction dışında yürütür. EN: Atomically claims pending Outbox records and executes every provider call outside SQL transactions.</summary>
    /// <param name="cancellationToken">TR: Host shutdown iptal sinyali. EN: Host-shutdown cancellation signal.</param>
    private async Task DispatchBatchAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IOutboxStore>();
        var communication = scope.ServiceProvider.GetRequiredService<ICommunicationGateway>();
        var now = _timeProvider.GetUtcNow();
        var messages = await store.ClaimPendingAsync(now, now.Add(ClaimLease), 25, cancellationToken);

        foreach (var message in messages)
        {
            try
            {
                if (!TryReadCustomerId(message.PayloadJson, out var customerId))
                {
                    await store.RescheduleAsync(message.Id, NextAttempt(now, message.AttemptCount), "OUTBOX_INVALID_PAYLOAD", cancellationToken);
                    _logger.LogWarning("Outbox payload contract is invalid. MessageId={MessageId} Type={MessageType}", message.Id, message.MessageType);
                    continue;
                }

                var phone = await store.FindCustomerPhoneAsync(customerId, cancellationToken);
                if (string.IsNullOrWhiteSpace(phone))
                {
                    await store.RescheduleAsync(message.Id, NextAttempt(now, message.AttemptCount), "OUTBOX_RECIPIENT_UNAVAILABLE", cancellationToken);
                    _logger.LogWarning("Outbox recipient is unavailable. MessageId={MessageId} CustomerId={CustomerId}", message.Id, customerId);
                    continue;
                }

                var reference = message.AggregateId?.ToString("N") ?? message.Id.ToString("N");
                var body = $"FinWallet notification: {message.MessageType}. Reference: {reference}.";
                var correlationId = message.CorrelationId ?? $"outbox-{message.Id:N}";
                await communication.SendSmsAsync(phone, message.MessageType, body, correlationId, cancellationToken);
                await store.MarkProcessedAsync(message.Id, _timeProvider.GetUtcNow(), cancellationToken);
                _logger.LogInformation("Outbox message delivered. MessageId={MessageId} Type={MessageType} Attempt={Attempt}", message.Id, message.MessageType, message.AttemptCount);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (HttpRequestException)
            {
                await store.RescheduleAsync(message.Id, NextAttempt(_timeProvider.GetUtcNow(), message.AttemptCount), "COMMUNICATION_UNAVAILABLE", cancellationToken);
                _logger.LogWarning("Outbox communication delivery deferred. MessageId={MessageId} Type={MessageType} Attempt={Attempt}", message.Id, message.MessageType, message.AttemptCount);
            }
            catch (Exception exception)
            {
                await store.RescheduleAsync(message.Id, NextAttempt(_timeProvider.GetUtcNow(), message.AttemptCount), "OUTBOX_DISPATCH_ERROR", cancellationToken);
                _logger.LogError(exception, "Outbox delivery failed. MessageId={MessageId} Type={MessageType} Attempt={Attempt}", message.Id, message.MessageType, message.AttemptCount);
            }
        }
    }

    /// <summary>TR: Outbox JSON'dan yalnız CustomerId alanını parse eder; payload'un tamamını loglamaz. EN: Parses only CustomerId from Outbox JSON and never logs the whole payload.</summary>
    /// <param name="payloadJson">TR: Durable Outbox JSON. EN: Durable Outbox JSON.</param>
    /// <param name="customerId">TR: Bulunan CustomerId. EN: Parsed CustomerId.</param>
    /// <returns>TR: Geçerli CustomerId varsa true döndürür. EN: Returns true when a valid CustomerId exists.</returns>
    private static bool TryReadCustomerId(string payloadJson, out Guid customerId)
    {
        customerId = Guid.Empty;
        try
        {
            using var document = JsonDocument.Parse(payloadJson);
            return document.RootElement.TryGetProperty("CustomerId", out var property)
                && property.ValueKind == JsonValueKind.String
                && Guid.TryParse(property.GetString(), out customerId)
                && customerId != Guid.Empty;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>TR: Hızlı retry storm'u engellemek için deneme sayısına göre 10 saniyeden 15 dakikaya kadar sınırlı backoff hesaplar. EN: Calculates bounded backoff from 10 seconds to 15 minutes based on attempt count to prevent retry storms.</summary>
    /// <param name="now">TR: Mevcut UTC zaman. EN: Current UTC time.</param>
    /// <param name="attemptCount">TR: Mevcut attempt sayısı. EN: Current attempt count.</param>
    /// <returns>TR: Sonraki AvailableAt UTC değerini döndürür. EN: Returns next AvailableAt UTC value.</returns>
    private static DateTimeOffset NextAttempt(DateTimeOffset now, int attemptCount)
    {
        var exponent = Math.Clamp(attemptCount - 1, 0, 6);
        var seconds = Math.Min(900, 10 * (1 << exponent));
        return now.AddSeconds(seconds);
    }
}
