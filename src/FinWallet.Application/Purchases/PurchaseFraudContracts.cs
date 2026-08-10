using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using FinWallet.Application.Fraud;
using FinWallet.Domain.Fraud;
using FinWallet.Domain.Shared;

namespace FinWallet.Application.Purchases;

/// <summary>TR: Purchase fraud değerlendirmesi için yalnız server-side MSSQL state'ten üretilen sinyalleri taşır. EN: Carries purchase-fraud signals derived exclusively from server-side MSSQL state.</summary>
public sealed class PurchaseRiskSignals
{
    /// <summary>TR: Purchase risk signal setini oluşturur. EN: Creates purchase-risk signal set.</summary>
    public PurchaseRiskSignals(CurrencyCode currency, string countryCode, string deviceReference, bool isNewDevice, int transactionCountLastFiveMinutes, decimal amountLastTwentyFourHours, bool isKnownMerchant)
    {
        Currency = currency; CountryCode = countryCode; DeviceReference = deviceReference; IsNewDevice = isNewDevice; TransactionCountLastFiveMinutes = transactionCountLastFiveMinutes; AmountLastTwentyFourHours = amountLastTwentyFourHours; IsKnownMerchant = isKnownMerchant;
    }

    /// <summary>TR: Purchase currency'sini döndürür. EN: Gets purchase currency.</summary>
    public CurrencyCode Currency { get; }
    /// <summary>TR: Customer ülke kodunu döndürür. EN: Gets customer country code.</summary>
    public string CountryCode { get; }
    /// <summary>TR: Raw DeviceId yerine SHA-256 device reference döndürür. EN: Gets SHA-256 device reference instead of raw DeviceId.</summary>
    public string DeviceReference { get; }
    /// <summary>TR: Device yeni ise true döndürür. EN: Gets true when device is new.</summary>
    public bool IsNewDevice { get; }
    /// <summary>TR: Son 5 dakikadaki completed purchase sayısını döndürür. EN: Gets completed-purchase count over the last five minutes.</summary>
    public int TransactionCountLastFiveMinutes { get; }
    /// <summary>TR: Son 24 saat same-currency customer purchase toplamını döndürür. EN: Gets same-currency customer-purchase total over the last 24 hours.</summary>
    public decimal AmountLastTwentyFourHours { get; }
    /// <summary>TR: Merchant daha önce successful purchase counterparty olduysa true döndürür. EN: Gets true when merchant was previously a successful purchase counterparty.</summary>
    public bool IsKnownMerchant { get; }
}

/// <summary>TR: Purchase fraud için session/device/country/velocity/amount/merchant familiarity sinyallerini MSSQL implementasyonundan ayırır. EN: Decouples purchase-fraud session/device/country/velocity/amount/merchant-familiarity signals from MSSQL implementation.</summary>
public interface IPurchaseRiskSignalStore
{
    /// <summary>TR: Authenticated session ve customer-owned wallet/merchant state'ini doğrulayıp fraud sinyallerini server-side üretir. EN: Validates authenticated session and customer-owned wallet/merchant state and derives fraud signals server-side.</summary>
    Task<PurchaseRiskSignals> GetAsync(Guid customerId, Guid sessionId, Guid walletId, string merchantId, DateTimeOffset evaluatedAt, CancellationToken cancellationToken);
}

/// <summary>TR: Purchase JWT session durable olarak geçersiz olduğunda oluşur. EN: Raised when the durable JWT session for a purchase is invalid.</summary>
public sealed class PurchaseSessionInvalidException : Exception
{
    /// <summary>TR: Purchase session invalid exception oluşturur. EN: Creates purchase-session-invalid exception.</summary>
    public PurchaseSessionInvalidException() : base("The purchase session is invalid or no longer active.") { }
}

/// <summary>TR: Purchase fraud kararı Deny olduğunda oluşur. EN: Raised when purchase fraud decision is Deny.</summary>
public sealed class PurchaseFraudDeniedException : Exception
{
    /// <summary>TR: Purchase fraud denied exception oluşturur. EN: Creates purchase-fraud-denied exception.</summary>
    public PurchaseFraudDeniedException() : base("The purchase was denied by fraud controls.") { }
}

