using FinWallet.Domain.Shared;

namespace FinWallet.Domain.Transactions;

/// <summary>
/// TR: API request'inden bağımsız durable finansal işlem kimliği, türü, wallet referansları, tutar ve final lifecycle sonucunu temsil eder.
/// EN: Represents durable financial-operation identity, type, wallet references, amount and final lifecycle outcome independently from an API request.
/// </summary>
public sealed class FinancialTransaction
{
    /// <summary>TR: Persistence materialization için ayrılmış kurucudur. EN: Constructor reserved for persistence materialization.</summary>
    private FinancialTransaction()
    {
    }

    /// <summary>
    /// TR: İki farklı aynı-currency wallet arasındaki yeni Processing transfer transaction'ını oluşturur.
    /// EN: Creates a new Processing transfer transaction between two distinct wallets in the same currency.
    /// </summary>
    /// <param name="id">TR: Durable financial transaction kimliği. EN: Durable financial-transaction identifier.</param>
    /// <param name="customerId">TR: İşlemi başlatan source-wallet customer kimliği. EN: Source-wallet customer identifier initiating the operation.</param>
    /// <param name="sourceWalletId">TR: Para çıkacak internal wallet kimliği. EN: Internal wallet identifier being debited.</param>
    /// <param name="destinationWalletId">TR: Para girecek internal wallet kimliği. EN: Internal wallet identifier being credited.</param>
    /// <param name="amount">TR: Pozitif currency-aware transfer tutarı. EN: Positive currency-aware transfer amount.</param>
    /// <param name="createdAt">TR: Transaction oluşturulma UTC zamanı. EN: UTC transaction creation time.</param>
    /// <returns>TR: Processing durumundaki WalletTransfer aggregate'ini döndürür. EN: Returns a WalletTransfer aggregate in Processing state.</returns>
    public static FinancialTransaction CreateWalletTransfer(
        Guid id,
        Guid customerId,
        Guid sourceWalletId,
        Guid destinationWalletId,
        Money amount,
        DateTimeOffset createdAt)
    {
        ValidateCore(id, customerId, amount);
        if (sourceWalletId == Guid.Empty) throw new ArgumentException("Source wallet identifier cannot be empty.", nameof(sourceWalletId));
        if (destinationWalletId == Guid.Empty) throw new ArgumentException("Destination wallet identifier cannot be empty.", nameof(destinationWalletId));
        if (sourceWalletId == destinationWalletId) throw new ArgumentException("Source and destination wallets must differ.");

        return new FinancialTransaction
        {
            Id = id,
            CustomerId = customerId,
            Type = FinancialTransactionType.WalletTransfer,
            Status = FinancialTransactionStatus.Processing,
            SourceWalletId = sourceWalletId,
            DestinationWalletId = destinationWalletId,
            Amount = amount,
            CreatedAt = createdAt
        };
    }

