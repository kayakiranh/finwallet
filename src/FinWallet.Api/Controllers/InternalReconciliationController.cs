using FinWallet.Application.Reconciliation;
using FinWallet.Shared.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace FinWallet.Api.Controllers;

/// <summary>
/// TR: Gateway `InternalService` policy arkasında non-mutating reconciliation run başlatır ve run/issue raporlarını sunar; mismatch bulduğunda finansal state'i otomatik değiştirmez.
/// EN: Starts non-mutating reconciliation runs and exposes run/issue reports behind the Gateway `InternalService` policy; it never automatically changes financial state when mismatches are found.
/// </summary>
[ApiController]
[Route("api/v1/internal/reconciliation")]
public sealed class InternalReconciliationController : ControllerBase
{
    private readonly RunReconciliationHandler _runHandler;
    private readonly GetReconciliationReportHandler _reportHandler;

    /// <summary>TR: Reconciliation run ve report handler bağımlılıklarıyla controller'ı oluşturur. EN: Creates controller with reconciliation-run and report-handler dependencies.</summary>
    /// <param name="runHandler">TR: Non-mutating reconciliation orchestrator'ı. EN: Non-mutating reconciliation orchestrator.</param>
    /// <param name="reportHandler">TR: Durable run/issue query handler'ı. EN: Durable run/issue query handler.</param>
    public InternalReconciliationController(RunReconciliationHandler runHandler,GetReconciliationReportHandler reportHandler)
    {
        _runHandler=runHandler??throw new ArgumentNullException(nameof(runHandler));
        _reportHandler=reportHandler??throw new ArgumentNullException(nameof(reportHandler));
    }

    /// <summary>TR: `WalletLedger`, `BankSettlementLedger` veya `ExternalBankStatement` scope'unda yeni reconciliation run çalıştırır. EN: Runs a new reconciliation in `WalletLedger`, `BankSettlementLedger` or `ExternalBankStatement` scope.</summary>
    /// <param name="scope">TR: Reconciliation scope adı. EN: Reconciliation-scope name.</param>
    /// <param name="cancellationToken">TR: SQL/Bank provider iptal sinyali. EN: SQL/Bank-provider cancellation signal.</param>
    /// <returns>TR: Completed run summary döndürür. EN: Returns completed-run summary.</returns>
    [HttpPost("runs/{scope}")]
    [ProducesResponseType(typeof(ServiceResult<ReconciliationRunResponse>),StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ServiceResult<object>),StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ServiceResult<object>),StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<ServiceResult<ReconciliationRunResponse>>> RunAsync(string scope,CancellationToken cancellationToken)
    {
        if(!Enum.TryParse<ReconciliationScope>(scope,true,out var parsed))
        {
            return BadRequest(ServiceResult<ReconciliationRunResponse>.Failure("INVALID_RECONCILIATION_SCOPE","The reconciliation scope is invalid."));
        }
        var result=await _runHandler.HandleAsync(parsed,cancellationToken);
        return Ok(ServiceResult<ReconciliationRunResponse>.Success(new ReconciliationRunResponse(result),"RECONCILIATION_COMPLETED","Reconciliation completed without mutating financial state."));
    }

    /// <summary>TR: Durable reconciliation run summary getirir. EN: Gets durable reconciliation-run summary.</summary>
    /// <param name="runId">TR: Reconciliation run kimliği. EN: Reconciliation-run identifier.</param>
    /// <param name="cancellationToken">TR: MSSQL query iptal sinyali. EN: MSSQL-query cancellation signal.</param>
    /// <returns>TR: Run summary veya 404 döndürür. EN: Returns run summary or 404.</returns>
    [HttpGet("runs/{runId:guid}")]
    public async Task<ActionResult<ServiceResult<ReconciliationRunResponse>>> GetRunAsync(Guid runId,CancellationToken cancellationToken)
    {
        var run=await _reportHandler.GetRunAsync(runId,cancellationToken);
        return run is null
            ? NotFound(ServiceResult<ReconciliationRunResponse>.Failure("RECONCILIATION_RUN_NOT_FOUND","The reconciliation run was not found."))
            : Ok(ServiceResult<ReconciliationRunResponse>.Success(new ReconciliationRunResponse(run),"RECONCILIATION_RUN_FOUND","Reconciliation run loaded successfully."));
    }

