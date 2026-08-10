using FinWallet.Application.Banking;
using FinWallet.Domain.Shared;
using FinWallet.Domain.Transactions;

namespace FinWallet.Application.Reconciliation;

/// <summary>TR: Durable reconciliation run kapsamını MSSQL schema numeric değerleriyle tanımlar. EN: Defines durable reconciliation-run scope using numeric values aligned with MSSQL schema.</summary>
public enum ReconciliationScope
{
    /// <summary>TR: Wallet Available+Blocked ile wallet-liability ledger-derived bakiyeyi karşılaştırır. EN: Compares Wallet Available+Blocked with wallet-liability ledger-derived balance.</summary>
    WalletLedger = 1,
    /// <summary>TR: Completed internal bank transactions ile BANK-SETTLEMENT ledger posting'lerini karşılaştırır. EN: Compares completed internal bank transactions with BANK-SETTLEMENT ledger postings.</summary>
    BankSettlementLedger = 2,
    /// <summary>TR: FinWallet completed bank transactions ile provider statement satırlarını karşılaştırır. EN: Compares FinWallet completed bank transactions with provider statement rows.</summary>
    ExternalBankStatement = 3
}

/// <summary>TR: Reconciliation run lifecycle state'ini tanımlar. EN: Defines reconciliation-run lifecycle state.</summary>
public enum ReconciliationRunStatus
{
    /// <summary>TR: Run çalışıyor. EN: Run is executing.</summary>
    Running = 1,
    /// <summary>TR: Run başarıyla tamamlandı. EN: Run completed successfully.</summary>
    Completed = 2,
    /// <summary>TR: Run dependency/technical hata nedeniyle tamamlanamadı. EN: Run failed due to dependency/technical failure.</summary>
    Failed = 3
}

/// <summary>TR: Reconciliation mismatch tiplerini MSSQL schema ile stabil numeric değerlerle tanımlar. EN: Defines reconciliation mismatch types with stable numeric values aligned with MSSQL schema.</summary>
public enum ReconciliationIssueType
{
    /// <summary>TR: Provider'da hareket var fakat FinWallet internal kaydı yok. EN: Provider movement exists but FinWallet internal record is missing.</summary>
    MissingInternal = 1,
    /// <summary>TR: FinWallet internal tamamlanmış kayıt var fakat provider/ledger karşılığı yok. EN: FinWallet completed internal record exists but provider/ledger counterpart is missing.</summary>
    MissingExternal = 2,
    /// <summary>TR: Eşleşen referanslarda amount farklı. EN: Matching references have different amounts.</summary>
    AmountMismatch = 3,
    /// <summary>TR: Eşleşen referanslarda currency farklı. EN: Matching references have different currencies.</summary>
    CurrencyMismatch = 4,
    /// <summary>TR: Aynı external/reference hareket birden fazla bulundu. EN: The same external/reference movement appears more than once.</summary>
    Duplicate = 5,
    /// <summary>TR: Deposit/withdrawal veya debit/credit yönü beklenenin tersidir. EN: Deposit/withdrawal or debit/credit direction is opposite to expectation.</summary>
    DirectionMismatch = 6,
    /// <summary>TR: Wallet current balance ile wallet-liability ledger-derived balance farklı. EN: Wallet current balance differs from wallet-liability ledger-derived balance.</summary>
    WalletLedgerMismatch = 7
}

