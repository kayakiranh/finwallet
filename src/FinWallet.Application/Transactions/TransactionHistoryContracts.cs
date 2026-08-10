using FinWallet.Domain.Shared;
using FinWallet.Domain.Transactions;

namespace FinWallet.Application.Transactions;

/// <summary>TR: Customer-facing finansal transaction geçmişindeki tek read-model kaydını taşır; ledger satırlarının yerine geçmez. EN: Carries one customer-facing financial-transaction history read-model row; it never replaces ledger entries.</summary>
public sealed class TransactionHistoryItem
{
    /// <summary>TR: Transaction history item oluşturur. EN: Creates transaction-history item.</summary>
    public TransactionHistoryItem(Guid transactionId, FinancialTransactionType type, byte status, Guid? sourceWalletId, Guid? destinationWalletId, Money amount, DateTimeOffset createdAt, DateTimeOffset? finalizedAt, DateTimeOffset? reversedAt, string? failureCode, Guid? parentTransactionId, Guid? bankAccountId, Guid? externalTransactionId, string? merchantId, decimal? originalAmount, decimal? discountAmount, DateOnly? processingDate, DateOnly? settlementDate)
    {
        TransactionId = transactionId; Type = type; Status = status; SourceWalletId = sourceWalletId; DestinationWalletId = destinationWalletId; Amount = amount; CreatedAt = createdAt; FinalizedAt = finalizedAt; ReversedAt = reversedAt; FailureCode = failureCode; ParentTransactionId = parentTransactionId; BankAccountId = bankAccountId; ExternalTransactionId = externalTransactionId; MerchantId = merchantId; OriginalAmount = originalAmount; DiscountAmount = discountAmount; ProcessingDate = processingDate; SettlementDate = settlementDate;
    }

    /// <summary>TR: FinancialTransaction kimliğini döndürür. EN: Gets FinancialTransaction identifier.</summary>
    public Guid TransactionId { get; }
    /// <summary>TR: Financial transaction tipini döndürür. EN: Gets financial-transaction type.</summary>
    public FinancialTransactionType Type { get; }
    /// <summary>TR: Durable numeric transaction lifecycle state'ini döndürür. EN: Gets durable numeric transaction lifecycle state.</summary>
    public byte Status { get; }
    /// <summary>TR: Source wallet kimliğini döndürür. EN: Gets source-wallet identifier.</summary>
    public Guid? SourceWalletId { get; }
    /// <summary>TR: Destination wallet kimliğini döndürür. EN: Gets destination-wallet identifier.</summary>
    public Guid? DestinationWalletId { get; }
    /// <summary>TR: Transaction'ın customer-facing currency-aware tutarını döndürür. EN: Gets customer-facing currency-aware transaction amount.</summary>
    public Money Amount { get; }
    /// <summary>TR: Oluşturulma UTC zamanını döndürür. EN: Gets creation UTC timestamp.</summary>
    public DateTimeOffset CreatedAt { get; }
    /// <summary>TR: Finalize UTC zamanını döndürür. EN: Gets finalization UTC timestamp.</summary>
    public DateTimeOffset? FinalizedAt { get; }
    /// <summary>TR: Original transaction reversal UTC zamanını döndürür. EN: Gets original-transaction reversal UTC timestamp.</summary>
    public DateTimeOffset? ReversedAt { get; }
    /// <summary>TR: Güvenli failure code değerini döndürür. EN: Gets safe failure code.</summary>
    public string? FailureCode { get; }
    /// <summary>TR: Refund/Reversal parent transaction kimliğini döndürür. EN: Gets Refund/Reversal parent transaction identifier.</summary>
    public Guid? ParentTransactionId { get; }
    /// <summary>TR: Bank movement internal BankAccount kimliğini döndürür. EN: Gets bank-movement internal BankAccount identifier.</summary>
    public Guid? BankAccountId { get; }
    /// <summary>TR: External bank transaction kimliğini döndürür. EN: Gets external-bank transaction identifier.</summary>
    public Guid? ExternalTransactionId { get; }
    /// <summary>TR: Purchase merchant kimliğini döndürür. EN: Gets purchase merchant identifier.</summary>
    public string? MerchantId { get; }
    /// <summary>TR: Campaign öncesi purchase tutarını döndürür. EN: Gets purchase amount before campaign.</summary>
    public decimal? OriginalAmount { get; }
    /// <summary>TR: Campaign indirim tutarını döndürür. EN: Gets campaign discount amount.</summary>
    public decimal? DiscountAmount { get; }
    /// <summary>TR: Bank processing business tarihini döndürür. EN: Gets bank-processing business date.</summary>
    public DateOnly? ProcessingDate { get; }
    /// <summary>TR: Bank settlement business tarihini döndürür. EN: Gets bank-settlement business date.</summary>
    public DateOnly? SettlementDate { get; }
}

