using FinWallet.Application.Campaigns;
using FinWallet.Domain.Shared;

namespace FinWallet.Application.Purchases;

/// <summary>TR: Purchase için server-side wallet/merchant context'ini taşır. EN: Carries server-side wallet/merchant context for a purchase.</summary>
public sealed class PurchaseContext
{
    /// <summary>TR: Purchase context oluşturur. EN: Creates purchase context.</summary>
    /// <param name="walletId">TR: Source wallet kimliği. EN: Source wallet identifier.</param>
    /// <param name="currency">TR: Wallet currency'si. EN: Wallet currency.</param>
    /// <param name="merchantId">TR: Aktif merchant kimliği. EN: Active merchant identifier.</param>
    public PurchaseContext(Guid walletId, CurrencyCode currency, string merchantId)
    {
        if (walletId == Guid.Empty) throw new ArgumentException("Wallet identifier cannot be empty.", nameof(walletId));
        ArgumentException.ThrowIfNullOrWhiteSpace(merchantId);
        WalletId = walletId;
        Currency = currency;
        MerchantId = merchantId.Trim();
    }

    /// <summary>TR: Source wallet kimliğini döndürür. EN: Gets source-wallet identifier.</summary>
    public Guid WalletId { get; }
    /// <summary>TR: Wallet currency'sini döndürür. EN: Gets wallet currency.</summary>
    public CurrencyCode Currency { get; }
    /// <summary>TR: Merchant kimliğini döndürür. EN: Gets merchant identifier.</summary>
    public string MerchantId { get; }
}

/// <summary>TR: Purchase client command'ının durable idempotency fingerprint alanlarını taşır. EN: Carries durable-idempotency fingerprint fields of a purchase client command.</summary>
public sealed class PurchaseCommand
{
    /// <summary>TR: Purchase command oluşturur. EN: Creates purchase command.</summary>
    /// <param name="customerId">TR: Authenticated customer kimliği. EN: Authenticated customer identifier.</param>
    /// <param name="walletId">TR: Source wallet kimliği. EN: Source wallet identifier.</param>
    /// <param name="merchantId">TR: Merchant kimliği. EN: Merchant identifier.</param>
    /// <param name="originalAmount">TR: Kampanya öncesi pozitif tutar. EN: Positive amount before campaign.</param>
    /// <param name="idempotencyKey">TR: Durable idempotency anahtarı. EN: Durable-idempotency key.</param>
    /// <param name="correlationId">TR: Correlation kimliği. EN: Correlation identifier.</param>
    public PurchaseCommand(Guid customerId, Guid walletId, string merchantId, decimal originalAmount, string idempotencyKey, string correlationId)
    {
        if (customerId == Guid.Empty || walletId == Guid.Empty) throw new ArgumentException("Purchase identifiers cannot be empty.");
        ArgumentException.ThrowIfNullOrWhiteSpace(merchantId);
        if (originalAmount <= 0m) throw new ArgumentOutOfRangeException(nameof(originalAmount));
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        CustomerId = customerId;
        WalletId = walletId;
        MerchantId = merchantId.Trim();
        OriginalAmount = originalAmount;
        IdempotencyKey = idempotencyKey.Trim();
        CorrelationId = correlationId.Trim();
    }

    /// <summary>TR: Authenticated customer kimliğini döndürür. EN: Gets authenticated customer identifier.</summary>
    public Guid CustomerId { get; }
    /// <summary>TR: Source wallet kimliğini döndürür. EN: Gets source-wallet identifier.</summary>
    public Guid WalletId { get; }
    /// <summary>TR: Merchant kimliğini döndürür. EN: Gets merchant identifier.</summary>
    public string MerchantId { get; }
    /// <summary>TR: Kampanya öncesi tutarı döndürür. EN: Gets amount before campaign.</summary>
    public decimal OriginalAmount { get; }
    /// <summary>TR: Durable idempotency anahtarını döndürür. EN: Gets durable-idempotency key.</summary>
    public string IdempotencyKey { get; }
    /// <summary>TR: Correlation kimliğini döndürür. EN: Gets correlation identifier.</summary>
    public string CorrelationId { get; }
}

/// <summary>TR: Campaign değerlendirmesi tamamlanmış purchase posting request'ini MSSQL store'a taşır. EN: Carries a campaign-evaluated purchase posting request into the MSSQL store.</summary>
public sealed class PurchasePostingRequest
{
    /// <summary>TR: Purchase command ve campaign sonucundan posting request oluşturur. EN: Creates posting request from purchase command and campaign result.</summary>
    /// <param name="command">TR: Client purchase command. EN: Client purchase command.</param>
    /// <param name="campaign">TR: External campaign ACL sonucu. EN: External campaign ACL result.</param>
    public PurchasePostingRequest(PurchaseCommand command, CampaignEvaluationResult campaign)
    {
        Command = command ?? throw new ArgumentNullException(nameof(command));
        Campaign = campaign ?? throw new ArgumentNullException(nameof(campaign));
    }