/// <summary>TR: Reconciliation issue yazımı için provider/domain bağımsız veri taşır. EN: Carries provider/domain-independent data for reconciliation-issue persistence.</summary>
public sealed class ReconciliationIssueCandidate
{
    /// <summary>TR: Reconciliation issue candidate oluşturur. EN: Creates reconciliation-issue candidate.</summary>
    public ReconciliationIssueCandidate(ReconciliationIssueType type, Guid? transactionId, Guid? walletId, Guid? bankAccountId, Guid? externalTransactionId, CurrencyCode? currency, decimal? expectedAmount, decimal? actualAmount, string details)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(details);
        Type = type; TransactionId = transactionId; WalletId = walletId; BankAccountId = bankAccountId; ExternalTransactionId = externalTransactionId; Currency = currency; ExpectedAmount = expectedAmount; ActualAmount = actualAmount; Details = details.Trim();
    }

    /// <summary>TR: Issue tipini döndürür. EN: Gets issue type.</summary>
    public ReconciliationIssueType Type { get; }
    /// <summary>TR: Internal transaction kimliğini döndürür. EN: Gets internal transaction identifier.</summary>
    public Guid? TransactionId { get; }
    /// <summary>TR: Wallet kimliğini döndürür. EN: Gets wallet identifier.</summary>
    public Guid? WalletId { get; }
    /// <summary>TR: BankAccount kimliğini döndürür. EN: Gets BankAccount identifier.</summary>
    public Guid? BankAccountId { get; }
    /// <summary>TR: Provider transaction kimliğini döndürür. EN: Gets provider transaction identifier.</summary>
    public Guid? ExternalTransactionId { get; }
    /// <summary>TR: İlgili currency değerini döndürür. EN: Gets related currency.</summary>
    public CurrencyCode? Currency { get; }
    /// <summary>TR: Beklenen amount değerini döndürür. EN: Gets expected amount.</summary>
    public decimal? ExpectedAmount { get; }
    /// <summary>TR: Gerçek/observed amount değerini döndürür. EN: Gets actual/observed amount.</summary>
    public decimal? ActualAmount { get; }
    /// <summary>TR: PII içermeyen kısa mismatch açıklamasını döndürür. EN: Gets short mismatch description containing no PII.</summary>
    public string Details { get; }
}

/// <summary>TR: External bank statement reconciliation için active linked account context'ini taşır. EN: Carries active linked-account context for external-bank statement reconciliation.</summary>
public sealed class ReconciliationBankAccount
{
    /// <summary>TR: Bank reconciliation account context oluşturur. EN: Creates bank-reconciliation account context.</summary>
    public ReconciliationBankAccount(Guid bankAccountId, Guid externalAccountId, CurrencyCode currency)
    {
        BankAccountId = bankAccountId; ExternalAccountId = externalAccountId; Currency = currency;
    }

    /// <summary>TR: Internal BankAccount kimliğini döndürür. EN: Gets internal BankAccount identifier.</summary>
    public Guid BankAccountId { get; }
    /// <summary>TR: Provider account kimliğini döndürür. EN: Gets provider-account identifier.</summary>
    public Guid ExternalAccountId { get; }
    /// <summary>TR: Account currency'sini döndürür. EN: Gets account currency.</summary>
    public CurrencyCode Currency { get; }
}

/// <summary>TR: External statement ile karşılaştırılacak completed FinWallet bank transaction read-model'ini taşır. EN: Carries completed FinWallet bank-transaction read model to compare with external statement.</summary>
public sealed class ReconciliationBankMovement
{
    /// <summary>TR: Internal bank movement snapshot oluşturur. EN: Creates internal bank-movement snapshot.</summary>
    public ReconciliationBankMovement(Guid transactionId, Guid bankAccountId, Guid externalTransactionId, FinancialTransactionType type, Money amount)
    {
        TransactionId = transactionId; BankAccountId = bankAccountId; ExternalTransactionId = externalTransactionId; Type = type; Amount = amount;
    }

