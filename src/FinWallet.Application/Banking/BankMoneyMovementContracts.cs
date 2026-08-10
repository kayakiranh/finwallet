using FinWallet.Domain.Shared;

namespace FinWallet.Application.Banking;

/// <summary>TR: FinWallet içindeki banka deposit/withdrawal lifecycle sonucunu ifade eder. EN: Represents the FinWallet lifecycle result of a bank deposit/withdrawal.</summary>
public enum BankMoneyMovementState
{
    /// <summary>TR: Provider tamamlanması bekleniyor. EN: Provider completion is pending.</summary>
    Pending = 1,
    /// <summary>TR: Cutoff processing tarihine kadar planlandı. EN: Scheduled until its cutoff processing date.</summary>
    Scheduled = 2,
    /// <summary>TR: Finansal hareket ve ledger posting tamamlandı. EN: Financial movement and ledger posting completed.</summary>
    Completed = 3,
    /// <summary>TR: İşlem başarısız oldu ve varsa bloke fon serbest bırakıldı. EN: Operation failed and any reserved funds were released.</summary>
    Failed = 4
}

/// <summary>TR: Authenticated bank-account ve customer context'ini server-side persistence'tan taşır. EN: Carries authenticated bank-account and customer context from server-side persistence.</summary>
public sealed class BankMoneyMovementContext
{
    /// <summary>TR: Context nesnesini oluşturur. EN: Creates the context.</summary>
    /// <param name="bankAccountId">TR: Internal BankAccount kimliği. EN: Internal BankAccount identifier.</param>
    /// <param name="walletId">TR: Bağlı internal Wallet kimliği. EN: Linked internal Wallet identifier.</param>
    /// <param name="externalAccountId">TR: Provider hesap kimliği. EN: Provider account identifier.</param>
    /// <param name="currency">TR: Hesap/wallet currency'si. EN: Account/wallet currency.</param>
    /// <param name="countryCode">TR: Customer business-calendar ülke kodu. EN: Customer business-calendar country code.</param>
    public BankMoneyMovementContext(Guid bankAccountId, Guid walletId, Guid externalAccountId, CurrencyCode currency, string countryCode)
    {
        if (bankAccountId == Guid.Empty || walletId == Guid.Empty || externalAccountId == Guid.Empty) throw new ArgumentException("Bank movement context identifiers cannot be empty.");
        ArgumentException.ThrowIfNullOrWhiteSpace(countryCode);
        BankAccountId = bankAccountId;
        WalletId = walletId;
        ExternalAccountId = externalAccountId;
        Currency = currency;
        CountryCode = countryCode.Trim().ToUpperInvariant();
    }

    /// <summary>TR: Internal BankAccount kimliğini döndürür. EN: Gets internal BankAccount identifier.</summary>
    public Guid BankAccountId { get; }
    /// <summary>TR: Bağlı Wallet kimliğini döndürür. EN: Gets linked Wallet identifier.</summary>
    public Guid WalletId { get; }
    /// <summary>TR: Provider account kimliğini döndürür. EN: Gets provider account identifier.</summary>
    public Guid ExternalAccountId { get; }
    /// <summary>TR: Currency değerini döndürür. EN: Gets currency.</summary>
    public CurrencyCode Currency { get; }
    /// <summary>TR: Business-calendar ülke kodunu döndürür. EN: Gets business-calendar country code.</summary>
    public string CountryCode { get; }
}

