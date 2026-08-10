using FinWallet.Domain.Shared;
using FinWallet.Domain.Transactions;

namespace FinWallet.Application.Corrections;

/// <summary>TR: Public correction komutunun Refund veya güvenli internal Reversal tipini belirtir. EN: Identifies Refund or safe internal Reversal type of a public correction command.</summary>
public enum TransactionCorrectionType
{
    /// <summary>TR: Completed Purchase işlemini tam iade eder. EN: Fully refunds a completed Purchase.</summary>
    Refund = 1,
    /// <summary>TR: Completed internal WalletTransfer işlemini ters kayıtla geri alır. EN: Reverses a completed internal WalletTransfer with opposite entries.</summary>
    Reversal = 2
}

/// <summary>TR: Authenticated correction command'ını taşır. EN: Carries authenticated correction command.</summary>
public sealed class TransactionCorrectionCommand
{
    /// <summary>TR: Correction command oluşturur. EN: Creates correction command.</summary>
    /// <param name="customerId">TR: Authenticated customer kimliği. EN: Authenticated customer identifier.</param>
    /// <param name="originalTransactionId">TR: Düzeltilecek completed transaction kimliği. EN: Completed transaction identifier to correct.</param>
    /// <param name="type">TR: Refund veya Reversal tipi. EN: Refund or Reversal type.</param>
    /// <param name="idempotencyKey">TR: Durable idempotency anahtarı. EN: Durable-idempotency key.</param>
    /// <param name="correlationId">TR: Correlation kimliği. EN: Correlation identifier.</param>
    public TransactionCorrectionCommand(Guid customerId, Guid originalTransactionId, TransactionCorrectionType type, string idempotencyKey, string correlationId)
    {
        if (customerId == Guid.Empty || originalTransactionId == Guid.Empty) throw new ArgumentException("Correction identifiers cannot be empty.");
        if (!Enum.IsDefined(type)) throw new ArgumentOutOfRangeException(nameof(type));
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        CustomerId = customerId;
        OriginalTransactionId = originalTransactionId;
        Type = type;
        IdempotencyKey = idempotencyKey.Trim();
        CorrelationId = correlationId.Trim();
    }

    /// <summary>TR: Authenticated customer kimliğini döndürür. EN: Gets authenticated customer identifier.</summary>
    public Guid CustomerId { get; }
    /// <summary>TR: Original transaction kimliğini döndürür. EN: Gets original transaction identifier.</summary>
    public Guid OriginalTransactionId { get; }
    /// <summary>TR: Correction tipini döndürür. EN: Gets correction type.</summary>
    public TransactionCorrectionType Type { get; }
    /// <summary>TR: Durable idempotency key'i döndürür. EN: Gets durable idempotency key.</summary>
    public string IdempotencyKey { get; }
    /// <summary>TR: Correlation kimliğini döndürür. EN: Gets correlation identifier.</summary>
    public string CorrelationId { get; }
}

/// <summary>TR: Completed Refund/Reversal sonucunu taşır. EN: Carries completed Refund/Reversal result.</summary>
public sealed class TransactionCorrectionResult
{
    /// <summary>TR: Completed correction sonucunu oluşturur. EN: Creates completed correction result.</summary>
    /// <param name="transactionId">TR: Yeni correction FinancialTransaction kimliği. EN: New correction FinancialTransaction identifier.</param>
    /// <param name="originalTransactionId">TR: Düzeltilecek original transaction kimliği. EN: Corrected original transaction identifier.</param>
    /// <param name="type">TR: Correction tipi. EN: Correction type.</param>
    /// <param name="amount">TR: Customer-facing correction tutarı. EN: Customer-facing correction amount.</param>
    /// <param name="completedAt">TR: Correction posting UTC zamanı. EN: Correction posting UTC timestamp.</param>
    /// <param name="wasReplay">TR: Durable replay bilgisidir. EN: Durable replay state.</param>
    public TransactionCorrectionResult(Guid transactionId, Guid originalTransactionId, TransactionCorrectionType type, Money amount, DateTimeOffset completedAt, bool wasReplay)
    {
        TransactionId = transactionId; OriginalTransactionId = originalTransactionId; Type = type; Amount = amount; CompletedAt = completedAt; WasReplay = wasReplay;
    }