    /// <summary>TR: Client purchase command'ını döndürür. EN: Gets client purchase command.</summary>
    public PurchaseCommand Command { get; }
    /// <summary>TR: Campaign değerlendirme sonucunu döndürür. EN: Gets campaign evaluation result.</summary>
    public CampaignEvaluationResult Campaign { get; }
}

/// <summary>TR: Completed purchase ve campaign accounting sonucunu taşır. EN: Carries completed purchase and campaign-accounting result.</summary>
public sealed class PurchaseResult
{
    /// <summary>TR: Completed purchase sonucu oluşturur. EN: Creates completed purchase result.</summary>
    /// <param name="transactionId">TR: FinancialTransaction kimliği. EN: FinancialTransaction identifier.</param>
    /// <param name="walletId">TR: Source wallet kimliği. EN: Source wallet identifier.</param>
    /// <param name="merchantId">TR: Merchant kimliği. EN: Merchant identifier.</param>
    /// <param name="originalAmount">TR: Kampanya öncesi tutar. EN: Amount before campaign.</param>
    /// <param name="discountAmount">TR: Campaign indirim tutarı. EN: Campaign discount amount.</param>
    /// <param name="finalAmount">TR: Customer wallet'tan düşülen tutar. EN: Amount debited from customer wallet.</param>
    /// <param name="currency">TR: Purchase currency'si. EN: Purchase currency.</param>
    /// <param name="campaignId">TR: Uygulanan campaign kimliği veya null. EN: Applied campaign identifier or null.</param>
    /// <param name="sponsor">TR: Campaign sponsor'u veya null. EN: Campaign sponsor or null.</param>
    /// <param name="completedAt">TR: Posting UTC zamanı. EN: Posting UTC timestamp.</param>
    /// <param name="wasReplay">TR: Durable replay bilgisidir. EN: Durable replay state.</param>
    public PurchaseResult(Guid transactionId, Guid walletId, string merchantId, decimal originalAmount, decimal discountAmount, decimal finalAmount, CurrencyCode currency, string? campaignId, CampaignSponsor? sponsor, DateTimeOffset completedAt, bool wasReplay)
    {
        TransactionId = transactionId; WalletId = walletId; MerchantId = merchantId; OriginalAmount = originalAmount; DiscountAmount = discountAmount; FinalAmount = finalAmount; Currency = currency; CampaignId = campaignId; Sponsor = sponsor; CompletedAt = completedAt; WasReplay = wasReplay;
    }

    /// <summary>TR: FinancialTransaction kimliğini döndürür. EN: Gets FinancialTransaction identifier.</summary>
    public Guid TransactionId { get; }
    /// <summary>TR: Source wallet kimliğini döndürür. EN: Gets source-wallet identifier.</summary>
    public Guid WalletId { get; }
    /// <summary>TR: Merchant kimliğini döndürür. EN: Gets merchant identifier.</summary>
    public string MerchantId { get; }
    /// <summary>TR: Orijinal tutarı döndürür. EN: Gets original amount.</summary>
    public decimal OriginalAmount { get; }
    /// <summary>TR: Discount tutarını döndürür. EN: Gets discount amount.</summary>
    public decimal DiscountAmount { get; }
    /// <summary>TR: Customer final tutarını döndürür. EN: Gets customer final amount.</summary>
    public decimal FinalAmount { get; }
    /// <summary>TR: Currency değerini döndürür. EN: Gets currency.</summary>
    public CurrencyCode Currency { get; }
    /// <summary>TR: Campaign kimliğini döndürür. EN: Gets campaign identifier.</summary>
    public string? CampaignId { get; }
    /// <summary>TR: Campaign sponsor bilgisini döndürür. EN: Gets campaign sponsor.</summary>
    public CampaignSponsor? Sponsor { get; }
    /// <summary>TR: Completion UTC zamanını döndürür. EN: Gets completion UTC timestamp.</summary>
    public DateTimeOffset CompletedAt { get; }
    /// <summary>TR: Durable replay bilgisini döndürür. EN: Gets durable replay state.</summary>
    public bool WasReplay { get; }
}

/// <summary>TR: Purchase persistence ve durable replay davranışını MSSQL implementasyonundan ayırır. EN: Decouples purchase persistence and durable replay behavior from the MSSQL implementation.</summary>
public interface IPurchaseStore
{
    /// <summary>TR: Customer-owned active wallet ve merchant context'ini yükler. EN: Loads customer-owned active wallet and merchant context.</summary>
    /// <param name="customerId">TR: Authenticated customer kimliği. EN: Authenticated customer identifier.</param>
    /// <param name="walletId">TR: Source wallet kimliği. EN: Source wallet identifier.</param>
    /// <param name="merchantId">TR: Merchant kimliği. EN: Merchant identifier.</param>
    /// <param name="cancellationToken">TR: SQL sorgu iptal sinyali. EN: SQL-query cancellation signal.</param>
    /// <returns>TR: Valid context veya null döndürür. EN: Returns valid context or null.</returns>
    Task<PurchaseContext?> FindContextAsync(Guid customerId, Guid walletId, string merchantId, CancellationToken cancellationToken);

