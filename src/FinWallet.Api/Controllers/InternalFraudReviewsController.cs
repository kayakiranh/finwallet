using FinWallet.Application.Fraud;
using FinWallet.Shared.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace FinWallet.Api.Controllers;

/// <summary>
/// TR: Normal customer/admin kullanıcı tipi oluşturmadan, Gateway `InternalService` policy arkasında pending FraudEvent operasyonlarını listeler ve approve/deny eder.
/// EN: Lists and approves/denies pending FraudEvent operations behind the Gateway `InternalService` policy without introducing a normal customer/admin user type.
/// </summary>
[ApiController]
[Route("api/v1/internal/fraud-reviews")]
public sealed class InternalFraudReviewsController : ControllerBase
{
    private readonly IFraudEventStore _fraudEventStore;
    private readonly TimeProvider _timeProvider;

    /// <summary>TR: Fraud event store ve UTC zaman kaynağıyla internal review controller oluşturur. EN: Creates internal-review controller with fraud-event store and UTC time source.</summary>
    /// <param name="fraudEventStore">TR: Durable fraud-review store. EN: Durable fraud-review store.</param>
    /// <param name="timeProvider">TR: Review audit UTC zaman kaynağı. EN: Review-audit UTC time source.</param>
    public InternalFraudReviewsController(IFraudEventStore fraudEventStore, TimeProvider timeProvider)
    {
        _fraudEventStore = fraudEventStore ?? throw new ArgumentNullException(nameof(fraudEventStore));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <summary>TR: Pending FraudEvent kayıtlarını oldest-first listeler; request hash veya secret değerleri response'a koymaz. EN: Lists pending FraudEvent records oldest-first without exposing request hashes or secrets.</summary>
    /// <param name="take">TR: 1–100 arası batch boyutu. EN: Batch size between 1 and 100.</param>
    /// <param name="cancellationToken">TR: MSSQL query iptal sinyali. EN: MSSQL-query cancellation signal.</param>
    /// <returns>TR: Pending review item koleksiyonunu döndürür. EN: Returns pending-review item collection.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(ServiceResult<IReadOnlyCollection<FraudReviewResponse>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ServiceResult<IReadOnlyCollection<FraudReviewResponse>>>> ListAsync([FromQuery] int take = 50, CancellationToken cancellationToken = default)
    {
        if (take < 1 || take > 100) return BadRequest(ServiceResult<IReadOnlyCollection<FraudReviewResponse>>.Failure("INVALID_PAGE_SIZE", "Take must be between 1 and 100."));
        var events = await _fraudEventStore.ListPendingAsync(take, cancellationToken);
        IReadOnlyCollection<FraudReviewResponse> response = events.Select(static item => new FraudReviewResponse(item)).ToArray();
        return Ok(ServiceResult<IReadOnlyCollection<FraudReviewResponse>>.Success(response, "FRAUD_REVIEWS_LISTED", "Pending fraud reviews listed successfully."));
    }

    /// <summary>TR: Pending FraudEvent'i bir kez approve veya deny eder; reviewer kimliği internal caller tarafından header ile verilmelidir. EN: Approves or denies a Pending FraudEvent once; reviewer identity must be supplied by the internal caller header.</summary>
    /// <param name="fraudEventId">TR: Review edilecek FraudEvent kimliği. EN: FraudEvent identifier to review.</param>
    /// <param name="request">TR: Approve/Deny kararı. EN: Approve/Deny decision.</param>
    /// <param name="reviewerId">TR: Internal operasyon/reviewer kimliği. EN: Internal operation/reviewer identifier.</param>
    /// <param name="cancellationToken">TR: MSSQL transaction iptal sinyali. EN: MSSQL-transaction cancellation signal.</param>
    /// <returns>TR: Final review state'ini döndürür. EN: Returns final review state.</returns>
    [HttpPost("{fraudEventId:guid}/decision")]
    [ProducesResponseType(typeof(ServiceResult<FraudReviewResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ServiceResult<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ServiceResult<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ServiceResult<object>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ServiceResult<FraudReviewResponse>>> ReviewAsync(Guid fraudEventId, [FromBody] FraudReviewDecisionRequest request, [FromHeader(Name = "X-Reviewer-Id")] string? reviewerId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(reviewerId)) return BadRequest(ServiceResult<FraudReviewResponse>.Failure("REVIEWER_REQUIRED", "X-Reviewer-Id header is required."));
        var reviewed = await _fraudEventStore.ReviewAsync(fraudEventId, request.Approve, reviewerId, _timeProvider.GetUtcNow(), cancellationToken);
        return Ok(ServiceResult<FraudReviewResponse>.Success(new FraudReviewResponse(reviewed), request.Approve ? "FRAUD_REVIEW_APPROVED" : "FRAUD_REVIEW_DENIED", request.Approve ? "Fraud review approved." : "Fraud review denied."));
    }
}

/// <summary>TR: Internal fraud review approve/deny request modelidir. EN: Internal fraud-review approve/deny request model.</summary>
public sealed class FraudReviewDecisionRequest
{
    /// <summary>TR: `true` ise approve, `false` ise deny kararını döndürür veya ayarlar. EN: Gets or sets approve when `true`, deny when `false`.</summary>
    public bool Approve { get; init; }
}

/// <summary>TR: Internal pending/final FraudEvent response modelidir; idempotency key/hash veya PII içermez. EN: Internal pending/final FraudEvent response model; it contains no idempotency key/hash or PII.</summary>
public sealed class FraudReviewResponse
{
    /// <summary>TR: FraudEvent kaydını internal API response'a dönüştürür. EN: Converts FraudEvent record into internal API response.</summary>
    /// <param name="record">TR: Durable FraudEvent snapshot. EN: Durable FraudEvent snapshot.</param>
    public FraudReviewResponse(FraudEventRecord record)
    {
        Id = record.Id;
        CustomerId = record.CustomerId;
        Operation = record.Operation;
        InternalDecision = record.InternalDecision.ToString();
        ExternalDecision = record.ExternalDecision?.ToString();
        FinalDecision = record.FinalDecision.ToString();
        ReasonCodes = record.ReasonCodes;
        ReviewState = record.ReviewState.ToString();
        CreatedAt = record.CreatedAt;
        ReviewedAt = record.ReviewedAt;
        ReviewedBy = record.ReviewedBy;
    }

    /// <summary>TR: FraudEvent kimliğini döndürür. EN: Gets FraudEvent identifier.</summary>
    public Guid Id { get; }
    /// <summary>TR: Customer kimliğini döndürür. EN: Gets customer identifier.</summary>
    public Guid CustomerId { get; }
    /// <summary>TR: Operation adını döndürür. EN: Gets operation name.</summary>
    public string Operation { get; }
    /// <summary>TR: Internal fraud kararını döndürür. EN: Gets internal fraud decision.</summary>
    public string InternalDecision { get; }
    /// <summary>TR: External fraud kararını döndürür. EN: Gets external fraud decision.</summary>
    public string? ExternalDecision { get; }
    /// <summary>TR: Final fraud kararını döndürür. EN: Gets final fraud decision.</summary>
    public string FinalDecision { get; }
    /// <summary>TR: Normalize reason code koleksiyonunu döndürür. EN: Gets normalized reason-code collection.</summary>
    public IReadOnlyCollection<string> ReasonCodes { get; }
    /// <summary>TR: Review lifecycle state'ini döndürür. EN: Gets review lifecycle state.</summary>
    public string ReviewState { get; }
    /// <summary>TR: Evaluation UTC zamanını döndürür. EN: Gets evaluation UTC timestamp.</summary>
    public DateTimeOffset CreatedAt { get; }
    /// <summary>TR: Review UTC zamanını döndürür. EN: Gets review UTC timestamp.</summary>
    public DateTimeOffset? ReviewedAt { get; }
    /// <summary>TR: Reviewer/service kimliğini döndürür. EN: Gets reviewer/service identifier.</summary>
    public string? ReviewedBy { get; }
}