    /// <summary>TR: Belirli run için persisted mismatch issue kayıtlarını listeler. EN: Lists persisted mismatch-issue rows for a run.</summary>
    /// <param name="runId">TR: Reconciliation run kimliği. EN: Reconciliation-run identifier.</param>
    /// <param name="take">TR: 1–500 arası issue limit değeri. EN: Issue limit between 1 and 500.</param>
    /// <param name="cancellationToken">TR: MSSQL query iptal sinyali. EN: MSSQL-query cancellation signal.</param>
    /// <returns>TR: Reconciliation issue report koleksiyonunu döndürür. EN: Returns reconciliation-issue report collection.</returns>
    [HttpGet("runs/{runId:guid}/issues")]
    public async Task<ActionResult<ServiceResult<IReadOnlyCollection<ReconciliationIssueResponse>>>> ListIssuesAsync(Guid runId,[FromQuery]int take=200,CancellationToken cancellationToken=default)
    {
        var issues=await _reportHandler.ListIssuesAsync(runId,take,cancellationToken);
        IReadOnlyCollection<ReconciliationIssueResponse> response=issues.Select(static issue=>new ReconciliationIssueResponse(issue)).ToArray();
        return Ok(ServiceResult<IReadOnlyCollection<ReconciliationIssueResponse>>.Success(response,"RECONCILIATION_ISSUES_LISTED","Reconciliation issues listed successfully."));
    }
}

/// <summary>TR: Internal reconciliation run summary response modelidir. EN: Internal reconciliation-run summary response model.</summary>
public sealed class ReconciliationRunResponse
{
    /// <summary>TR: Application run sonucunu API response'a dönüştürür. EN: Converts Application run result into API response.</summary>
    public ReconciliationRunResponse(ReconciliationRunResult result){RunId=result.RunId;Scope=result.Scope.ToString();Status=result.Status.ToString();IssueCount=result.IssueCount;StartedAt=result.StartedAt;CompletedAt=result.CompletedAt;}
    /// <summary>TR: Run kimliğini döndürür. EN: Gets run identifier.</summary>
    public Guid RunId{get;}
    /// <summary>TR: Scope adını döndürür. EN: Gets scope name.</summary>
    public string Scope{get;}
    /// <summary>TR: Run status adını döndürür. EN: Gets run-status name.</summary>
    public string Status{get;}
    /// <summary>TR: Issue sayısını döndürür. EN: Gets issue count.</summary>
    public int IssueCount{get;}
    /// <summary>TR: Start UTC zamanını döndürür. EN: Gets start UTC timestamp.</summary>
    public DateTimeOffset StartedAt{get;}
    /// <summary>TR: Completion UTC zamanını döndürür. EN: Gets completion UTC timestamp.</summary>
    public DateTimeOffset? CompletedAt{get;}
}

/// <summary>TR: Internal reconciliation mismatch issue response modelidir; PII içermez. EN: Internal reconciliation mismatch-issue response model; it contains no PII.</summary>
public sealed class ReconciliationIssueResponse
{
    /// <summary>TR: Reconciliation issue record'ını API response'a dönüştürür. EN: Converts reconciliation-issue record into API response.</summary>
    public ReconciliationIssueResponse(ReconciliationIssueRecord issue){Id=issue.Id;Type=issue.Type.ToString();TransactionId=issue.TransactionId;WalletId=issue.WalletId;BankAccountId=issue.BankAccountId;ExternalTransactionId=issue.ExternalTransactionId;Currency=issue.Currency?.ToString();ExpectedAmount=issue.ExpectedAmount;ActualAmount=issue.ActualAmount;Details=issue.Details;CreatedAt=issue.CreatedAt;ResolvedAt=issue.ResolvedAt;}
    /// <summary>TR: Issue kimliğini döndürür. EN: Gets issue identifier.</summary>
    public Guid Id{get;}
    /// <summary>TR: Issue type adını döndürür. EN: Gets issue-type name.</summary>
    public string Type{get;}
    /// <summary>TR: Internal transaction kimliğini döndürür. EN: Gets internal transaction identifier.</summary>
    public Guid? TransactionId{get;}
    /// <summary>TR: Wallet kimliğini döndürür. EN: Gets wallet identifier.</summary>
    public Guid? WalletId{get;}
    /// <summary>TR: BankAccount kimliğini döndürür. EN: Gets BankAccount identifier.</summary>
    public Guid? BankAccountId{get;}
    /// <summary>TR: External transaction kimliğini döndürür. EN: Gets external-transaction identifier.</summary>
    public Guid? ExternalTransactionId{get;}
    /// <summary>TR: Currency kodunu döndürür. EN: Gets currency code.</summary>
    public string? Currency{get;}
    /// <summary>TR: Expected amount değerini döndürür. EN: Gets expected amount.</summary>
    public decimal? ExpectedAmount{get;}
    /// <summary>TR: Observed amount değerini döndürür. EN: Gets observed amount.</summary>
    public decimal? ActualAmount{get;}
    /// <summary>TR: PII içermeyen mismatch açıklamasını döndürür. EN: Gets mismatch description containing no PII.</summary>
    public string Details{get;}
    /// <summary>TR: Issue creation UTC zamanını döndürür. EN: Gets issue-creation UTC timestamp.</summary>
    public DateTimeOffset CreatedAt{get;}
    /// <summary>TR: Manual resolution UTC zamanını döndürür. EN: Gets manual-resolution UTC timestamp.</summary>
    public DateTimeOffset? ResolvedAt{get;}
}
