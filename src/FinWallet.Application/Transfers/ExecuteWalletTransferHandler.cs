using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using FinWallet.Application.Fraud;
using FinWallet.Domain.Fraud;
using FinWallet.Domain.Shared;

namespace FinWallet.Application.Transfers;

/// <summary>
/// TR: Completed idempotency replay kontrolü, durable FraudEvent/manual-review state'i, server-side risk sinyalleri, internal/external fraud kararı ve atomik financial posting sırasını yöneten wallet-transfer use-case handler'ıdır.
/// EN: Wallet-transfer use-case handler coordinating completed-idempotency replay, durable FraudEvent/manual-review state, server-side risk signals, internal/external fraud decisions and atomic financial posting in the required order.
/// </summary>
public sealed class ExecuteWalletTransferHandler
{
    private const string FraudOperation = "WalletTransfer";
    private readonly IWalletTransferReplayStore _replayStore;
    private readonly IWalletTransferRiskSignalStore _riskSignalStore;
    private readonly IFraudEventStore _fraudEventStore;
    private readonly InternalFraudEngine _internalFraudEngine;
    private readonly IExternalFraudProvider _externalFraudProvider;
    private readonly FraudDecisionPolicy _fraudDecisionPolicy;
    private readonly IWalletTransferPostingStore _postingStore;
    private readonly TimeProvider _timeProvider;