/// <summary>TR: Customer financial history read-side sorgusunu MSSQL implementasyonundan ayırır. EN: Decouples customer financial-history read-side query from MSSQL implementation.</summary>
public interface ITransactionHistoryStore
{
    /// <summary>TR: Customer'a ait transaction'ları newest-first keyset pagination ile listeler; cursor transaction dahil edilmez. EN: Lists customer transactions newest-first using keyset pagination; cursor transaction is excluded.</summary>
    /// <param name="customerId">TR: Authenticated customer kimliği. EN: Authenticated customer identifier.</param>
    /// <param name="beforeTransactionId">TR: Önceki sayfanın son transaction kimliği veya null. EN: Last transaction identifier from previous page or null.</param>
    /// <param name="take">TR: Sayfa boyutu. EN: Page size.</param>
    /// <param name="cancellationToken">TR: SQL sorgu iptal sinyali. EN: SQL-query cancellation signal.</param>
    /// <returns>TR: Customer history kayıtlarını döndürür. EN: Returns customer-history rows.</returns>
    Task<IReadOnlyCollection<TransactionHistoryItem>> ListAsync(Guid customerId, Guid? beforeTransactionId, int take, CancellationToken cancellationToken);
}

/// <summary>TR: Customer transaction history use-case'ini uygular. EN: Implements customer transaction-history use case.</summary>
public sealed class ListTransactionHistoryHandler
{
    private readonly ITransactionHistoryStore _store;

    /// <summary>TR: Transaction history store bağımlılığıyla handler'ı oluşturur. EN: Creates handler with transaction-history-store dependency.</summary>
    /// <param name="store">TR: Read-side MSSQL sınırı. EN: Read-side MSSQL boundary.</param>
    public ListTransactionHistoryHandler(ITransactionHistoryStore store) => _store = store ?? throw new ArgumentNullException(nameof(store));

    /// <summary>TR: 1–100 arası page size ile customer-owned transaction history listeler. EN: Lists customer-owned transaction history with a page size between 1 and 100.</summary>
    /// <param name="customerId">TR: Authenticated customer kimliği. EN: Authenticated customer identifier.</param>
    /// <param name="beforeTransactionId">TR: Keyset cursor transaction kimliği. EN: Keyset-cursor transaction identifier.</param>
    /// <param name="take">TR: İstenen page size. EN: Requested page size.</param>
    /// <param name="cancellationToken">TR: SQL iptal sinyali. EN: SQL cancellation signal.</param>
    /// <returns>TR: Transaction history sayfasını döndürür. EN: Returns transaction-history page.</returns>
    public Task<IReadOnlyCollection<TransactionHistoryItem>> HandleAsync(Guid customerId, Guid? beforeTransactionId, int take, CancellationToken cancellationToken)
    {
        if (customerId == Guid.Empty) throw new ArgumentException("Customer identifier cannot be empty.", nameof(customerId));
        if (take < 1 || take > 100) throw new ArgumentOutOfRangeException(nameof(take));
        return _store.ListAsync(customerId, beforeTransactionId, take, cancellationToken);
    }
}
