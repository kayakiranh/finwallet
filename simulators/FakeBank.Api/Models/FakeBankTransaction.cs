namespace FakeBank.Api.Models;

/// <summary>
/// TR: FakeBank simulatorında harici banka hesabına ait deposit/withdrawal isteğinin provider transaction state'ini temsil eder.
/// EN: Represents provider transaction state for a deposit/withdrawal request associated with an external bank account in FakeBank.
/// </summary>
public sealed class FakeBankTransaction
{
    /// <summary>
    /// TR: Harici banka transaction kaydını oluşturur.
    /// EN: Creates an external bank-transaction record.
    /// </summary>
    /// <param name="transactionId">TR: Provider transaction kimliği. EN: Provider transaction identifier.</param>
    /// <param name="accountId">TR: İşlemin bağlı olduğu provider hesap kimliği. EN: Provider account identifier associated with the transaction.</param>
    /// <param name="requestKey">TR: Provider-side duplicate korumasında kullanılan idempotency request anahtarı. EN: Idempotency request key used for provider-side duplicate protection.</param>
    /// <param name="type">TR: Deposit veya Withdrawal işlem tipi. EN: Deposit or Withdrawal transaction type.</param>
    /// <param name="amount">TR: Pozitif işlem tutarı. EN: Positive transaction amount.</param>
    /// <param name="currency">TR: İşlem para birimi. EN: Transaction currency.</param>
    /// <param name="status">TR: Başlangıç provider transaction durumu. EN: Initial provider transaction state.</param>
    /// <param name="createdAt">TR: Provider transaction oluşturulma UTC zamanı. EN: UTC time at which the provider transaction was created.</param>
    public FakeBankTransaction(Guid transactionId, Guid accountId, string requestKey, FakeBankTransactionType type, decimal amount, string currency, FakeBankTransactionStatus status, DateTimeOffset createdAt)
    {
        if (transactionId == Guid.Empty) throw new ArgumentException("Transaction identifier cannot be empty.", nameof(transactionId));
        if (accountId == Guid.Empty) throw new ArgumentException("Account identifier cannot be empty.", nameof(accountId));
        ArgumentException.ThrowIfNullOrWhiteSpace(requestKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));

        TransactionId = transactionId;
        AccountId = accountId;
        RequestKey = requestKey.Trim();
        Type = type;
        Amount = amount;
        Currency = currency.Trim().ToUpperInvariant();
        Status = status;
        CreatedAt = createdAt;
    }

    /// <summary>TR: Provider transaction kimliğini döndürür. EN: Gets the provider transaction identifier.</summary>
    public Guid TransactionId { get; }

    /// <summary>TR: Provider hesap kimliğini döndürür. EN: Gets the provider account identifier.</summary>
    public Guid AccountId { get; }

    /// <summary>TR: Provider-side idempotency request anahtarını döndürür. EN: Gets the provider-side idempotency request key.</summary>
    public string RequestKey { get; }

    /// <summary>TR: Transaction tipini döndürür. EN: Gets the transaction type.</summary>
    public FakeBankTransactionType Type { get; }

    /// <summary>TR: Transaction tutarını döndürür. EN: Gets the transaction amount.</summary>
    public decimal Amount { get; }

    /// <summary>TR: Transaction currency kodunu döndürür. EN: Gets the transaction currency code.</summary>
    public string Currency { get; }

    /// <summary>TR: Provider transaction durumunu döndürür. EN: Gets the provider transaction state.</summary>
    public FakeBankTransactionStatus Status { get; private set; }

    /// <summary>TR: Provider transaction oluşturulma UTC zamanını döndürür. EN: Gets the provider transaction UTC creation time.</summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>TR: Provider transaction tamamlanma/başarısızlık UTC zamanını; sonuçlanmadıysa null döndürür. EN: Gets provider transaction completion/failure UTC time, or null while unresolved.</summary>
    public DateTimeOffset? CompletedAt { get; private set; }

    /// <summary>
    /// TR: Pending transaction'ı başarılı provider sonucu olarak Completed durumuna geçirir.
    /// EN: Transitions a Pending transaction into Completed after a successful provider outcome.
    /// </summary>
    /// <param name="completedAt">TR: Başarılı sonuç UTC zamanı. EN: UTC time of successful outcome.</param>
    public void Complete(DateTimeOffset completedAt)
    {
        EnsurePending(completedAt);
        Status = FakeBankTransactionStatus.Completed;
        CompletedAt = completedAt;
    }

    /// <summary>
    /// TR: Pending transaction'ı başarısız provider sonucu olarak Failed durumuna geçirir.
    /// EN: Transitions a Pending transaction into Failed after an unsuccessful provider outcome.
    /// </summary>
    /// <param name="failedAt">TR: Başarısız sonuç UTC zamanı. EN: UTC time of failed outcome.</param>
    public void Fail(DateTimeOffset failedAt)
    {
        EnsurePending(failedAt);
        Status = FakeBankTransactionStatus.Failed;
        CompletedAt = failedAt;
    }

    /// <summary>
    /// TR: Transaction'ın yalnızca Pending durumdan final duruma geçmesini ve final zamanın creation öncesi olmamasını doğrular.
    /// EN: Ensures the transaction can move to a final state only from Pending and that final time does not precede creation.
    /// </summary>
    /// <param name="finalizedAt">TR: Final provider sonucu UTC zamanı. EN: UTC time of final provider outcome.</param>
    private void EnsurePending(DateTimeOffset finalizedAt)
    {
        if (Status != FakeBankTransactionStatus.Pending) throw new InvalidOperationException("Only pending bank transactions can be finalized.");
        if (finalizedAt < CreatedAt) throw new ArgumentException("Finalization cannot precede transaction creation.", nameof(finalizedAt));
    }
}
