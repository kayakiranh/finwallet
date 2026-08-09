using FinWallet.Application.Fraud;
using FinWallet.Domain.Fraud;
using FinWallet.Domain.Shared;

namespace FinWallet.Application.Transfers;

/// <summary>
/// TR: Completed idempotency replay kontrolü, server-side risk sinyalleri, internal/external fraud kararı ve atomik financial posting sırasını yöneten wallet-transfer use-case handler'ıdır.
/// EN: Wallet-transfer use-case handler coordinating completed-idempotency replay checks, server-side risk signals, internal/external fraud decisions and atomic financial posting in the required order.
/// </summary>
public sealed class ExecuteWalletTransferHandler
{
    private readonly IWalletTransferReplayStore _replayStore;
    private readonly IWalletTransferRiskSignalStore _riskSignalStore;
    private readonly InternalFraudEngine _internalFraudEngine;
    private readonly IExternalFraudProvider _externalFraudProvider;
    private readonly FraudDecisionPolicy _fraudDecisionPolicy;
    private readonly IWalletTransferPostingStore _postingStore;
    private readonly TimeProvider _timeProvider;

    /// <summary>TR: Wallet-transfer orchestration bağımlılıklarıyla handler'ı oluşturur. EN: Creates the handler with wallet-transfer orchestration dependencies.</summary>
    /// <param name="replayStore">TR: Completed durable idempotency replay read boundary'si. EN: Read boundary for completed durable-idempotency replay.</param>
    /// <param name="riskSignalStore">TR: Server-side transfer risk signal read boundary'si. EN: Server-side transfer risk-signal read boundary.</param>
    /// <param name="internalFraudEngine">TR: FinWallet internal rule-based fraud engine'i. EN: FinWallet internal rule-based fraud engine.</param>
    /// <param name="externalFraudProvider">TR: FakeFraud/gerçek provider bağımsız external fraud boundary'si. EN: External fraud boundary independent from FakeFraud/real provider.</param>
    /// <param name="fraudDecisionPolicy">TR: Internal ve external fraud kararlarını birleştiren policy. EN: Policy combining internal and external fraud decisions.</param>
    /// <param name="postingStore">TR: Atomic MSSQL financial posting boundary'si. EN: Atomic MSSQL financial-posting boundary.</param>
    /// <param name="timeProvider">TR: Risk evaluation UTC zaman kaynağı. EN: UTC time source for risk evaluation.</param>
    public ExecuteWalletTransferHandler(
        IWalletTransferReplayStore replayStore,
        IWalletTransferRiskSignalStore riskSignalStore,
        InternalFraudEngine internalFraudEngine,
        IExternalFraudProvider externalFraudProvider,
        FraudDecisionPolicy fraudDecisionPolicy,
        IWalletTransferPostingStore postingStore,
        TimeProvider timeProvider)
    {
        _replayStore = replayStore ?? throw new ArgumentNullException(nameof(replayStore));
        _riskSignalStore = riskSignalStore ?? throw new ArgumentNullException(nameof(riskSignalStore));
        _internalFraudEngine = internalFraudEngine ?? throw new ArgumentNullException(nameof(internalFraudEngine));
        _externalFraudProvider = externalFraudProvider ?? throw new ArgumentNullException(nameof(externalFraudProvider));
        _fraudDecisionPolicy = fraudDecisionPolicy ?? throw new ArgumentNullException(nameof(fraudDecisionPolicy));
        _postingStore = postingStore ?? throw new ArgumentNullException(nameof(postingStore));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <summary>
    /// TR: Aynı request daha önce tamamlandıysa doğrudan replay eder; aksi halde server-side fraud preflight'i tamamlar ve yalnız Allow kararında atomic posting başlatır.
    /// EN: Directly replays an already completed identical request; otherwise completes server-side fraud preflight and starts atomic posting only when the combined decision is Allow.
    /// </summary>
    /// <param name="command">TR: Authenticated session transfer command'ı. EN: Authenticated-session transfer command.</param>
    /// <param name="cancellationToken">TR: SQL ve external fraud çağrılarına yayılan request iptal sinyali. EN: Request cancellation signal propagated to SQL and external-fraud calls.</param>
    /// <returns>TR: Yeni veya replay edilmiş Completed transfer sonucunu döndürür. EN: Returns newly completed or replayed Completed transfer result.</returns>
    public async Task<WalletTransferPostingResult> HandleAsync(
        ExecuteWalletTransferCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var replay = await _replayStore.TryGetCompletedAsync(command.PostingRequest, cancellationToken);
        if (replay is not null)
        {
            return replay;
        }

        var evaluatedAt = _timeProvider.GetUtcNow();
        var signals = await _riskSignalStore.GetAsync(
            command.PostingRequest.CustomerId,
            command.SessionId,
            command.PostingRequest.SourceWalletId,
            command.PostingRequest.DestinationWalletId,
            evaluatedAt,
            cancellationToken);

        var amount = new Money(command.PostingRequest.Amount, signals.Currency);
        var amountLastTwentyFourHours = new Money(signals.AmountLastTwentyFourHours, signals.Currency);
        var evaluationReference = Guid.NewGuid();
        var internalContext = new FraudAssessmentContext(
            evaluationReference,
            command.PostingRequest.CustomerId,
            amount,
            amountLastTwentyFourHours,
            signals.TransactionCountLastFiveMinutes,
            signals.IsNewDevice,
            signals.IsKnownBeneficiary);
        var internalDecision = _internalFraudEngine.Evaluate(internalContext);

        if (internalDecision == FraudDecision.Deny)
        {
            throw new WalletTransferFraudDeniedException();
        }

        ExternalFraudEvaluationResult externalResult;
        try
        {
            externalResult = await _externalFraudProvider.EvaluateAsync(
                new ExternalFraudEvaluationContext(
                    evaluationReference,
                    command.PostingRequest.CustomerId,
                    "WalletTransfer",
                    amount.Amount,
                    amount.Currency.ToString(),
                    signals.CountryCode,
                    signals.DeviceReference,
                    signals.IsNewDevice,
                    signals.TransactionCountLastFiveMinutes,
                    signals.AmountLastTwentyFourHours,
                    merchantId: null,
                    command.CorrelationId),
                cancellationToken);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new WalletTransferFraudUnavailableException(exception);
        }
        catch (HttpRequestException exception)
        {
            throw new WalletTransferFraudUnavailableException(exception);
        }
        catch (InvalidOperationException exception)
        {
            throw new WalletTransferFraudUnavailableException(exception);
        }

        var finalDecision = _fraudDecisionPolicy.Combine(internalDecision, externalResult.Decision);
        switch (finalDecision)
        {
            case FraudDecision.Deny:
                throw new WalletTransferFraudDeniedException();
            case FraudDecision.Review:
                throw new WalletTransferFraudReviewRequiredException();
            case FraudDecision.Allow:
                return await _postingStore.PostAsync(command.PostingRequest, cancellationToken);
            default:
                throw new InvalidOperationException("Unknown combined fraud decision.");
        }
    }
}
