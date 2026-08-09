using FinWallet.Domain.Shared;

namespace FinWallet.Application.Transfers;

/// <summary>
/// TR: Atomik wallet transfer posting sonucunun immutable transaction bilgisini taşır; değişebilir wallet bakiye snapshot'larını idempotency response'una dahil etmez.
/// EN: Carries immutable transaction information returned by atomic wallet-transfer posting and deliberately excludes mutable wallet-balance snapshots from idempotency responses.
/// </summary>
public sealed class WalletTransferPostingResult
{
    /// <summary>TR: Wallet transfer posting sonucunu oluşturur. EN: Creates a wallet-transfer posting result.</summary>
    /// <param name="transactionId">TR: Durable FinancialTransaction kimliği. EN: Durable FinancialTransaction identifier.</param>
    /// <param name="sourceWalletId">TR: Source wallet kimliği. EN: Source-wallet identifier.</param>
    /// <param name="destinationWalletId">TR: Destination wallet kimliği. EN: Destination-wallet identifier.</param>
    /// <param name="amount">TR: Currency-aware transfer tutarı. EN: Currency-aware transfer amount.</param>
    /// <param name="completedAt">TR: Transaction'ın original completion UTC zamanı. EN: Original UTC completion time of the transaction.</param>
    /// <param name="wasReplay">TR: Sonucun daha önce tamamlanmış idempotent request'ten döndüğünü belirtir. EN: Indicates that the result was replayed from a previously completed idempotent request.</param>
    public WalletTransferPostingResult(Guid transactionId, Guid sourceWalletId, Guid destinationWalletId, Money amount, DateTimeOffset completedAt, bool wasReplay)
    {
        if (transactionId == Guid.Empty) throw new ArgumentException("Transaction identifier cannot be empty.", nameof(transactionId));
        if (sourceWalletId == Guid.Empty) throw new ArgumentException("Source wallet identifier cannot be empty.", nameof(sourceWalletId));
        if (destinationWalletId == Guid.Empty) throw new ArgumentException("Destination wallet identifier cannot be empty.", nameof(destinationWalletId));
        if (!amount.IsPositive) throw new ArgumentOutOfRangeException(nameof(amount));
        TransactionId = transactionId;
        SourceWalletId = sourceWalletId;
        DestinationWalletId = destinationWalletId;
        Amount = amount;
        CompletedAt = completedAt;
        WasReplay = wasReplay;
    }

    /// <summary>TR: Durable FinancialTransaction kimliğini döndürür. EN: Gets durable FinancialTransaction identifier.</summary>
    public Guid TransactionId { get; }

    /// <summary>TR: Source wallet kimliğini döndürür. EN: Gets source-wallet identifier.</summary>
    public Guid SourceWalletId { get; }

    /// <summary>TR: Destination wallet kimliğini döndürür. EN: Gets destination-wallet identifier.</summary>
    public Guid DestinationWalletId { get; }

    /// <summary>TR: Currency-aware transfer tutarını döndürür. EN: Gets currency-aware transfer amount.</summary>
    public Money Amount { get; }

    /// <summary>TR: Original completion UTC zamanını döndürür. EN: Gets original UTC completion time.</summary>
    public DateTimeOffset CompletedAt { get; }

    /// <summary>TR: Sonuç idempotent replay ise true döndürür. EN: Gets whether the result is an idempotent replay.</summary>
    public bool WasReplay { get; }
}
