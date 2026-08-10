using FinWallet.Domain.Fraud;

namespace FinWallet.Application.Fraud;

/// <summary>TR: FraudEvent'in manual-review lifecycle state'ini MSSQL schema numeric değerleriyle tanımlar. EN: Defines FraudEvent manual-review lifecycle state using numeric values aligned with MSSQL schema.</summary>
public enum FraudReviewState
{
    /// <summary>TR: İnsan/operasyon kararı bekleniyor. EN: Awaiting human/operational decision.</summary>
    Pending = 1,
    /// <summary>TR: Internal reviewer işlemi onayladı. EN: Internal reviewer approved the operation.</summary>
    Approved = 2,
    /// <summary>TR: Internal reviewer işlemi reddetti. EN: Internal reviewer denied the operation.</summary>
    Denied = 3,
    /// <summary>TR: Allow/Deny kararı manual review gerektirmeden kapanmıştır. EN: Allow/Deny decision closed without manual review.</summary>
    NotRequired = 4
}

/// <summary>TR: Durable fraud evaluation/review snapshot'ını taşır. EN: Carries durable fraud-evaluation/review snapshot.</summary>
public sealed class FraudEventRecord
{
    /// <summary>TR: Fraud event snapshot oluşturur. EN: Creates fraud-event snapshot.</summary>
    public FraudEventRecord(Guid id, Guid customerId, string operation, string idempotencyKey, string requestHash, FraudDecision internalDecision, FraudDecision? externalDecision, FraudDecision finalDecision, IReadOnlyCollection<string> reasonCodes, FraudReviewState reviewState, DateTimeOffset createdAt, DateTimeOffset? reviewedAt, string? reviewedBy)
    {
        Id = id; CustomerId = customerId; Operation = operation; IdempotencyKey = idempotencyKey; RequestHash = requestHash; InternalDecision = internalDecision; ExternalDecision = externalDecision; FinalDecision = finalDecision; ReasonCodes = reasonCodes; ReviewState = reviewState; CreatedAt = createdAt; ReviewedAt = reviewedAt; ReviewedBy = reviewedBy;
    }

    /// <summary>TR: FraudEvent kimliğini döndürür. EN: Gets FraudEvent identifier.</summary>
    public Guid Id { get; }
    /// <summary>TR: Customer kimliğini döndürür. EN: Gets customer identifier.</summary>
    public Guid CustomerId { get; }
    /// <summary>TR: Operation adını döndürür. EN: Gets operation name.</summary>
    public string Operation { get; }
    /// <summary>TR: Financial command idempotency anahtarını döndürür. EN: Gets financial-command idempotency key.</summary>
    public string IdempotencyKey { get; }
    /// <summary>TR: Canonical request SHA-256 hash'ini döndürür. EN: Gets canonical-request SHA-256 hash.</summary>
    public string RequestHash { get; }
    /// <summary>TR: Internal fraud kararını döndürür. EN: Gets internal fraud decision.</summary>
    public FraudDecision InternalDecision { get; }
    /// <summary>TR: External fraud kararını veya null değerini döndürür. EN: Gets external fraud decision or null.</summary>
    public FraudDecision? ExternalDecision { get; }
    /// <summary>TR: Birleşik fraud kararını döndürür. EN: Gets combined fraud decision.</summary>
    public FraudDecision FinalDecision { get; }
    /// <summary>TR: Normalize reason code koleksiyonunu döndürür. EN: Gets normalized reason-code collection.</summary>
    public IReadOnlyCollection<string> ReasonCodes { get; }
    /// <summary>TR: Manual-review state'ini döndürür. EN: Gets manual-review state.</summary>
    public FraudReviewState ReviewState { get; }
    /// <summary>TR: Fraud evaluation UTC zamanını döndürür. EN: Gets fraud-evaluation UTC timestamp.</summary>
    public DateTimeOffset CreatedAt { get; }
    /// <summary>TR: Review UTC zamanını döndürür. EN: Gets review UTC timestamp.</summary>
    public DateTimeOffset? ReviewedAt { get; }
    /// <summary>TR: Internal reviewer/service kimliğini döndürür. EN: Gets internal reviewer/service identifier.</summary>
    public string? ReviewedBy { get; }
}

/// <summary>TR: FraudEvent insert/read/manual-review state'ini MSSQL implementasyonundan ayırır. EN: Decouples FraudEvent insert/read/manual-review state from MSSQL implementation.</summary>
public interface IFraudEventStore
{
    /// <summary>TR: Operation+Customer+IdempotencyKey fraud event'ini bulur; aynı key farklı requestHash ise conflict üretir. EN: Finds fraud event by Operation+Customer+IdempotencyKey and raises conflict when the same key carries a different requestHash.</summary>
    Task<FraudEventRecord?> FindAsync(string operation, Guid customerId, string idempotencyKey, string requestHash, CancellationToken cancellationToken);

    /// <summary>TR: Internal/external/final fraud kararlarını durable olarak kaydeder veya aynı payload concurrency duplicate'ında mevcut kaydı döndürür. EN: Durably records internal/external/final fraud decisions or returns existing row on same-payload concurrency duplicate.</summary>
    Task<FraudEventRecord> SaveAsync(string operation, Guid customerId, string idempotencyKey, string requestHash, FraudDecision internalDecision, FraudDecision? externalDecision, FraudDecision finalDecision, IReadOnlyCollection<string> reasonCodes, DateTimeOffset createdAt, CancellationToken cancellationToken);

    /// <summary>TR: Pending FraudEvent kayıtlarını oldest-first internal operasyon listesi için döndürür. EN: Returns pending FraudEvent records oldest-first for internal operations list.</summary>
    Task<IReadOnlyCollection<FraudEventRecord>> ListPendingAsync(int take, CancellationToken cancellationToken);

    /// <summary>TR: Yalnız Pending FraudEvent'i Approved veya Denied yapar ve reviewer audit bilgisini kalıcılaştırır. EN: Changes only a Pending FraudEvent to Approved or Denied and persists reviewer audit data.</summary>
    Task<FraudEventRecord> ReviewAsync(Guid fraudEventId, bool approve, string reviewedBy, DateTimeOffset reviewedAt, CancellationToken cancellationToken);
}

/// <summary>TR: FraudEvent idempotency key farklı canonical request ile reuse edildiğinde oluşur. EN: Raised when a FraudEvent idempotency key is reused with a different canonical request.</summary>
public sealed class FraudEventIdempotencyConflictException : Exception
{
    /// <summary>TR: Fraud-event idempotency conflict exception oluşturur. EN: Creates fraud-event-idempotency-conflict exception.</summary>
    public FraudEventIdempotencyConflictException() : base("The Idempotency-Key was already used with a different fraud-evaluated request.") { }
}

/// <summary>TR: Manual review hedefi bulunamadığında oluşur. EN: Raised when manual-review target is not found.</summary>
public sealed class FraudEventNotFoundException : Exception
{
    /// <summary>TR: FraudEvent not-found exception oluşturur. EN: Creates FraudEvent-not-found exception.</summary>
    public FraudEventNotFoundException() : base("The fraud review event was not found.") { }
}

/// <summary>TR: Pending olmayan fraud event yeniden review edilmeye çalışıldığında oluşur. EN: Raised when a non-pending fraud event is reviewed again.</summary>
public sealed class FraudEventReviewConflictException : Exception
{
    /// <summary>TR: Fraud review conflict exception oluşturur. EN: Creates fraud-review-conflict exception.</summary>
    public FraudEventReviewConflictException() : base("The fraud event is no longer pending review.") { }
}