/// <summary>TR: Yeni veya replay bank money movement hazırlama komutunu persistence katmanına taşır. EN: Carries a new or replayed bank-money-movement preparation command into persistence.</summary>
public sealed class BankMoneyMovementPreparation
{
    /// <summary>TR: Durable bank movement hazırlama request'ini oluşturur. EN: Creates the durable bank-movement preparation request.</summary>
    /// <param name="customerId">TR: Authenticated customer kimliği. EN: Authenticated customer identifier.</param>
    /// <param name="bankAccountId">TR: Internal bank account kimliği. EN: Internal bank-account identifier.</param>
    /// <param name="amount">TR: Pozitif para tutarı. EN: Positive monetary amount.</param>
    /// <param name="type">TR: Deposit veya Withdrawal yönü. EN: Deposit or Withdrawal direction.</param>
    /// <param name="idempotencyKey">TR: Client durable idempotency anahtarı. EN: Client durable-idempotency key.</param>
    /// <param name="correlationId">TR: Correlation kimliği. EN: Correlation identifier.</param>
    /// <param name="cutoffReference">TR: Withdrawal cutoff provider referansı veya null. EN: Withdrawal cutoff-provider reference or null.</param>
    /// <param name="processingDate">TR: İşlem business processing tarihi. EN: Operation business processing date.</param>
    /// <param name="settlementDate">TR: İşlem settlement tarihi. EN: Operation settlement date.</param>
    /// <param name="canProcessNow">TR: Provider çağrısının hemen başlayabilmesini belirtir. EN: Indicates whether provider processing may start immediately.</param>
    public BankMoneyMovementPreparation(Guid customerId, Guid bankAccountId, Money amount, BankMoneyMovementType type, string idempotencyKey, string correlationId, Guid? cutoffReference, DateOnly processingDate, DateOnly settlementDate, bool canProcessNow)
    {
        if (customerId == Guid.Empty || bankAccountId == Guid.Empty) throw new ArgumentException("Bank movement identifiers cannot be empty.");
        if (!amount.IsPositive) throw new ArgumentOutOfRangeException(nameof(amount));
        if (!Enum.IsDefined(type)) throw new ArgumentOutOfRangeException(nameof(type));
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        if (idempotencyKey.Trim().Length > 128) throw new ArgumentOutOfRangeException(nameof(idempotencyKey));
        if (settlementDate < processingDate) throw new ArgumentException("Settlement date cannot precede processing date.", nameof(settlementDate));
        CustomerId = customerId;
        BankAccountId = bankAccountId;
        Amount = amount;
        Type = type;
        IdempotencyKey = idempotencyKey.Trim();
        CorrelationId = correlationId.Trim();
        CutoffReference = cutoffReference;
        ProcessingDate = processingDate;
        SettlementDate = settlementDate;
        CanProcessNow = canProcessNow;
    }

    /// <summary>TR: Authenticated customer kimliğini döndürür. EN: Gets authenticated customer identifier.</summary>
    public Guid CustomerId { get; }
    /// <summary>TR: Internal bank account kimliğini döndürür. EN: Gets internal bank-account identifier.</summary>
    public Guid BankAccountId { get; }
    /// <summary>TR: Currency-aware tutarı döndürür. EN: Gets currency-aware amount.</summary>
    public Money Amount { get; }
    /// <summary>TR: Deposit/Withdrawal tipini döndürür. EN: Gets Deposit/Withdrawal type.</summary>
    public BankMoneyMovementType Type { get; }
    /// <summary>TR: Durable idempotency anahtarını döndürür. EN: Gets durable idempotency key.</summary>
    public string IdempotencyKey { get; }
    /// <summary>TR: Correlation kimliğini döndürür. EN: Gets correlation identifier.</summary>
    public string CorrelationId { get; }
    /// <summary>TR: Cutoff provider referansını döndürür. EN: Gets cutoff-provider reference.</summary>
    public Guid? CutoffReference { get; }
    /// <summary>TR: Processing business tarihini döndürür. EN: Gets processing business date.</summary>
    public DateOnly ProcessingDate { get; }
    /// <summary>TR: Settlement business tarihini döndürür. EN: Gets settlement business date.</summary>
    public DateOnly SettlementDate { get; }
    /// <summary>TR: İşlemin provider tarafında hemen başlayabilmesini döndürür. EN: Gets whether provider processing may start immediately.</summary>
    public bool CanProcessNow { get; }
}