/// <summary>TR: Purchase fraud kararı Review olduğunda para hareket etmeden oluşur. EN: Raised without moving money when purchase fraud decision is Review.</summary>
public sealed class PurchaseFraudReviewRequiredException : Exception
{
    /// <summary>TR: Purchase fraud review-required exception oluşturur. EN: Creates purchase-fraud-review-required exception.</summary>
    public PurchaseFraudReviewRequiredException() : base("The purchase requires manual fraud review.") { }
}

/// <summary>TR: Zorunlu external fraud dependency geçici olarak erişilemediğinde oluşur. EN: Raised when the required external fraud dependency is temporarily unavailable.</summary>
public sealed class PurchaseFraudUnavailableException : Exception
{
    /// <summary>TR: External fraud root cause ile safe purchase fraud unavailable exception oluşturur. EN: Creates safe purchase-fraud-unavailable exception with the external-fraud root cause.</summary>
    public PurchaseFraudUnavailableException(Exception innerException) : base("The purchase fraud dependency is temporarily unavailable.", innerException) { }
}

/// <summary>TR: Purchase completed replay → durable fraud replay → server-side internal/external fraud → existing purchase/campaign posting sırasını uygular. EN: Applies purchase completed replay → durable fraud replay → server-side internal/external fraud → existing purchase/campaign posting order.</summary>
public sealed class ExecuteFraudProtectedPurchaseHandler
{
    private const string FraudOperation = "Purchase";
    private readonly IPurchaseStore _purchaseStore;
    private readonly IPurchaseRiskSignalStore _riskSignalStore;
    private readonly IFraudEventStore _fraudEventStore;
    private readonly InternalFraudEngine _internalFraudEngine;
    private readonly IExternalFraudProvider _externalFraudProvider;
    private readonly FraudDecisionPolicy _decisionPolicy;
    private readonly ExecutePurchaseHandler _purchaseHandler;
    private readonly TimeProvider _timeProvider;