    /// <summary>
    /// TR: MSSQL kaydındaki finansal transaction state'ini lifecycle invariant'larını doğrulayarak yeniden oluşturur.
    /// EN: Rehydrates financial-transaction state from MSSQL while validating lifecycle invariants.
    /// </summary>
    /// <param name="id">TR: Durable transaction kimliği. EN: Durable transaction identifier.</param>
    /// <param name="customerId">TR: İşlemi başlatan customer kimliği. EN: Customer identifier that initiated the operation.</param>
    /// <param name="type">TR: Transaction türü. EN: Transaction type.</param>
    /// <param name="status">TR: Transaction lifecycle durumu. EN: Transaction lifecycle state.</param>
    /// <param name="sourceWalletId">TR: İsteğe bağlı source wallet kimliği. EN: Optional source-wallet identifier.</param>
    /// <param name="destinationWalletId">TR: İsteğe bağlı destination wallet kimliği. EN: Optional destination-wallet identifier.</param>
    /// <param name="amount">TR: Currency-aware transaction tutarı. EN: Currency-aware transaction amount.</param>
    /// <param name="createdAt">TR: Oluşturulma UTC zamanı. EN: UTC creation time.</param>
    /// <param name="completedAt">TR: Final UTC zamanı; Processing ise null. EN: Final UTC time, or null while Processing.</param>
    /// <param name="failureCode">TR: Failed transaction için güvenli failure code; diğer durumlarda null. EN: Safe failure code for a Failed transaction, otherwise null.</param>
    /// <returns>TR: Rehydrate edilmiş financial transaction aggregate'ini döndürür. EN: Returns the rehydrated financial-transaction aggregate.</returns>
    public static FinancialTransaction Restore(
        Guid id,
        Guid customerId,
        FinancialTransactionType type,
        FinancialTransactionStatus status,
        Guid? sourceWalletId,
        Guid? destinationWalletId,
        Money amount,
        DateTimeOffset createdAt,
        DateTimeOffset? completedAt,
        string? failureCode)
    {
        ValidateCore(id, customerId, amount);
        if (!Enum.IsDefined(type)) throw new ArgumentOutOfRangeException(nameof(type));
        if (!Enum.IsDefined(status)) throw new ArgumentOutOfRangeException(nameof(status));
        if (status == FinancialTransactionStatus.Processing && completedAt is not null) throw new ArgumentException("Processing transaction cannot have completion time.", nameof(completedAt));
        if (status != FinancialTransactionStatus.Processing && completedAt is null) throw new ArgumentException("Final transaction must have completion time.", nameof(completedAt));
        if (completedAt.HasValue && completedAt.Value < createdAt) throw new ArgumentException("Completion time cannot precede creation.", nameof(completedAt));
        if (status == FinancialTransactionStatus.Failed && string.IsNullOrWhiteSpace(failureCode)) throw new ArgumentException("Failed transaction requires a failure code.", nameof(failureCode));
        if (status != FinancialTransactionStatus.Failed && !string.IsNullOrWhiteSpace(failureCode)) throw new ArgumentException("Only Failed transaction may carry a failure code.", nameof(failureCode));
        if (type == FinancialTransactionType.WalletTransfer &&
            (sourceWalletId is null || destinationWalletId is null || sourceWalletId == destinationWalletId))
        {
            throw new ArgumentException("Wallet transfer requires distinct source and destination wallets.");
        }

        return new FinancialTransaction
        {
            Id = id,
            CustomerId = customerId,
            Type = type,
            Status = status,
            SourceWalletId = sourceWalletId,
            DestinationWalletId = destinationWalletId,
            Amount = amount,
            CreatedAt = createdAt,
            CompletedAt = completedAt,
            FailureCode = string.IsNullOrWhiteSpace(failureCode) ? null : failureCode.Trim()
        };
    }

    /// <summary>TR: Durable financial transaction kimliğini döndürür. EN: Gets durable financial-transaction identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>TR: İşlemi başlatan customer kimliğini döndürür. EN: Gets customer identifier that initiated the operation.</summary>
    public Guid CustomerId { get; private set; }

    /// <summary>TR: Finansal transaction türünü döndürür. EN: Gets financial-transaction type.</summary>
    public FinancialTransactionType Type { get; private set; }

    /// <summary>TR: Transaction lifecycle durumunu döndürür. EN: Gets transaction lifecycle state.</summary>
    public FinancialTransactionStatus Status { get; private set; }

    /// <summary>TR: Source wallet kimliğini; uygulanmıyorsa null döndürür. EN: Gets source-wallet identifier, or null when not applicable.</summary>
    public Guid? SourceWalletId { get; private set; }

    /// <summary>TR: Destination wallet kimliğini; uygulanmıyorsa null döndürür. EN: Gets destination-wallet identifier, or null when not applicable.</summary>
    public Guid? DestinationWalletId { get; private set; }

    /// <summary>TR: Currency-aware finansal tutarı döndürür. EN: Gets currency-aware financial amount.</summary>
    public Money Amount { get; private set; }

    /// <summary>TR: Transaction oluşturulma UTC zamanını döndürür. EN: Gets transaction UTC creation time.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>TR: Transaction final UTC zamanını; Processing ise null döndürür. EN: Gets transaction final UTC time, or null while Processing.</summary>
    public DateTimeOffset? CompletedAt { get; private set; }