/// <summary>TR: Durable bank movement state'ini handler/API katmanına taşır. EN: Carries durable bank-movement state to handler/API layers.</summary>
public sealed class BankMoneyMovementResult
{
    /// <summary>TR: Bank movement sonucunu oluşturur. EN: Creates the bank-movement result.</summary>
    /// <param name="transactionId">TR: Internal FinancialTransaction kimliği. EN: Internal FinancialTransaction identifier.</param>
    /// <param name="bankAccountId">TR: Internal BankAccount kimliği. EN: Internal BankAccount identifier.</param>
    /// <param name="externalAccountId">TR: Provider account kimliği. EN: Provider account identifier.</param>
    /// <param name="externalTransactionId">TR: Provider transaction kimliği veya null. EN: Provider transaction identifier or null.</param>
    /// <param name="type">TR: Deposit/Withdrawal tipi. EN: Deposit/Withdrawal type.</param>
    /// <param name="amount">TR: Currency-aware tutar. EN: Currency-aware amount.</param>
    /// <param name="state">TR: FinWallet operation state'i. EN: FinWallet operation state.</param>
    /// <param name="processingDate">TR: Processing business tarihi. EN: Processing business date.</param>
    /// <param name="settlementDate">TR: Settlement business tarihi. EN: Settlement business date.</param>
    /// <param name="wasReplay">TR: Sonucun durable replay olup olmadığını belirtir. EN: Indicates whether result is a durable replay.</param>
    public BankMoneyMovementResult(Guid transactionId, Guid bankAccountId, Guid externalAccountId, Guid? externalTransactionId, BankMoneyMovementType type, Money amount, BankMoneyMovementState state, DateOnly processingDate, DateOnly settlementDate, bool wasReplay)
    {
        TransactionId = transactionId;
        BankAccountId = bankAccountId;
        ExternalAccountId = externalAccountId;
        ExternalTransactionId = externalTransactionId;
        Type = type;
        Amount = amount;
        State = state;
        ProcessingDate = processingDate;
        SettlementDate = settlementDate;
        WasReplay = wasReplay;
    }

    /// <summary>TR: Internal transaction kimliğini döndürür. EN: Gets internal transaction identifier.</summary>
    public Guid TransactionId { get; }
    /// <summary>TR: Internal bank account kimliğini döndürür. EN: Gets internal bank-account identifier.</summary>
    public Guid BankAccountId { get; }
    /// <summary>TR: Provider account kimliğini döndürür. EN: Gets provider account identifier.</summary>
    public Guid ExternalAccountId { get; }
    /// <summary>TR: Provider transaction kimliğini veya null değerini döndürür. EN: Gets provider transaction identifier or null.</summary>
    public Guid? ExternalTransactionId { get; }
    /// <summary>TR: Deposit/Withdrawal tipini döndürür. EN: Gets Deposit/Withdrawal type.</summary>
    public BankMoneyMovementType Type { get; }
    /// <summary>TR: Currency-aware tutarı döndürür. EN: Gets currency-aware amount.</summary>
    public Money Amount { get; }
    /// <summary>TR: Operation state'ini döndürür. EN: Gets operation state.</summary>
    public BankMoneyMovementState State { get; }
    /// <summary>TR: Processing business tarihini döndürür. EN: Gets processing business date.</summary>
    public DateOnly ProcessingDate { get; }
    /// <summary>TR: Settlement business tarihini döndürür. EN: Gets settlement business date.</summary>
    public DateOnly SettlementDate { get; }
    /// <summary>TR: Durable replay bilgisini döndürür. EN: Gets durable-replay state.</summary>
    public bool WasReplay { get; }
}

/// <summary>TR: Bank movement correctness state'ini MSSQL implementasyonundan ayıran aggregate-specific persistence sınırıdır. EN: Aggregate-specific persistence boundary decoupling bank-movement correctness state from the MSSQL implementation.</summary>
public interface IBankMoneyMovementStore
{
    /// <summary>TR: Authenticated bank account/customer context'ini server-side state'ten yükler. EN: Loads authenticated bank-account/customer context from server-side state.</summary>
    /// <param name="customerId">TR: Authenticated customer kimliği. EN: Authenticated customer identifier.</param>
    /// <param name="bankAccountId">TR: Internal bank account kimliği. EN: Internal bank-account identifier.</param>
    /// <param name="cancellationToken">TR: SQL sorgu iptal sinyali. EN: SQL-query cancellation signal.</param>
    /// <returns>TR: Aktif ve provider'a bağlı account context'ini; yoksa null döndürür. EN: Returns active provider-linked account context, or null.</returns>
    Task<BankMoneyMovementContext?> FindContextAsync(Guid customerId, Guid bankAccountId, CancellationToken cancellationToken);