    /// <summary>TR: Wallet-transfer orchestration bağımlılıklarıyla handler'ı oluşturur. EN: Creates the handler with wallet-transfer orchestration dependencies.</summary>
    /// <param name="replayStore">TR: Completed durable idempotency replay read boundary'si. EN: Read boundary for completed durable-idempotency replay.</param>
    /// <param name="riskSignalStore">TR: Server-side transfer risk signal read boundary'si. EN: Server-side transfer risk-signal read boundary.</param>
    /// <param name="fraudEventStore">TR: Fraud karar/review state'ini durable saklayan boundary. EN: Boundary durably storing fraud-decision/review state.</param>
    /// <param name="internalFraudEngine">TR: FinWallet internal rule-based fraud engine'i. EN: FinWallet internal rule-based fraud engine.</param>
    /// <param name="externalFraudProvider">TR: FakeFraud/gerçek provider bağımsız external fraud boundary'si. EN: External fraud boundary independent from FakeFraud/real provider.</param>
    /// <param name="fraudDecisionPolicy">TR: Internal ve external fraud kararlarını birleştiren policy. EN: Policy combining internal and external fraud decisions.</param>
    /// <param name="postingStore">TR: Atomic MSSQL financial posting boundary'si. EN: Atomic MSSQL financial-posting boundary.</param>
    /// <param name="timeProvider">TR: Risk evaluation UTC zaman kaynağı. EN: UTC time source for risk evaluation.</param>
    public ExecuteWalletTransferHandler(
        IWalletTransferReplayStore replayStore,
        IWalletTransferRiskSignalStore riskSignalStore,
        IFraudEventStore fraudEventStore,
        InternalFraudEngine internalFraudEngine,
        IExternalFraudProvider externalFraudProvider,
        FraudDecisionPolicy fraudDecisionPolicy,
        IWalletTransferPostingStore postingStore,
        TimeProvider timeProvider)
    {
        _replayStore = replayStore ?? throw new ArgumentNullException(nameof(replayStore));
        _riskSignalStore = riskSignalStore ?? throw new ArgumentNullException(nameof(riskSignalStore));
        _fraudEventStore = fraudEventStore ?? throw new ArgumentNullException(nameof(fraudEventStore));
        _internalFraudEngine = internalFraudEngine ?? throw new ArgumentNullException(nameof(internalFraudEngine));
        _externalFraudProvider = externalFraudProvider ?? throw new ArgumentNullException(nameof(externalFraudProvider));
        _fraudDecisionPolicy = fraudDecisionPolicy ?? throw new ArgumentNullException(nameof(fraudDecisionPolicy));
        _postingStore = postingStore ?? throw new ArgumentNullException(nameof(postingStore));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <summary>
    /// TR: Aynı request daha önce tamamlandıysa doğrudan replay eder; durable fraud sonucu varsa provider'ı tekrar çağırmadan uygular; aksi halde fraud preflight'i kaydeder ve yalnız Allow/Approved kararında atomic posting başlatır.
    /// EN: Directly replays an already completed request; applies an existing durable fraud result without calling the provider again; otherwise records fraud preflight and starts atomic posting only after Allow/Approved.
    /// </summary>
    /// <param name="command">TR: Authenticated session transfer command'ı. EN: Authenticated-session transfer command.</param>
    /// <param name="cancellationToken">TR: SQL ve external fraud çağrılarına yayılan request iptal sinyali. EN: Request cancellation signal propagated to SQL and external-fraud calls.</param>
    /// <returns>TR: Yeni veya replay edilmiş Completed transfer sonucunu döndürür. EN: Returns newly completed or replayed Completed transfer result.</returns>
    public async Task<WalletTransferPostingResult> HandleAsync(ExecuteWalletTransferCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var replay = await _replayStore.TryGetCompletedAsync(command.PostingRequest, cancellationToken);
        if (replay is not null) return replay;

        var requestHash = CreateFraudRequestHash(command.PostingRequest);
        var durableFraud = await _fraudEventStore.FindAsync(
            FraudOperation,
            command.PostingRequest.CustomerId,
            command.PostingRequest.IdempotencyKey,
            requestHash,
            cancellationToken);
        if (durableFraud is not null)
        {
            return await ContinueFromDurableFraudAsync(durableFraud, command.PostingRequest, cancellationToken);
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
        var internalResult = _internalFraudEngine.Evaluate(internalContext);

        if (internalResult.Decision == FraudDecision.Deny)
        {
            await _fraudEventStore.SaveAsync(
                FraudOperation,
                command.PostingRequest.CustomerId,
                command.PostingRequest.IdempotencyKey,
                requestHash,
                internalResult.Decision,
                externalDecision: null,
                FraudDecision.Deny,
                internalResult.ReasonCodes,
                evaluatedAt,
                cancellationToken);
            throw new WalletTransferFraudDeniedException();
        }

        ExternalFraudEvaluationResult externalResult;
        try
        {
            externalResult = await _externalFraudProvider.EvaluateAsync(
                new ExternalFraudEvaluationContext(
                    evaluationReference,
                    command.PostingRequest.CustomerId,
                    FraudOperation,
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

        var finalDecision = _fraudDecisionPolicy.Combine(internalResult.Decision, externalResult.Decision);
        var reasons = internalResult.ReasonCodes.Concat(externalResult.ReasonCodes).Distinct(StringComparer.Ordinal).ToArray();
        var fraudEvent = await _fraudEventStore.SaveAsync(
            FraudOperation,
            command.PostingRequest.CustomerId,
            command.PostingRequest.IdempotencyKey,
            requestHash,
            internalResult.Decision,
            externalResult.Decision,
            finalDecision,
            reasons,
            evaluatedAt,
            cancellationToken);

        return await ContinueFromDurableFraudAsync(fraudEvent, command.PostingRequest, cancellationToken);
    }

    private async Task<WalletTransferPostingResult> ContinueFromDurableFraudAsync(FraudEventRecord fraudEvent, WalletTransferPostingRequest request, CancellationToken cancellationToken)
    {
        if (fraudEvent.ReviewState == FraudReviewState.Pending) throw new WalletTransferFraudReviewRequiredException();
        if (fraudEvent.ReviewState == FraudReviewState.Denied || fraudEvent.FinalDecision == FraudDecision.Deny) throw new WalletTransferFraudDeniedException();
        if (fraudEvent.ReviewState == FraudReviewState.Approved || fraudEvent.FinalDecision == FraudDecision.Allow)
        {
            return await _postingStore.PostAsync(request, cancellationToken);
        }
        if (fraudEvent.FinalDecision == FraudDecision.Review) throw new WalletTransferFraudReviewRequiredException();
        throw new InvalidOperationException("Unknown durable fraud decision state.");
    }

    private static string CreateFraudRequestHash(WalletTransferPostingRequest request)
    {
        var canonical = string.Join('|', request.SourceWalletId.ToString("N"), request.DestinationWalletId.ToString("N"), request.Amount.ToString("0.0000", CultureInfo.InvariantCulture));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}
