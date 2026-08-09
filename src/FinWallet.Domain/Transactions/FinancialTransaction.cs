using FinWallet.Domain.Shared;

namespace FinWallet.Domain.Transactions;

/// <summary>
/// TR: API request'inden bağımsız durable finansal işlem kimliği, türü, wallet referansları, tutar ve lifecycle sonucunu temsil eder.
/// EN: Represents durable financial-operation identity, type, wallet references, amount and lifecycle outcome independently from an API request.
/// </summary>
public sealed class FinancialTransaction
{
    /// <summary>TR: Persistence materialization için ayrılmış kurucudur. EN: Constructor reserved for persistence materialization.</summary>
    private FinancialTransaction()
    {
    }

    /// <summary>TR: İki farklı wallet arasındaki yeni Processing transfer transaction'ını oluşturur. EN: Creates a new Processing transfer transaction between two distinct wallets.</summary>
    /// <param name="id">TR: Durable transaction kimliği. EN: Durable transaction identifier.</param>
    /// <param name="customerId">TR: Source-wallet customer kimliği. EN: Source-wallet customer identifier.</param>
    /// <param name="sourceWalletId">TR: Para çıkacak wallet kimliği. EN: Wallet identifier being debited.</param>
    /// <param name="destinationWalletId">TR: Para girecek wallet kimliği. EN: Wallet identifier being credited.</param>
    /// <param name="amount">TR: Pozitif currency-aware tutar. EN: Positive currency-aware amount.</param>
    /// <param name="createdAt">TR: Oluşturulma UTC zamanı. EN: UTC creation time.</param>
    /// <returns>TR: Processing WalletTransfer aggregate'ini döndürür. EN: Returns a Processing WalletTransfer aggregate.</returns>
    public static FinancialTransaction CreateWalletTransfer(Guid id, Guid customerId, Guid sourceWalletId, Guid destinationWalletId, Money amount, DateTimeOffset createdAt)
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

