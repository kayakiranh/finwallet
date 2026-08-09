using FinWallet.Domain.Shared;

namespace FinWallet.Application.Banking;

/// <summary>
/// TR: Reconciliation amacıyla dış bankadan okunan tamamlanmış transaction statement satırını provider DTO'sundan bağımsız taşır.
/// EN: Carries a completed external-bank statement transaction independently from provider DTOs for reconciliation.
/// </summary>
public sealed class ExternalBankStatementItem
{
    /// <summary>
    /// TR: Dış banka statement satırını oluşturur.
    /// EN: Creates an external-bank statement item.
    /// </summary>
    /// <param name="transactionId">TR: Provider transaction kimliği. EN: Provider transaction identifier.</param>
    /// <param name="transactionType">TR: Para hareketinin yönü. EN: Direction of the money movement.</param>
    /// <param name="amount">TR: Pozitif transaction tutarı. EN: Positive transaction amount.</param>
    /// <param name="currency">TR: Transaction currency değeri. EN: Transaction currency.</param>
    /// <param name="completedAt">TR: Provider tamamlanma UTC zamanı. EN: Provider completion UTC time.</param>
    public ExternalBankStatementItem(Guid transactionId, BankMoneyMovementType transactionType, decimal amount, CurrencyCode currency, DateTimeOffset completedAt)
    {
        if (transactionId == Guid.Empty) throw new ArgumentException("External transaction identifier cannot be empty.", nameof(transactionId));
        if (amount <= 0m) throw new ArgumentOutOfRangeException(nameof(amount));
        TransactionId = transactionId;
        TransactionType = transactionType;
        Amount = amount;
        Currency = currency;
        CompletedAt = completedAt;
    }

    /// <summary>TR: Provider transaction kimliğini döndürür. EN: Gets provider transaction identifier.</summary>
    public Guid TransactionId { get; }

    /// <summary>TR: Para hareketinin yönünü döndürür. EN: Gets money-movement direction.</summary>
    public BankMoneyMovementType TransactionType { get; }

    /// <summary>TR: Pozitif transaction tutarını döndürür. EN: Gets positive transaction amount.</summary>
    public decimal Amount { get; }

    /// <summary>TR: Transaction currency değerini döndürür. EN: Gets transaction currency.</summary>
    public CurrencyCode Currency { get; }

    /// <summary>TR: Provider tamamlanma UTC zamanını döndürür. EN: Gets provider completion UTC time.</summary>
    public DateTimeOffset CompletedAt { get; }
}
