using FinWallet.Application.Banking;

namespace FinWallet.Api.BackgroundJobs;

/// <summary>
/// TR: Cutoff nedeniyle scheduled veya provider tarafında pending kalan banka hareketlerini kısa SQL transaction'ları ve dış HTTP çağrıları arasında güvenli biçimde ilerleten background worker'dır.
/// EN: Background worker that safely advances bank movements left scheduled by cutoff or pending at the provider while keeping SQL transactions short and external HTTP calls outside them.
/// </summary>
public sealed class BankMoneyMovementBackgroundService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(15);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<BankMoneyMovementBackgroundService> _logger;

    /// <summary>TR: Scoped financial dependencies, UTC zaman kaynağı ve güvenli logger ile worker'ı oluşturur. EN: Creates the worker with scoped financial dependencies, UTC time source and safe logger.</summary>
    /// <param name="scopeFactory">TR: Her poll iterasyonunda scoped store/processor oluşturan factory. EN: Factory creating scoped store/processor per polling iteration.</param>
    /// <param name="timeProvider">TR: Due değerlendirmesi için UTC zaman kaynağı. EN: UTC time source for due evaluation.</param>
    /// <param name="logger">TR: Hassas payload yazmayan structured logger. EN: Structured logger that does not write sensitive payloads.</param>
    public BankMoneyMovementBackgroundService(IServiceScopeFactory scopeFactory, TimeProvider timeProvider, ILogger<BankMoneyMovementBackgroundService> logger)
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
                await ProcessBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Bank movement background batch failed without exposing request payloads.");
            }

            await Task.Delay(PollInterval, _timeProvider, stoppingToken);
        }
    }

    /// <summary>TR: Zamanı gelmiş en fazla 25 durable banka hareketini yükler ve her birini bağımsız hata sınırı içinde işler. EN: Loads up to 25 due durable bank movements and processes each inside an independent failure boundary.</summary>
    /// <param name="cancellationToken">TR: Host shutdown iptal sinyali. EN: Host-shutdown cancellation signal.</param>
    private async Task ProcessBatchAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IBankMoneyMovementStore>();
        var processor = scope.ServiceProvider.GetRequiredService<BankMoneyMovementProcessor>();
        var due = await store.ListDueAsync(_timeProvider.GetUtcNow(), 25, cancellationToken);

        foreach (var movement in due)
        {
            try
            {
                var correlationId = $"bank-bg-{movement.TransactionId:N}";
                await processor.ProcessAsync(movement, correlationId, cancellationToken);
            }
            catch (ExternalBankProviderException exception)
            {
                _logger.LogWarning(
                    "Bank provider retry deferred. TransactionId={TransactionId} Code={Code} Retryable={Retryable}",
                    movement.TransactionId,
                    exception.Code,
                    exception.IsRetryable);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Bank movement processing failed. TransactionId={TransactionId}", movement.TransactionId);
            }
        }
    }
}