    /// <summary>TR: MSSQL kaydındaki transaction state'ini lifecycle invariant'larını doğrulayarak yeniden oluşturur. EN: Rehydrates transaction state from MSSQL while validating lifecycle invariants.</summary>
    /// <param name="id">TR: Durable transaction kimliği. EN: Durable transaction identifier.</param>
    /// <param name="customerId">TR: İşlemi başlatan customer kimliği. EN: Customer identifier initiating the operation.</param>
    /// <param name="type">TR: Transaction türü. EN: Transaction type.</param>
    /// <param name="status">TR: Lifecycle durumu. EN: Lifecycle state.</param>
    /// <param name="sourceWalletId">TR: İsteğe bağlı source wallet. EN: Optional source wallet.</param>
    /// <param name="destinationWalletId">TR: İsteğe bağlı destination wallet. EN: Optional destination wallet.</param>
    /// <param name="amount">TR: Currency-aware tutar. EN: Currency-aware amount.</param>
    /// <param name="createdAt">TR: Oluşturulma UTC zamanı. EN: UTC creation time.</param>
    /// <param name="finalizedAt">TR: Completed/Failed ilk final zamanı; Processing ise null. EN: Initial final time for Completed/Failed state, or null while Processing.</param>
    /// <param name="reversedAt">TR: Reversed transaction için reversal zamanı; diğer durumlarda null. EN: Reversal time for a Reversed transaction, otherwise null.</param>
    /// <param name="failureCode">TR: Failed durumunda güvenli failure code. EN: Safe failure code in Failed state.</param>
    /// <returns>TR: Rehydrate edilmiş aggregate'i döndürür. EN: Returns the rehydrated aggregate.</returns>
    public static FinancialTransaction Restore(
        Guid id,
        Guid customerId,
        FinancialTransactionType type,
        FinancialTransactionStatus status,
        Guid? sourceWalletId,
        Guid? destinationWalletId,
        Money amount,
        DateTimeOffset createdAt,
        DateTimeOffset? finalizedAt,
        DateTimeOffset? reversedAt,
        string? failureCode)
    {
        ValidateCore(id, customerId, amount);
        if (!Enum.IsDefined(type)) throw new ArgumentOutOfRangeException(nameof(type));
        if (!Enum.IsDefined(status)) throw new ArgumentOutOfRangeException(nameof(status));
        ValidateLifecycle(status, createdAt, finalizedAt, reversedAt, failureCode);
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
            FinalizedAt = finalizedAt,
            ReversedAt = reversedAt,
            FailureCode = string.IsNullOrWhiteSpace(failureCode) ? null : failureCode.Trim()
        };
    }

    /// <summary>TR: Durable financial transaction kimliğini döndürür. EN: Gets durable financial-transaction identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>TR: İşlemi başlatan customer kimliğini döndürür. EN: Gets customer identifier initiating the operation.</summary>
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

    /// <summary>TR: İlk Completed/Failed final UTC zamanını döndürür ve reversal sonrasında değişmez. EN: Gets the initial Completed/Failed final UTC time and remains unchanged after reversal.</summary>
    public DateTimeOffset? FinalizedAt { get; private set; }

    /// <summary>TR: Reversal UTC zamanını; transaction terslenmemişse null döndürür. EN: Gets reversal UTC time, or null when the transaction has not been reversed.</summary>
    public DateTimeOffset? ReversedAt { get; private set; }

    /// <summary>TR: Failed transaction failure code'unu; diğer durumlarda null döndürür. EN: Gets failure code for a Failed transaction, otherwise null.</summary>
    public string? FailureCode { get; private set; }

    /// <summary>TR: Processing transaction'ı başarıyla Completed duruma geçirir. EN: Transitions a Processing transaction into successful Completed state.</summary>
    /// <param name="completedAt">TR: Başarı final UTC zamanı. EN: Successful final UTC time.</param>
    public void Complete(DateTimeOffset completedAt)
    {
        EnsureProcessing();
        EnsureTimeNotBeforeCreated(completedAt);
        Status = FinancialTransactionStatus.Completed;
        FinalizedAt = completedAt;
    }

    /// <summary>TR: Processing transaction'ı güvenli failure code ile Failed duruma geçirir. EN: Transitions a Processing transaction into Failed state with a safe failure code.</summary>
    /// <param name="failureCode">TR: En fazla 64 karakterlik machine-readable failure code. EN: Machine-readable failure code up to 64 characters.</param>
    /// <param name="failedAt">TR: Failure final UTC zamanı. EN: Failure final UTC time.</param>
    public void Fail(string failureCode, DateTimeOffset failedAt)
    {
        EnsureProcessing();
        ArgumentException.ThrowIfNullOrWhiteSpace(failureCode);
        if (failureCode.Trim().Length > 64) throw new ArgumentOutOfRangeException(nameof(failureCode));
        EnsureTimeNotBeforeCreated(failedAt);
        Status = FinancialTransactionStatus.Failed;
        FinalizedAt = failedAt;
        FailureCode = failureCode.Trim();
    }

    /// <summary>TR: Completed transaction'ın etkisinin ayrı reversal ile terslendiğini işaretler; original completion zamanını korur. EN: Marks a Completed transaction as reversed by a separate reversal while preserving original completion time.</summary>
    /// <param name="reversedAt">TR: Reversal UTC zamanı. EN: Reversal UTC time.</param>
    public void MarkReversed(DateTimeOffset reversedAt)
    {
        if (Status != FinancialTransactionStatus.Completed) throw new InvalidOperationException("Only a Completed transaction can be marked Reversed.");
        if (FinalizedAt is null) throw new InvalidOperationException("Completed transaction must have a finalization time.");
        if (reversedAt < FinalizedAt.Value) throw new ArgumentException("Reversal time cannot precede original finalization.", nameof(reversedAt));
        Status = FinancialTransactionStatus.Reversed;
        ReversedAt = reversedAt;
    }

    /// <summary>TR: Transaction'ın halen Processing olduğunu doğrular. EN: Ensures the transaction is still Processing.</summary>
    private void EnsureProcessing()
    {
        if (Status != FinancialTransactionStatus.Processing) throw new InvalidOperationException("Only a Processing transaction can be finalized.");
    }

    /// <summary>TR: Zamanın create zamanından önce olmadığını doğrular. EN: Ensures a lifecycle time does not precede creation time.</summary>
    /// <param name="time">TR: Doğrulanacak UTC zaman. EN: UTC time to validate.</param>
    private void EnsureTimeNotBeforeCreated(DateTimeOffset time)
    {
        if (time < CreatedAt) throw new ArgumentException("Transaction lifecycle time cannot precede creation time.", nameof(time));
    }

    /// <summary>TR: Restore sırasında lifecycle timestamp/failure invariant'larını doğrular. EN: Validates lifecycle timestamp/failure invariants during Restore.</summary>
    /// <param name="status">TR: Persisted status. EN: Persisted status.</param>
    /// <param name="createdAt">TR: Persisted creation time. EN: Persisted creation time.</param>
    /// <param name="finalizedAt">TR: Persisted initial finalization time. EN: Persisted initial finalization time.</param>
    /// <param name="reversedAt">TR: Persisted reversal time. EN: Persisted reversal time.</param>
    /// <param name="failureCode">TR: Persisted failure code. EN: Persisted failure code.</param>
    private static void ValidateLifecycle(
        FinancialTransactionStatus status,
        DateTimeOffset createdAt,
        DateTimeOffset? finalizedAt,
        DateTimeOffset? reversedAt,
        string? failureCode)
    {
        if (finalizedAt.HasValue && finalizedAt.Value < createdAt) throw new ArgumentException("Finalization time cannot precede creation.", nameof(finalizedAt));
        if (reversedAt.HasValue && (!finalizedAt.HasValue || reversedAt.Value < finalizedAt.Value)) throw new ArgumentException("Reversal time cannot precede finalization.", nameof(reversedAt));

        switch (status)
        {
            case FinancialTransactionStatus.Processing when finalizedAt is not null || reversedAt is not null || !string.IsNullOrWhiteSpace(failureCode):
                throw new ArgumentException("Processing transaction cannot carry final lifecycle fields.");
            case FinancialTransactionStatus.Completed when finalizedAt is null || reversedAt is not null || !string.IsNullOrWhiteSpace(failureCode):
                throw new ArgumentException("Completed transaction lifecycle fields are inconsistent.");
            case FinancialTransactionStatus.Failed when finalizedAt is null || reversedAt is not null || string.IsNullOrWhiteSpace(failureCode):
                throw new ArgumentException("Failed transaction lifecycle fields are inconsistent.");
            case FinancialTransactionStatus.Reversed when finalizedAt is null || reversedAt is null || !string.IsNullOrWhiteSpace(failureCode):
                throw new ArgumentException("Reversed transaction lifecycle fields are inconsistent.");
        }
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