    /// <summary>TR: Campaign çağrısından önce completed durable idempotency replay'i arar ve farklı payload key reuse'ını reddeder. EN: Looks for a completed durable-idempotency replay before campaign call and rejects key reuse with a different payload.</summary>
    /// <param name="command">TR: Client purchase command. EN: Client purchase command.</param>
    /// <param name="cancellationToken">TR: SQL sorgu iptal sinyali. EN: SQL-query cancellation signal.</param>
    /// <returns>TR: Completed replay veya null döndürür. EN: Returns completed replay or null.</returns>
    Task<PurchaseResult?> TryGetCompletedAsync(PurchaseCommand command, CancellationToken cancellationToken);

    /// <summary>TR: Wallet debit, purchase FinancialTransaction, campaign accounting ledger, idempotency ve outbox kayıtlarını tek SQL transaction içinde post eder. EN: Posts wallet debit, purchase FinancialTransaction, campaign-accounting ledger, idempotency and outbox records in one SQL transaction.</summary>
    /// <param name="request">TR: Campaign-evaluated posting request. EN: Campaign-evaluated posting request.</param>
    /// <param name="cancellationToken">TR: SQL transaction iptal sinyali. EN: SQL-transaction cancellation signal.</param>
    /// <returns>TR: Completed yeni veya replay purchase sonucunu döndürür. EN: Returns completed new or replayed purchase result.</returns>
    Task<PurchaseResult> PostAsync(PurchasePostingRequest request, CancellationToken cancellationToken);
}

/// <summary>TR: Purchase wallet veya merchant kullanılabilir olmadığında oluşur. EN: Raised when purchase wallet or merchant is unavailable.</summary>
public sealed class PurchaseUnavailableException : Exception
{
    /// <summary>TR: Purchase unavailable exception oluşturur. EN: Creates purchase-unavailable exception.</summary>
    public PurchaseUnavailableException() : base("The wallet or merchant is not available for purchase.") { }
}

/// <summary>TR: Purchase idempotency key farklı payload ile reuse edildiğinde oluşur. EN: Raised when a purchase idempotency key is reused with a different payload.</summary>
public sealed class PurchaseIdempotencyConflictException : Exception
{
    /// <summary>TR: Purchase idempotency conflict exception oluşturur. EN: Creates purchase-idempotency-conflict exception.</summary>
    public PurchaseIdempotencyConflictException() : base("The Idempotency-Key was already used with a different purchase request.") { }
}

/// <summary>TR: Completed replay kontrolü, campaign ACL çağrısı ve atomic purchase posting sırasını yönetir. EN: Coordinates completed replay check, campaign ACL call and atomic purchase posting in order.</summary>
public sealed class ExecutePurchaseHandler
{
    private readonly IPurchaseStore _store;
    private readonly ICampaignProvider _campaignProvider;
    private readonly TimeProvider _timeProvider;

    /// <summary>TR: Purchase store, campaign provider ve zaman kaynağıyla handler'ı oluşturur. EN: Creates the handler with purchase store, campaign provider and time source.</summary>
    /// <param name="store">TR: Purchase persistence sınırı. EN: Purchase persistence boundary.</param>
    /// <param name="campaignProvider">TR: Campaign ACL sınırı. EN: Campaign ACL boundary.</param>
    /// <param name="timeProvider">TR: Campaign request zaman kaynağı. EN: Campaign-request time source.</param>
    public ExecutePurchaseHandler(IPurchaseStore store, ICampaignProvider campaignProvider, TimeProvider timeProvider)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _campaignProvider = campaignProvider ?? throw new ArgumentNullException(nameof(campaignProvider));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <summary>TR: Aynı purchase tamamlandıysa campaign'i tekrar çağırmadan replay eder; aksi halde campaign kararını alıp atomic posting yapar. EN: Replays an already completed purchase without calling campaign again; otherwise evaluates campaign and performs atomic posting.</summary>
    /// <param name="command">TR: Authenticated purchase command. EN: Authenticated purchase command.</param>
    /// <param name="cancellationToken">TR: SQL/HTTP iptal sinyali. EN: SQL/HTTP cancellation signal.</param>
    /// <returns>TR: Completed purchase sonucunu döndürür. EN: Returns completed purchase result.</returns>
    public async Task<PurchaseResult> HandleAsync(PurchaseCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var replay = await _store.TryGetCompletedAsync(command, cancellationToken);
        if (replay is not null) return replay;
        var context = await _store.FindContextAsync(command.CustomerId, command.WalletId, command.MerchantId, cancellationToken) ?? throw new PurchaseUnavailableException();
        var campaign = await _campaignProvider.EvaluateAsync(
            command.CustomerId,
            context.MerchantId,
            new Money(command.OriginalAmount, context.Currency),
            _timeProvider.GetUtcNow(),
            command.CorrelationId,
            cancellationToken);
        if (campaign.Currency != context.Currency || campaign.OriginalAmount != command.OriginalAmount) throw new CampaignProviderException("CAMPAIGN_PROVIDER_INVALID_RESPONSE", "Campaign provider returned inconsistent purchase amounts or currency.");
        return await _store.PostAsync(new PurchasePostingRequest(command, campaign), cancellationToken);
    }
}
