using FakeBank.Api.Models;

namespace FakeBank.Api.Contracts;

/// <summary>
/// TR: FakeBank statement endpoint'inde reconciliation amacıyla döndürülen tamamlanmış harici banka hareketini temsil eder.
/// EN: Represents a completed external bank movement returned by the FakeBank statement endpoint for reconciliation purposes.
/// </summary>
public sealed class BankStatementItem
{
    /// <summary>TR: Statement satırını oluşturur. EN: Creates statement item.</summary>
    /// <param name="transactionId">TR: Provider transaction kimliği. EN: Provider transaction identifier.</param>
    /// <param name="type">TR: Deposit/Withdrawal tipi. EN: Deposit/Withdrawal type.</param>
    /// <param name="amount">TR: İşlem tutarı. EN: Transaction amount.</param>
    /// <param name="currency">TR: İşlem currency kodu. EN: Transaction currency code.</param>
    /// <param name="completedAt">TR: Provider tamamlanma UTC zamanı. EN: Provider UTC completion time.</param>
    public BankStatementItem(Guid transactionId, FakeBankTransactionType type, decimal amount, string currency, DateTimeOffset completedAt)
    {
        TransactionId = transactionId;
        Type = type;
        Amount = amount;
        Currency = currency;
        CompletedAt = completedAt;
    }

    /// <summary>TR: Provider transaction kimliğini döndürür. EN: Gets provider transaction identifier.</summary>
    public Guid TransactionId { get; }

    /// <summary>TR: Para hareketi tipini döndürür. EN: Gets money-movement type.</summary>
    public FakeBankTransactionType Type { get; }

    /// <summary>TR: İşlem tutarını döndürür. EN: Gets transaction amount.</summary>
    public decimal Amount { get; }

    /// <summary>TR: İşlem currency kodunu döndürür. EN: Gets transaction currency code.</summary>
    public string Currency { get; }

    /// <summary>TR: Provider tamamlanma UTC zamanını döndürür. EN: Gets provider UTC completion time.</summary>
    public DateTimeOffset CompletedAt { get; }
}