    /// <summary>TR: Internal FinancialTransaction kimliğini döndürür. EN: Gets internal FinancialTransaction identifier.</summary>
    public Guid TransactionId { get; }
    /// <summary>TR: Internal BankAccount kimliğini döndürür. EN: Gets internal BankAccount identifier.</summary>
    public Guid BankAccountId { get; }
    /// <summary>TR: Provider transaction kimliğini döndürür. EN: Gets provider transaction identifier.</summary>
    public Guid ExternalTransactionId { get; }
    /// <summary>TR: FinWallet BankDeposit/BankWithdrawal tipini döndürür. EN: Gets FinWallet BankDeposit/BankWithdrawal type.</summary>
    public FinancialTransactionType Type { get; }
    /// <summary>TR: Currency-aware amount değerini döndürür. EN: Gets currency-aware amount.</summary>
    public Money Amount { get; }
}

/// <summary>TR: Completed reconciliation run sonucunu taşır. EN: Carries completed reconciliation-run result.</summary>
public sealed class ReconciliationRunResult
{
    /// <summary>TR: Reconciliation run result oluşturur. EN: Creates reconciliation-run result.</summary>
    public ReconciliationRunResult(Guid runId, ReconciliationScope scope, ReconciliationRunStatus status, int issueCount, DateTimeOffset startedAt, DateTimeOffset? completedAt)
    {
        RunId = runId; Scope = scope; Status = status; IssueCount = issueCount; StartedAt = startedAt; CompletedAt = completedAt;
    }

    /// <summary>TR: Run kimliğini döndürür. EN: Gets run identifier.</summary>
    public Guid RunId { get; }
    /// <summary>TR: Reconciliation scope değerini döndürür. EN: Gets reconciliation scope.</summary>
    public ReconciliationScope Scope { get; }
    /// <summary>TR: Run status değerini döndürür. EN: Gets run status.</summary>
    public ReconciliationRunStatus Status { get; }
    /// <summary>TR: Bulunan issue sayısını döndürür. EN: Gets discovered issue count.</summary>
    public int IssueCount { get; }
    /// <summary>TR: Start UTC zamanını döndürür. EN: Gets start UTC timestamp.</summary>
    public DateTimeOffset StartedAt { get; }
    /// <summary>TR: Completion UTC zamanını döndürür. EN: Gets completion UTC timestamp.</summary>
    public DateTimeOffset? CompletedAt { get; }
}

/// <summary>TR: Reconciliation run/issue persistence ve SQL-only checks'i MSSQL implementasyonundan ayırır. EN: Decouples reconciliation run/issue persistence and SQL-only checks from MSSQL implementation.</summary>
public interface IReconciliationStore
{
    /// <summary>TR: Running reconciliation run kaydı oluşturur. EN: Creates a Running reconciliation-run record.</summary>
    Task<ReconciliationRunResult> StartRunAsync(ReconciliationScope scope, DateTimeOffset startedAt, CancellationToken cancellationToken);
    /// <summary>TR: Wallet↔Ledger mismatch'lerini hesaplayıp run'a issue olarak yazar. EN: Calculates Wallet↔Ledger mismatches and writes them as run issues.</summary>
    Task<int> ReconcileWalletLedgerAsync(Guid runId, DateTimeOffset now, CancellationToken cancellationToken);
    /// <summary>TR: Internal completed bank movement↔BANK-SETTLEMENT ledger mismatch'lerini hesaplayıp yazar. EN: Calculates and writes internal completed-bank-movement↔BANK-SETTLEMENT-ledger mismatches.</summary>
    Task<int> ReconcileBankSettlementLedgerAsync(Guid runId, DateTimeOffset now, CancellationToken cancellationToken);
    /// <summary>TR: External statement reconciliation için active linked BankAccount kayıtlarını listeler. EN: Lists active linked BankAccount records for external-statement reconciliation.</summary>
    Task<IReadOnlyCollection<ReconciliationBankAccount>> ListBankAccountsAsync(CancellationToken cancellationToken);
    /// <summary>TR: Belirtilen BankAccount için completed internal bank movements listeler. EN: Lists completed internal bank movements for the specified BankAccount.</summary>
    Task<IReadOnlyCollection<ReconciliationBankMovement>> ListCompletedBankMovementsAsync(Guid bankAccountId, CancellationToken cancellationToken);
    /// <summary>TR: In-memory external comparison sonucu üretilen issue candidate'ları durable run'a yazar. EN: Persists issue candidates produced by in-memory external comparison into the durable run.</summary>
    Task SaveIssuesAsync(Guid runId, IReadOnlyCollection<ReconciliationIssueCandidate> issues, DateTimeOffset now, CancellationToken cancellationToken);
    /// <summary>TR: Run'ı Completed ve final issue count ile finalize eder. EN: Finalizes run as Completed with final issue count.</summary>
    Task<ReconciliationRunResult> CompleteRunAsync(Guid runId, int issueCount, DateTimeOffset completedAt, CancellationToken cancellationToken);
    /// <summary>TR: Dependency/technical hata alan run'ı Failed olarak finalize eder; issue state'ini otomatik düzeltmez. EN: Finalizes a dependency/technical-failure run as Failed without automatically repairing any issue state.</summary>
    Task<ReconciliationRunResult> FailRunAsync(Guid runId, DateTimeOffset completedAt, CancellationToken cancellationToken);
}

