using FinWallet.Domain.Shared;

namespace FinWallet.Application.Reconciliation;

/// <summary>TR: Persist edilmiş tek reconciliation mismatch kaydını internal report read-model olarak taşır. EN: Carries one persisted reconciliation mismatch as an internal report read model.</summary>
public sealed class ReconciliationIssueRecord
{
    /// <summary>TR: Reconciliation issue read-model oluşturur. EN: Creates reconciliation-issue read model.</summary>
    public ReconciliationIssueRecord(Guid id, Guid runId, ReconciliationIssueType type, Guid? transactionId, Guid? walletId, Guid? bankAccountId, Guid? externalTransactionId, CurrencyCode? currency, decimal? expectedAmount, decimal? actualAmount, string details, DateTimeOffset createdAt, DateTimeOffset? resolvedAt)
    {
        Id=id;RunId=runId;Type=type;TransactionId=transactionId;WalletId=walletId;BankAccountId=bankAccountId;ExternalTransactionId=externalTransactionId;Currency=currency;ExpectedAmount=expectedAmount;ActualAmount=actualAmount;Details=details;CreatedAt=createdAt;ResolvedAt=resolvedAt;
    }
    /// <summary>TR: Issue kimliğini döndürür. EN: Gets issue identifier.</summary>
    public Guid Id { get; }
    /// <summary>TR: Run kimliğini döndürür. EN: Gets run identifier.</summary>
    public Guid RunId { get; }
    /// <summary>TR: Issue tipini döndürür. EN: Gets issue type.</summary>
    public ReconciliationIssueType Type { get; }
    /// <summary>TR: Internal transaction kimliğini döndürür. EN: Gets internal transaction identifier.</summary>
    public Guid? TransactionId { get; }
    /// <summary>TR: Wallet kimliğini döndürür. EN: Gets wallet identifier.</summary>
    public Guid? WalletId { get; }
    /// <summary>TR: BankAccount kimliğini döndürür. EN: Gets BankAccount identifier.</summary>
    public Guid? BankAccountId { get; }
    /// <summary>TR: External transaction kimliğini döndürür. EN: Gets external transaction identifier.</summary>
    public Guid? ExternalTransactionId { get; }
    /// <summary>TR: Currency değerini döndürür. EN: Gets currency.</summary>
    public CurrencyCode? Currency { get; }
    /// <summary>TR: Beklenen amount değerini döndürür. EN: Gets expected amount.</summary>
    public decimal? ExpectedAmount { get; }
    /// <summary>TR: Observed amount değerini döndürür. EN: Gets observed amount.</summary>
    public decimal? ActualAmount { get; }
    /// <summary>TR: PII içermeyen mismatch açıklamasını döndürür. EN: Gets mismatch description containing no PII.</summary>
    public string Details { get; }
    /// <summary>TR: Issue creation UTC zamanını döndürür. EN: Gets issue creation UTC timestamp.</summary>
    public DateTimeOffset CreatedAt { get; }
    /// <summary>TR: Manual resolution UTC zamanını döndürür. EN: Gets manual-resolution UTC timestamp.</summary>
    public DateTimeOffset? ResolvedAt { get; }
}

/// <summary>TR: Reconciliation run/issue report read-side'ını MSSQL implementasyonundan ayırır. EN: Decouples reconciliation run/issue report read side from MSSQL implementation.</summary>
public interface IReconciliationQueryStore
{
    /// <summary>TR: Run kimliğiyle reconciliation summary getirir. EN: Gets reconciliation summary by run identifier.</summary>
    Task<ReconciliationRunResult?> GetRunAsync(Guid runId, CancellationToken cancellationToken);
    /// <summary>TR: Belirli run issue kayıtlarını oldest-first ve 1–500 limit ile listeler. EN: Lists issue rows for a run oldest-first with a limit between 1 and 500.</summary>
    Task<IReadOnlyCollection<ReconciliationIssueRecord>> ListIssuesAsync(Guid runId, int take, CancellationToken cancellationToken);
}

/// <summary>TR: Internal reconciliation report use-case'lerini uygular. EN: Implements internal reconciliation-report use cases.</summary>
public sealed class GetReconciliationReportHandler
{
    private readonly IReconciliationQueryStore _store;
    /// <summary>TR: Query store bağımlılığıyla report handler oluşturur. EN: Creates report handler with query-store dependency.</summary>
    public GetReconciliationReportHandler(IReconciliationQueryStore store)=>_store=store??throw new ArgumentNullException(nameof(store));
    /// <summary>TR: Run summary getirir. EN: Gets run summary.</summary>
    public Task<ReconciliationRunResult?> GetRunAsync(Guid runId,CancellationToken cancellationToken)=>_store.GetRunAsync(runId,cancellationToken);
    /// <summary>TR: Run issue listesi getirir. EN: Gets run issue list.</summary>
    public Task<IReadOnlyCollection<ReconciliationIssueRecord>> ListIssuesAsync(Guid runId,int take,CancellationToken cancellationToken)
    {
        if(take<1||take>500)throw new ArgumentOutOfRangeException(nameof(take));
        return _store.ListIssuesAsync(runId,take,cancellationToken);
    }
}