    /// <summary>TR: Failed transaction failure code'unu; diğer durumlarda null döndürür. EN: Gets failure code for a Failed transaction, otherwise null.</summary>
    public string? FailureCode { get; private set; }

    /// <summary>TR: Processing transaction'ı başarıyla Completed duruma geçirir. EN: Transitions a Processing transaction into successful Completed state.</summary>
    /// <param name="completedAt">TR: Başarı final UTC zamanı. EN: Successful final UTC time.</param>
    public void Complete(DateTimeOffset completedAt)
    {
        EnsureProcessing();
        EnsureFinalTime(completedAt);
        Status = FinancialTransactionStatus.Completed;
        CompletedAt = completedAt;
    }

    /// <summary>TR: Processing transaction'ı güvenli failure code ile Failed duruma geçirir. EN: Transitions a Processing transaction into Failed state with a safe failure code.</summary>
    /// <param name="failureCode">TR: En fazla 64 karakterlik machine-readable failure code. EN: Machine-readable failure code up to 64 characters.</param>
    /// <param name="failedAt">TR: Failure final UTC zamanı. EN: Failure final UTC time.</param>
    public void Fail(string failureCode, DateTimeOffset failedAt)
    {
        EnsureProcessing();
        ArgumentException.ThrowIfNullOrWhiteSpace(failureCode);
        if (failureCode.Trim().Length > 64) throw new ArgumentOutOfRangeException(nameof(failureCode));
        EnsureFinalTime(failedAt);
        Status = FinancialTransactionStatus.Failed;
        CompletedAt = failedAt;
        FailureCode = failureCode.Trim();
    }

    /// <summary>TR: Completed transaction'ın etkisinin ayrı reversal ile terslendiğini işaretler. EN: Marks that a Completed transaction effect was reversed by a separate reversal.</summary>
    /// <param name="reversedAt">TR: Reversal final UTC zamanı. EN: Reversal final UTC time.</param>
    public void MarkReversed(DateTimeOffset reversedAt)
    {
        if (Status != FinancialTransactionStatus.Completed) throw new InvalidOperationException("Only a Completed transaction can be marked Reversed.");
        EnsureFinalTime(reversedAt);
        Status = FinancialTransactionStatus.Reversed;
        CompletedAt = reversedAt;
    }

    /// <summary>TR: Transaction'ın halen Processing olduğunu doğrular. EN: Ensures the transaction is still Processing.</summary>
    private void EnsureProcessing()
    {
        if (Status != FinancialTransactionStatus.Processing) throw new InvalidOperationException("Only a Processing transaction can be finalized.");
    }

    /// <summary>TR: Final zamanın create zamanından önce olmadığını doğrular. EN: Ensures final time does not precede creation time.</summary>
    /// <param name="time">TR: Doğrulanacak final UTC zamanı. EN: Final UTC time to validate.</param>
    private void EnsureFinalTime(DateTimeOffset time)
    {
        if (time < CreatedAt) throw new ArgumentException("Final transaction time cannot precede creation time.", nameof(time));
    }

    /// <summary>TR: Ortak transaction kimliği/customer/tutar invariant'larını doğrular. EN: Validates shared transaction identifier/customer/amount invariants.</summary>
    /// <param name="id">TR: Transaction kimliği. EN: Transaction identifier.</param>
    /// <param name="customerId">TR: Customer kimliği. EN: Customer identifier.</param>
    /// <param name="amount">TR: Pozitif currency-aware tutar. EN: Positive currency-aware amount.</param>
    private static void ValidateCore(Guid id, Guid customerId, Money amount)
    {
        if (id == Guid.Empty) throw new ArgumentException("Financial transaction identifier cannot be empty.", nameof(id));
        if (customerId == Guid.Empty) throw new ArgumentException("Customer identifier cannot be empty.", nameof(customerId));
        if (!amount.IsPositive) throw new ArgumentOutOfRangeException(nameof(amount));
    }
}