/// <summary>TR: Wallet/ledger/bank reconciliation scope'larını çalıştıran non-mutating application orchestrator'dır. EN: Non-mutating application orchestrator running wallet/ledger/bank reconciliation scopes.</summary>
public sealed class RunReconciliationHandler
{
    private readonly IReconciliationStore _store;
    private readonly IBankProvider _bankProvider;
    private readonly TimeProvider _timeProvider;

    /// <summary>TR: Reconciliation store, bank provider ve UTC zaman kaynağıyla handler'ı oluşturur. EN: Creates handler with reconciliation store, bank provider and UTC time source.</summary>
    public RunReconciliationHandler(IReconciliationStore store, IBankProvider bankProvider, TimeProvider timeProvider)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _bankProvider = bankProvider ?? throw new ArgumentNullException(nameof(bankProvider));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <summary>TR: İstenen reconciliation scope'u çalıştırır ve mismatch'leri yalnız raporlar; finansal state'e correction yazmaz. EN: Runs requested reconciliation scope and only reports mismatches; it writes no correction to financial state.</summary>
    public async Task<ReconciliationRunResult> HandleAsync(ReconciliationScope scope, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(scope)) throw new ArgumentOutOfRangeException(nameof(scope));
        var started = _timeProvider.GetUtcNow();
        var run = await _store.StartRunAsync(scope, started, cancellationToken);
        try
        {
            var issueCount = scope switch
            {
                ReconciliationScope.WalletLedger => await _store.ReconcileWalletLedgerAsync(run.RunId, _timeProvider.GetUtcNow(), cancellationToken),
                ReconciliationScope.BankSettlementLedger => await _store.ReconcileBankSettlementLedgerAsync(run.RunId, _timeProvider.GetUtcNow(), cancellationToken),
                ReconciliationScope.ExternalBankStatement => await ReconcileExternalBankAsync(run.RunId, cancellationToken),
                _ => throw new ArgumentOutOfRangeException(nameof(scope))
            };
            return await _store.CompleteRunAsync(run.RunId, issueCount, _timeProvider.GetUtcNow(), cancellationToken);
        }
        catch
        {
            await _store.FailRunAsync(run.RunId, _timeProvider.GetUtcNow(), CancellationToken.None);
            throw;
        }
    }

    private async Task<int> ReconcileExternalBankAsync(Guid runId, CancellationToken cancellationToken)
    {
        var allIssues = new List<ReconciliationIssueCandidate>();
        var accounts = await _store.ListBankAccountsAsync(cancellationToken);
        foreach (var account in accounts)
        {
            var internalMovements = await _store.ListCompletedBankMovementsAsync(account.BankAccountId, cancellationToken);
            var statement = await _bankProvider.GetStatementAsync(account.ExternalAccountId, $"recon-{runId:N}", cancellationToken);
            allIssues.AddRange(Compare(account, internalMovements, statement));
        }
        await _store.SaveIssuesAsync(runId, allIssues, _timeProvider.GetUtcNow(), cancellationToken);
        return allIssues.Count;
    }

    private static IReadOnlyCollection<ReconciliationIssueCandidate> Compare(ReconciliationBankAccount account, IReadOnlyCollection<ReconciliationBankMovement> internalMovements, IReadOnlyCollection<ExternalBankStatementItem> statement)
    {
        var issues = new List<ReconciliationIssueCandidate>();
        var internalByExternal = internalMovements.GroupBy(static item => item.ExternalTransactionId).ToDictionary(static group => group.Key, static group => group.ToArray());
        var externalById = statement.GroupBy(static item => item.TransactionId).ToDictionary(static group => group.Key, static group => group.ToArray());

        foreach (var duplicate in internalByExternal.Where(static pair => pair.Value.Length > 1))
        {
            issues.Add(new ReconciliationIssueCandidate(ReconciliationIssueType.Duplicate, duplicate.Value[0].TransactionId, null, account.BankAccountId, duplicate.Key, account.Currency, null, null, "Duplicate internal mapping for one external transaction reference."));
        }
        foreach (var duplicate in externalById.Where(static pair => pair.Value.Length > 1))
        {
            issues.Add(new ReconciliationIssueCandidate(ReconciliationIssueType.Duplicate, null, null, account.BankAccountId, duplicate.Key, account.Currency, null, null, "Duplicate external statement transaction reference."));
        }

        foreach (var movement in internalMovements)
        {
            if (!externalById.TryGetValue(movement.ExternalTransactionId, out var matching) || matching.Length == 0)
            {
                issues.Add(new ReconciliationIssueCandidate(ReconciliationIssueType.MissingExternal, movement.TransactionId, null, account.BankAccountId, movement.ExternalTransactionId, movement.Amount.Currency, movement.Amount.Amount, null, "Completed FinWallet bank movement is missing from provider statement."));
                continue;
            }
            var external = matching[0];
            if (external.Currency != movement.Amount.Currency)
            {
                issues.Add(new ReconciliationIssueCandidate(ReconciliationIssueType.CurrencyMismatch, movement.TransactionId, null, account.BankAccountId, movement.ExternalTransactionId, movement.Amount.Currency, movement.Amount.Amount, external.Amount, "Internal and external bank movement currencies differ."));
            }
            if (external.Amount != movement.Amount.Amount)
            {
                issues.Add(new ReconciliationIssueCandidate(ReconciliationIssueType.AmountMismatch, movement.TransactionId, null, account.BankAccountId, movement.ExternalTransactionId, movement.Amount.Currency, movement.Amount.Amount, external.Amount, "Internal and external bank movement amounts differ."));
            }
            var expectedProviderDirection = movement.Type == FinancialTransactionType.BankDeposit ? BankMoneyMovementType.Withdrawal : BankMoneyMovementType.Deposit;
            if (external.TransactionType != expectedProviderDirection)
            {
                issues.Add(new ReconciliationIssueCandidate(ReconciliationIssueType.DirectionMismatch, movement.TransactionId, null, account.BankAccountId, movement.ExternalTransactionId, movement.Amount.Currency, movement.Amount.Amount, external.Amount, "Provider statement direction is inconsistent with FinWallet bank-movement direction."));
            }
        }

        foreach (var external in statement)
        {
            if (!internalByExternal.ContainsKey(external.TransactionId))
            {
                issues.Add(new ReconciliationIssueCandidate(ReconciliationIssueType.MissingInternal, null, null, account.BankAccountId, external.TransactionId, external.Currency, null, external.Amount, "Provider statement movement has no completed FinWallet bank transaction."));
            }
        }
        return issues;
    }
}