    /// <summary>TR: Durable idempotency claim'i ve gerekiyorsa withdrawal fund blocking'i tek SQL transaction içinde oluşturur veya mevcut state'i replay eder. EN: Creates the durable idempotency claim and, when required, withdrawal fund blocking in one SQL transaction or replays existing state.</summary>
    /// <param name="request">TR: Hazırlanacak bank movement request'i. EN: Bank-movement request to prepare.</param>
    /// <param name="cancellationToken">TR: SQL transaction iptal sinyali. EN: SQL-transaction cancellation signal.</param>
    /// <returns>TR: Yeni veya replay durable sonucu döndürür. EN: Returns new or replayed durable result.</returns>
    Task<BankMoneyMovementResult> PrepareAsync(BankMoneyMovementPreparation request, CancellationToken cancellationToken);

    /// <summary>TR: Provider sonucunu durable state'e uygular; Completed olduğunda wallet+ledger+outbox'ı aynı SQL transaction içinde commit eder. EN: Applies provider result to durable state and commits wallet+ledger+outbox in the same SQL transaction when Completed.</summary>
    /// <param name="transactionId">TR: Internal FinancialTransaction kimliği. EN: Internal FinancialTransaction identifier.</param>
    /// <param name="externalTransactionId">TR: Provider transaction kimliği. EN: Provider transaction identifier.</param>
    /// <param name="providerStatus">TR: Provider transaction durumu. EN: Provider transaction status.</param>
    /// <param name="cancellationToken">TR: SQL transaction iptal sinyali. EN: SQL-transaction cancellation signal.</param>
    /// <returns>TR: Güncel durable bank movement sonucunu döndürür. EN: Returns current durable bank-movement result.</returns>
    Task<BankMoneyMovementResult> ApplyProviderResultAsync(Guid transactionId, Guid externalTransactionId, ExternalBankTransactionStatus providerStatus, CancellationToken cancellationToken);

    /// <summary>TR: Background processor için zamanı gelmiş pending/scheduled bank movement'larını listeler. EN: Lists due pending/scheduled bank movements for the background processor.</summary>
    /// <param name="now">TR: Due karşılaştırma UTC zamanı. EN: UTC time used for due comparison.</param>
    /// <param name="take">TR: En fazla alınacak kayıt sayısı. EN: Maximum number of rows to return.</param>
    /// <param name="cancellationToken">TR: SQL sorgu iptal sinyali. EN: SQL-query cancellation signal.</param>
    /// <returns>TR: İşlenebilir durable bank movement sonuçlarını döndürür. EN: Returns processable durable bank-movement results.</returns>
    Task<IReadOnlyCollection<BankMoneyMovementResult>> ListDueAsync(DateTimeOffset now, int take, CancellationToken cancellationToken);
}

/// <summary>TR: Bank movement idempotency key aynı key ile farklı payload için kullanıldığında oluşur. EN: Raised when a bank-movement idempotency key is reused with a different payload.</summary>
public sealed class BankMoneyMovementIdempotencyConflictException : Exception
{
    /// <summary>TR: Idempotency conflict exception oluşturur. EN: Creates the idempotency-conflict exception.</summary>
    public BankMoneyMovementIdempotencyConflictException() : base("The Idempotency-Key was already used with a different bank movement request.") { }
}

/// <summary>TR: Bank movement için aktif/provider-linked account bulunamadığında oluşur. EN: Raised when an active provider-linked account cannot be found for a bank movement.</summary>
public sealed class BankMoneyMovementAccountUnavailableException : Exception
{
    /// <summary>TR: Account unavailable exception oluşturur. EN: Creates the account-unavailable exception.</summary>
    public BankMoneyMovementAccountUnavailableException() : base("The bank account is not available for this operation.") { }
}
