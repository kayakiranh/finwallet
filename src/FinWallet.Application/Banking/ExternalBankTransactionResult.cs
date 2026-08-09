namespace FinWallet.Application.Banking;

/// <summary>
/// TR: Dış bankadaki para hareketinin provider transaction kimliği, durum ve bakiye snapshot sonucunu taşır.
/// EN: Carries provider transaction identifier, state and balance snapshot for an external-bank money movement.
/// </summary>
public sealed class ExternalBankTransactionResult
{
    /// <summary>
    /// TR: Dış banka transaction sonucunu oluşturur.
    /// EN: Creates an external-bank transaction result.
    /// </summary>
    /// <param name="transactionId">TR: Provider transaction kimliği. EN: Provider transaction identifier.</param>
    /// <param name="status">TR: Provider bağımsız transaction durumu. EN: Provider-independent transaction state.</param>
    /// <param name="accountBalance">TR: Provider hesabının yanıt anındaki bakiye snapshot değeri. EN: Provider account balance snapshot at response time.</param>
    public ExternalBankTransactionResult(Guid transactionId, ExternalBankTransactionStatus status, decimal accountBalance)
    {
        if (transactionId == Guid.Empty) throw new ArgumentException("External transaction identifier cannot be empty.", nameof(transactionId));
        TransactionId = transactionId;
        Status = status;
        AccountBalance = accountBalance;
    }

    /// <summary>TR: Provider transaction kimliğini döndürür. EN: Gets the provider transaction identifier.</summary>
    public Guid TransactionId { get; }

    /// <summary>TR: Provider bağımsız transaction durumunu döndürür. EN: Gets the provider-independent transaction state.</summary>
    public ExternalBankTransactionStatus Status { get; }

    /// <summary>TR: Provider account balance snapshot değerini döndürür. EN: Gets the provider account balance snapshot.</summary>
    public decimal AccountBalance { get; }
}