    /// <summary>TR: Yeni correction transaction kimliğini döndürür. EN: Gets new correction transaction identifier.</summary>
    public Guid TransactionId { get; }
    /// <summary>TR: Original transaction kimliğini döndürür. EN: Gets original transaction identifier.</summary>
    public Guid OriginalTransactionId { get; }
    /// <summary>TR: Correction tipini döndürür. EN: Gets correction type.</summary>
    public TransactionCorrectionType Type { get; }
    /// <summary>TR: Currency-aware correction tutarını döndürür. EN: Gets currency-aware correction amount.</summary>
    public Money Amount { get; }
    /// <summary>TR: Completion UTC zamanını döndürür. EN: Gets completion UTC timestamp.</summary>
    public DateTimeOffset CompletedAt { get; }
    /// <summary>TR: Durable replay bilgisini döndürür. EN: Gets durable-replay state.</summary>
    public bool WasReplay { get; }
}

/// <summary>TR: Refund/Reversal correction persistence sınırını MSSQL implementasyonundan ayırır. EN: Decouples Refund/Reversal correction persistence boundary from MSSQL implementation.</summary>
public interface ITransactionCorrectionStore
{
    /// <summary>TR: Original transaction'ı lock altında doğrular, wallet etkilerini ve original journal'ın ters kayıtlarını atomik olarak post eder. EN: Validates original transaction under lock and atomically posts wallet effects plus opposite entries of the original journal.</summary>
    /// <param name="command">TR: Authenticated correction command. EN: Authenticated correction command.</param>
    /// <param name="cancellationToken">TR: SQL transaction iptal sinyali. EN: SQL-transaction cancellation signal.</param>
    /// <returns>TR: Completed yeni veya replay correction sonucunu döndürür. EN: Returns completed new or replayed correction result.</returns>
    Task<TransactionCorrectionResult> CorrectAsync(TransactionCorrectionCommand command, CancellationToken cancellationToken);
}

/// <summary>TR: Original transaction bulunamadığında veya customer'a ait olmadığında oluşur. EN: Raised when original transaction is missing or does not belong to customer.</summary>
public sealed class CorrectionTransactionNotFoundException : Exception
{
    /// <summary>TR: Not-found exception oluşturur. EN: Creates not-found exception.</summary>
    public CorrectionTransactionNotFoundException() : base("The original financial transaction was not found.") { }
}

/// <summary>TR: Transaction tipi/state'i requested correction için güvenli olmadığında oluşur. EN: Raised when transaction type/state is not safe for requested correction.</summary>
public sealed class CorrectionNotAllowedException : Exception
{
    /// <summary>TR: Correction-not-allowed exception oluşturur. EN: Creates correction-not-allowed exception.</summary>
    public CorrectionNotAllowedException() : base("The requested correction is not allowed for the original transaction state or type.") { }
}

/// <summary>TR: Correction idempotency key farklı original transaction ile reuse edildiğinde oluşur. EN: Raised when correction idempotency key is reused for a different original transaction.</summary>
public sealed class CorrectionIdempotencyConflictException : Exception
{
    /// <summary>TR: Idempotency conflict exception oluşturur. EN: Creates idempotency-conflict exception.</summary>
    public CorrectionIdempotencyConflictException() : base("The Idempotency-Key was already used with a different correction request.") { }
}

/// <summary>TR: Refund/Reversal use-case'ini aggregate-specific correction store'a delege eder. EN: Delegates Refund/Reversal use case to aggregate-specific correction store.</summary>
public sealed class ExecuteTransactionCorrectionHandler
{
    private readonly ITransactionCorrectionStore _store;

    /// <summary>TR: Correction store bağımlılığıyla handler'ı oluşturur. EN: Creates handler with correction-store dependency.</summary>
    /// <param name="store">TR: Atomic correction persistence sınırı. EN: Atomic correction persistence boundary.</param>
    public ExecuteTransactionCorrectionHandler(ITransactionCorrectionStore store) => _store = store ?? throw new ArgumentNullException(nameof(store));

    /// <summary>TR: Authenticated correction command'ını atomik store üzerinden uygular. EN: Applies authenticated correction command through atomic store.</summary>
    /// <param name="command">TR: Refund/Reversal command. EN: Refund/Reversal command.</param>
    /// <param name="cancellationToken">TR: SQL iptal sinyali. EN: SQL cancellation signal.</param>
    /// <returns>TR: Completed correction sonucunu döndürür. EN: Returns completed correction result.</returns>
    public Task<TransactionCorrectionResult> HandleAsync(TransactionCorrectionCommand command, CancellationToken cancellationToken) => _store.CorrectAsync(command, cancellationToken);
}