    /// <summary>TR: Purchase replay/risk/fraud/final-posting bağımlılıklarıyla handler'ı oluşturur. EN: Creates handler with purchase replay/risk/fraud/final-posting dependencies.</summary>
    public ExecuteFraudProtectedPurchaseHandler(IPurchaseStore purchaseStore, IPurchaseRiskSignalStore riskSignalStore, IFraudEventStore fraudEventStore, InternalFraudEngine internalFraudEngine, IExternalFraudProvider externalFraudProvider, FraudDecisionPolicy decisionPolicy, ExecutePurchaseHandler purchaseHandler, TimeProvider timeProvider)
    {
        _purchaseStore = purchaseStore ?? throw new ArgumentNullException(nameof(purchaseStore));
        _riskSignalStore = riskSignalStore ?? throw new ArgumentNullException(nameof(riskSignalStore));
        _fraudEventStore = fraudEventStore ?? throw new ArgumentNullException(nameof(fraudEventStore));
        _internalFraudEngine = internalFraudEngine ?? throw new ArgumentNullException(nameof(internalFraudEngine));
        _externalFraudProvider = externalFraudProvider ?? throw new ArgumentNullException(nameof(externalFraudProvider));
        _decisionPolicy = decisionPolicy ?? throw new ArgumentNullException(nameof(decisionPolicy));
        _purchaseHandler = purchaseHandler ?? throw new ArgumentNullException(nameof(purchaseHandler));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <summary>TR: Purchase request'i durable fraud sonucu olmadan financial posting'e sokmaz; manual approval sonrası aynı idempotency key ile devam eder. EN: Never sends a purchase request to financial posting without a durable fraud result; continues with the same idempotency key after manual approval.</summary>
    /// <param name="command">TR: Purchase financial command. EN: Purchase financial command.</param>
    /// <param name="sessionId">TR: JWT `sid` durable session kimliği. EN: Durable JWT `sid` session identifier.</param>
    /// <param name="cancellationToken">TR: SQL/HTTP iptal sinyali. EN: SQL/HTTP cancellation signal.</param>
    /// <returns>TR: Completed new/replay purchase sonucunu döndürür. EN: Returns completed new/replayed purchase result.</returns>
    public async Task<PurchaseResult> HandleAsync(PurchaseCommand command, Guid sessionId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (sessionId == Guid.Empty) throw new PurchaseSessionInvalidException();

        var completed = await _purchaseStore.TryGetCompletedAsync(command, cancellationToken);
        if (completed is not null) return completed;

        var requestHash = CreateRequestHash(command);
        var durable = await _fraudEventStore.FindAsync(FraudOperation, command.CustomerId, command.IdempotencyKey, requestHash, cancellationToken);
        if (durable is not null) return await ContinueAsync(durable, command, cancellationToken);

        var evaluatedAt = _timeProvider.GetUtcNow();
        var signals = await _riskSignalStore.GetAsync(command.CustomerId, sessionId, command.WalletId, command.MerchantId, evaluatedAt, cancellationToken);
        var amount = new Money(command.OriginalAmount, signals.Currency);
        var internalResult = _internalFraudEngine.Evaluate(new FraudAssessmentContext(
            Guid.NewGuid(),
            command.CustomerId,
            amount,
            new Money(signals.AmountLastTwentyFourHours, signals.Currency),
            signals.TransactionCountLastFiveMinutes,
            signals.IsNewDevice,
            signals.IsKnownMerchant));

        if (internalResult.Decision == FraudDecision.Deny)
        {
            await _fraudEventStore.SaveAsync(FraudOperation, command.CustomerId, command.IdempotencyKey, requestHash, internalResult.Decision, null, FraudDecision.Deny, internalResult.ReasonCodes, evaluatedAt, cancellationToken);
            throw new PurchaseFraudDeniedException();
        }

        ExternalFraudEvaluationResult externalResult;
        try
        {
            externalResult = await _externalFraudProvider.EvaluateAsync(new ExternalFraudEvaluationContext(
                Guid.NewGuid(),
                command.CustomerId,
                FraudOperation,
                amount.Amount,
                amount.Currency.ToString(),
                signals.CountryCode,
                signals.DeviceReference,
                signals.IsNewDevice,
                signals.TransactionCountLastFiveMinutes,
                signals.AmountLastTwentyFourHours,
                command.MerchantId,
                command.CorrelationId), cancellationToken);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new PurchaseFraudUnavailableException(exception);
        }
        catch (HttpRequestException exception)
        {
            throw new PurchaseFraudUnavailableException(exception);
        }
        catch (InvalidOperationException exception)
        {
            throw new PurchaseFraudUnavailableException(exception);
        }

        var finalDecision = _decisionPolicy.Combine(internalResult.Decision, externalResult.Decision);
        var reasons = internalResult.ReasonCodes.Concat(externalResult.ReasonCodes).Distinct(StringComparer.Ordinal).ToArray();
        var fraudEvent = await _fraudEventStore.SaveAsync(FraudOperation, command.CustomerId, command.IdempotencyKey, requestHash, internalResult.Decision, externalResult.Decision, finalDecision, reasons, evaluatedAt, cancellationToken);
        return await ContinueAsync(fraudEvent, command, cancellationToken);
    }

    private async Task<PurchaseResult> ContinueAsync(FraudEventRecord fraudEvent, PurchaseCommand command, CancellationToken cancellationToken)
    {
        if (fraudEvent.ReviewState == FraudReviewState.Pending) throw new PurchaseFraudReviewRequiredException();
        if (fraudEvent.ReviewState == FraudReviewState.Denied || fraudEvent.FinalDecision == FraudDecision.Deny) throw new PurchaseFraudDeniedException();
        if (fraudEvent.ReviewState == FraudReviewState.Approved || fraudEvent.FinalDecision == FraudDecision.Allow) return await _purchaseHandler.HandleAsync(command, cancellationToken);
        if (fraudEvent.FinalDecision == FraudDecision.Review) throw new PurchaseFraudReviewRequiredException();
        throw new InvalidOperationException("Unknown durable purchase fraud decision state.");
    }

    private static string CreateRequestHash(PurchaseCommand command)
    {
        var canonical = string.Join('|', command.WalletId.ToString("N"), command.MerchantId.ToUpperInvariant(), command.OriginalAmount.ToString("0.0000", CultureInfo.InvariantCulture));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}
