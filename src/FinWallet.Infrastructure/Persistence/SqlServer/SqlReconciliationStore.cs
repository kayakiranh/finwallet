using System.Data;
using FinWallet.Application.Reconciliation;
using FinWallet.Domain.Shared;
using FinWallet.Domain.Transactions;
using Microsoft.Data.SqlClient;

namespace FinWallet.Infrastructure.Persistence.SqlServer;

/// <summary>
/// TR: Reconciliation run/issue state'ini MSSQL üzerinde saklar ve Wallet↔Ledger ile completed bank transaction↔BANK-SETTLEMENT ledger kontrollerini yalnız mismatch raporlayacak biçimde çalıştırır; hiçbir finansal bakiyeyi otomatik değiştirmez.
/// EN: Persists reconciliation run/issue state in MSSQL and performs Wallet↔Ledger plus completed-bank-transaction↔BANK-SETTLEMENT-ledger checks in report-only mode; it never automatically mutates financial balances.
/// </summary>
public sealed class SqlReconciliationStore : IReconciliationStore
{
    private readonly SqlConnectionFactory _connectionFactory;

    /// <summary>TR: Pooled SQL connection factory ile reconciliation store oluşturur. EN: Creates reconciliation store with pooled SQL connection factory.</summary>
    /// <param name="connectionFactory">TR: SQL connection factory. EN: SQL connection factory.</param>
    public SqlReconciliationStore(SqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    /// <inheritdoc />
    public async Task<ReconciliationRunResult> StartRunAsync(ReconciliationScope scope, DateTimeOffset startedAt, CancellationToken cancellationToken)
    {
        var id = Guid.NewGuid();
        const string sql = "INSERT INTO dbo.ReconciliationRuns (Id,Scope,Status,StartedAt,CompletedAt,IssueCount) VALUES (@Id,@Scope,@Status,@StartedAt,NULL,0);";
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = id;
        command.Parameters.Add("@Scope", SqlDbType.TinyInt).Value = (byte)scope;
        command.Parameters.Add("@Status", SqlDbType.TinyInt).Value = (byte)ReconciliationRunStatus.Running;
        command.Parameters.Add("@StartedAt", SqlDbType.DateTimeOffset).Value = startedAt;
        EnsureSingleRow(await command.ExecuteNonQueryAsync(cancellationToken), "Reconciliation run insert");
        return new ReconciliationRunResult(id, scope, ReconciliationRunStatus.Running, 0, startedAt, null);
    }

    /// <inheritdoc />
    public async Task<int> ReconcileWalletLedgerAsync(Guid runId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        const string sql = """
            ;WITH LedgerBalances AS
            (
                SELECT
                    la.Code,
                    SUM(CASE WHEN le.Side=2 THEN le.Amount ELSE -le.Amount END) AS LedgerBalance
                FROM dbo.LedgerAccounts la
                INNER JOIN dbo.LedgerEntries le ON le.AccountId=la.Id
                INNER JOIN dbo.LedgerJournals lj ON lj.Id=le.JournalId AND lj.Status=2
                WHERE la.Type=2
                  AND la.Code LIKE N'WALLET-LIABILITY:%'
                GROUP BY la.Code
            )
            INSERT INTO dbo.ReconciliationIssues
                (Id,RunId,IssueType,TransactionId,WalletId,BankAccountId,ExternalTransactionId,Currency,ExpectedAmount,ActualAmount,Details,CreatedAt,ResolvedAt)
            SELECT
                NEWID(),
                @RunId,
                @IssueType,
                NULL,
                w.Id,
                NULL,
                NULL,
                w.Currency,
                CONVERT(DECIMAL(19,4),w.AvailableBalance+w.BlockedBalance),
                CONVERT(DECIMAL(19,4),COALESCE(lb.LedgerBalance,0)),
                N'Wallet current balance differs from wallet-liability ledger-derived balance.',
                @Now,
                NULL
            FROM dbo.Wallets w
            LEFT JOIN LedgerBalances lb
              ON lb.Code=CONCAT(N'WALLET-LIABILITY:',LOWER(REPLACE(CONVERT(VARCHAR(36),w.Id),'-','')))
            WHERE CONVERT(DECIMAL(19,4),w.AvailableBalance+w.BlockedBalance)<>CONVERT(DECIMAL(19,4),COALESCE(lb.LedgerBalance,0));
            SELECT @@ROWCOUNT;
            """;
        return await ExecuteIssueInsertAsync(sql, runId, ReconciliationIssueType.WalletLedgerMismatch, now, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<int> ReconcileBankSettlementLedgerAsync(Guid runId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var count = 0;

        const string missingSql = """
            INSERT INTO dbo.ReconciliationIssues
                (Id,RunId,IssueType,TransactionId,WalletId,BankAccountId,ExternalTransactionId,Currency,ExpectedAmount,ActualAmount,Details,CreatedAt,ResolvedAt)
            SELECT NEWID(),@RunId,@IssueType,t.Id,NULL,d.BankAccountId,d.ExternalTransactionId,t.Currency,t.Amount,NULL,
                   N'Completed FinWallet bank transaction has no BANK-SETTLEMENT ledger entry.',@Now,NULL
            FROM dbo.FinancialTransactions t
            INNER JOIN dbo.FinancialTransactionDetails d ON d.FinancialTransactionId=t.Id
            WHERE t.Type IN (2,3) AND t.Status=2
              AND NOT EXISTS
              (
                  SELECT 1
                  FROM dbo.LedgerJournals lj
                  INNER JOIN dbo.LedgerEntries le ON le.JournalId=lj.Id
                  INNER JOIN dbo.LedgerAccounts la ON la.Id=le.AccountId
                  WHERE lj.TransactionReference=t.Id
                    AND lj.Status=2
                    AND la.Code=CONCAT(N'BANK-SETTLEMENT:',CASE t.Currency WHEN 1 THEN N'TRY' WHEN 2 THEN N'USD' WHEN 3 THEN N'EUR' END)
              );
            SELECT @@ROWCOUNT;
            """;
        count += await ExecuteIssueInsertAsync(connection, transaction, missingSql, runId, ReconciliationIssueType.MissingExternal, now, cancellationToken);

        const string amountSql = """
            ;WITH BankEntries AS
            (
                SELECT t.Id TransactionId,d.BankAccountId,d.ExternalTransactionId,t.Currency,t.Amount,
                       SUM(le.Amount) LedgerAmount,COUNT_BIG(*) EntryCount
                FROM dbo.FinancialTransactions t
                INNER JOIN dbo.FinancialTransactionDetails d ON d.FinancialTransactionId=t.Id
                INNER JOIN dbo.LedgerJournals lj ON lj.TransactionReference=t.Id AND lj.Status=2
                INNER JOIN dbo.LedgerEntries le ON le.JournalId=lj.Id
                INNER JOIN dbo.LedgerAccounts la ON la.Id=le.AccountId
                WHERE t.Type IN (2,3) AND t.Status=2
                  AND la.Code=CONCAT(N'BANK-SETTLEMENT:',CASE t.Currency WHEN 1 THEN N'TRY' WHEN 2 THEN N'USD' WHEN 3 THEN N'EUR' END)
                GROUP BY t.Id,d.BankAccountId,d.ExternalTransactionId,t.Currency,t.Amount
            )
            INSERT INTO dbo.ReconciliationIssues
                (Id,RunId,IssueType,TransactionId,WalletId,BankAccountId,ExternalTransactionId,Currency,ExpectedAmount,ActualAmount,Details,CreatedAt,ResolvedAt)
            SELECT NEWID(),@RunId,@IssueType,TransactionId,NULL,BankAccountId,ExternalTransactionId,Currency,Amount,LedgerAmount,
                   N'BANK-SETTLEMENT ledger amount differs from completed FinWallet bank transaction amount.',@Now,NULL
            FROM BankEntries
            WHERE LedgerAmount<>Amount;
            SELECT @@ROWCOUNT;
            """;
        count += await ExecuteIssueInsertAsync(connection, transaction, amountSql, runId, ReconciliationIssueType.AmountMismatch, now, cancellationToken);

        const string directionSql = """
            INSERT INTO dbo.ReconciliationIssues
                (Id,RunId,IssueType,TransactionId,WalletId,BankAccountId,ExternalTransactionId,Currency,ExpectedAmount,ActualAmount,Details,CreatedAt,ResolvedAt)
            SELECT NEWID(),@RunId,@IssueType,t.Id,NULL,d.BankAccountId,d.ExternalTransactionId,t.Currency,t.Amount,le.Amount,
                   N'BANK-SETTLEMENT ledger debit/credit direction is inconsistent with FinWallet bank transaction direction.',@Now,NULL
            FROM dbo.FinancialTransactions t
            INNER JOIN dbo.FinancialTransactionDetails d ON d.FinancialTransactionId=t.Id
            INNER JOIN dbo.LedgerJournals lj ON lj.TransactionReference=t.Id AND lj.Status=2
            INNER JOIN dbo.LedgerEntries le ON le.JournalId=lj.Id
            INNER JOIN dbo.LedgerAccounts la ON la.Id=le.AccountId
            WHERE t.Type IN (2,3) AND t.Status=2
              AND la.Code=CONCAT(N'BANK-SETTLEMENT:',CASE t.Currency WHEN 1 THEN N'TRY' WHEN 2 THEN N'USD' WHEN 3 THEN N'EUR' END)
              AND ((t.Type=2 AND le.Side<>1) OR (t.Type=3 AND le.Side<>2));
            SELECT @@ROWCOUNT;
            """;
        count += await ExecuteIssueInsertAsync(connection, transaction, directionSql, runId, ReconciliationIssueType.DirectionMismatch, now, cancellationToken);

        const string duplicateSql = """
            ;WITH BankEntryCount AS
            (
                SELECT t.Id TransactionId,d.BankAccountId,d.ExternalTransactionId,t.Currency,t.Amount,COUNT_BIG(*) EntryCount
                FROM dbo.FinancialTransactions t
                INNER JOIN dbo.FinancialTransactionDetails d ON d.FinancialTransactionId=t.Id
                INNER JOIN dbo.LedgerJournals lj ON lj.TransactionReference=t.Id AND lj.Status=2
                INNER JOIN dbo.LedgerEntries le ON le.JournalId=lj.Id
                INNER JOIN dbo.LedgerAccounts la ON la.Id=le.AccountId
                WHERE t.Type IN (2,3) AND t.Status=2
                  AND la.Code=CONCAT(N'BANK-SETTLEMENT:',CASE t.Currency WHEN 1 THEN N'TRY' WHEN 2 THEN N'USD' WHEN 3 THEN N'EUR' END)
                GROUP BY t.Id,d.BankAccountId,d.ExternalTransactionId,t.Currency,t.Amount
            )
            INSERT INTO dbo.ReconciliationIssues
                (Id,RunId,IssueType,TransactionId,WalletId,BankAccountId,ExternalTransactionId,Currency,ExpectedAmount,ActualAmount,Details,CreatedAt,ResolvedAt)
            SELECT NEWID(),@RunId,@IssueType,TransactionId,NULL,BankAccountId,ExternalTransactionId,Currency,Amount,NULL,
                   N'More than one BANK-SETTLEMENT ledger entry exists for one completed bank transaction.',@Now,NULL
            FROM BankEntryCount WHERE EntryCount>1;
            SELECT @@ROWCOUNT;
            """;
        count += await ExecuteIssueInsertAsync(connection, transaction, duplicateSql, runId, ReconciliationIssueType.Duplicate, now, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return count;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<ReconciliationBankAccount>> ListBankAccountsAsync(CancellationToken cancellationToken)
    {
        const string sql = "SELECT Id,ExternalAccountId,Currency FROM dbo.BankAccounts WHERE Status=2 AND ExternalAccountId IS NOT NULL ORDER BY Id;";
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        var result = new List<ReconciliationBankAccount>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) result.Add(new ReconciliationBankAccount(reader.GetGuid(0), reader.GetGuid(1), (CurrencyCode)reader.GetByte(2)));
        return result;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<ReconciliationBankMovement>> ListCompletedBankMovementsAsync(Guid bankAccountId, CancellationToken cancellationToken)
    {
        if (bankAccountId == Guid.Empty) throw new ArgumentException("BankAccount identifier cannot be empty.", nameof(bankAccountId));
        const string sql = """
            SELECT t.Id,d.BankAccountId,d.ExternalTransactionId,t.Type,t.Currency,t.Amount
            FROM dbo.FinancialTransactions t
            INNER JOIN dbo.FinancialTransactionDetails d ON d.FinancialTransactionId=t.Id
            WHERE d.BankAccountId=@BankAccountId
              AND t.Type IN (2,3)
              AND t.Status=2
              AND d.ExternalTransactionId IS NOT NULL
            ORDER BY t.CreatedAt,t.Id;
            """;
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@BankAccountId", SqlDbType.UniqueIdentifier).Value = bankAccountId;
        var result = new List<ReconciliationBankMovement>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new ReconciliationBankMovement(
                reader.GetGuid(0),reader.GetGuid(1),reader.GetGuid(2),(FinancialTransactionType)reader.GetByte(3),new Money(reader.GetDecimal(5),(CurrencyCode)reader.GetByte(4))));
        }
        return result;
    }

    /// <inheritdoc />
    public async Task SaveIssuesAsync(Guid runId, IReadOnlyCollection<ReconciliationIssueCandidate> issues, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (runId == Guid.Empty) throw new ArgumentException("Run identifier cannot be empty.", nameof(runId));
        ArgumentNullException.ThrowIfNull(issues);
        if (issues.Count == 0) return;
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        const string sql = """
            INSERT INTO dbo.ReconciliationIssues
                (Id,RunId,IssueType,TransactionId,WalletId,BankAccountId,ExternalTransactionId,Currency,ExpectedAmount,ActualAmount,Details,CreatedAt,ResolvedAt)
            VALUES
                (@Id,@RunId,@IssueType,@TransactionId,@WalletId,@BankAccountId,@ExternalTransactionId,@Currency,@ExpectedAmount,@ActualAmount,@Details,@CreatedAt,NULL);
            """;
        foreach (var issue in issues)
        {
            await using var command = new SqlCommand(sql, connection, transaction);
            command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = Guid.NewGuid();
            command.Parameters.Add("@RunId", SqlDbType.UniqueIdentifier).Value = runId;
            command.Parameters.Add("@IssueType", SqlDbType.TinyInt).Value = (byte)issue.Type;
            command.Parameters.Add("@TransactionId", SqlDbType.UniqueIdentifier).Value = (object?)issue.TransactionId ?? DBNull.Value;
            command.Parameters.Add("@WalletId", SqlDbType.UniqueIdentifier).Value = (object?)issue.WalletId ?? DBNull.Value;
            command.Parameters.Add("@BankAccountId", SqlDbType.UniqueIdentifier).Value = (object?)issue.BankAccountId ?? DBNull.Value;
            command.Parameters.Add("@ExternalTransactionId", SqlDbType.UniqueIdentifier).Value = (object?)issue.ExternalTransactionId ?? DBNull.Value;
            command.Parameters.Add("@Currency", SqlDbType.TinyInt).Value = issue.Currency.HasValue ? (byte)issue.Currency.Value : DBNull.Value;
            AddNullableMoney(command,"@ExpectedAmount",issue.ExpectedAmount);
            AddNullableMoney(command,"@ActualAmount",issue.ActualAmount);
            command.Parameters.Add("@Details", SqlDbType.NVarChar,1024).Value = issue.Details.Length<=1024?issue.Details:issue.Details[..1024];
            command.Parameters.Add("@CreatedAt", SqlDbType.DateTimeOffset).Value = now;
            EnsureSingleRow(await command.ExecuteNonQueryAsync(cancellationToken),"Reconciliation issue insert");
        }
        await transaction.CommitAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task<ReconciliationRunResult> CompleteRunAsync(Guid runId, int issueCount, DateTimeOffset completedAt, CancellationToken cancellationToken) => FinalizeRunAsync(runId, ReconciliationRunStatus.Completed, issueCount, completedAt, cancellationToken);

    /// <inheritdoc />
    public Task<ReconciliationRunResult> FailRunAsync(Guid runId, DateTimeOffset completedAt, CancellationToken cancellationToken) => FinalizeRunAsync(runId, ReconciliationRunStatus.Failed, issueCount: null, completedAt, cancellationToken);

    private async Task<ReconciliationRunResult> FinalizeRunAsync(Guid runId, ReconciliationRunStatus status, int? issueCount, DateTimeOffset completedAt, CancellationToken cancellationToken)
    {
        if (runId == Guid.Empty) throw new ArgumentException("Run identifier cannot be empty.", nameof(runId));
        const string sql = """
            UPDATE dbo.ReconciliationRuns
            SET Status=@Status,CompletedAt=@CompletedAt,IssueCount=COALESCE(@IssueCount,(SELECT COUNT(1) FROM dbo.ReconciliationIssues WHERE RunId=@Id))
            WHERE Id=@Id AND Status=1;
            SELECT Scope,Status,StartedAt,CompletedAt,IssueCount FROM dbo.ReconciliationRuns WHERE Id=@Id;
            """;
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@Status", SqlDbType.TinyInt).Value = (byte)status;
        command.Parameters.Add("@CompletedAt", SqlDbType.DateTimeOffset).Value = completedAt;
        command.Parameters.Add("@IssueCount", SqlDbType.Int).Value = (object?)issueCount ?? DBNull.Value;
        command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = runId;
        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) throw new InvalidOperationException("Reconciliation run could not be finalized.");
        return new ReconciliationRunResult(runId,(ReconciliationScope)reader.GetByte(0),(ReconciliationRunStatus)reader.GetByte(1),reader.GetInt32(4),reader.GetFieldValue<DateTimeOffset>(2),reader.IsDBNull(3)?null:reader.GetFieldValue<DateTimeOffset>(3));
    }

    private async Task<int> ExecuteIssueInsertAsync(string sql, Guid runId, ReconciliationIssueType issueType, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted,cancellationToken);
        var count = await ExecuteIssueInsertAsync(connection,transaction,sql,runId,issueType,now,cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return count;
    }

    private static async Task<int> ExecuteIssueInsertAsync(SqlConnection connection, SqlTransaction transaction, string sql, Guid runId, ReconciliationIssueType issueType, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@RunId",SqlDbType.UniqueIdentifier).Value=runId;
        command.Parameters.Add("@IssueType",SqlDbType.TinyInt).Value=(byte)issueType;
        command.Parameters.Add("@Now",SqlDbType.DateTimeOffset).Value=now;
        var value=await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(value,System.Globalization.CultureInfo.InvariantCulture);
    }

    private static void AddNullableMoney(SqlCommand command,string name,decimal? value)
    {
        var parameter=command.Parameters.Add(name,SqlDbType.Decimal);parameter.Precision=19;parameter.Scale=4;parameter.Value=(object?)value??DBNull.Value;
    }

    private static void EnsureSingleRow(int count,string operation)
    {
        if(count!=1)throw new InvalidOperationException($"{operation} expected one affected row but affected {count}.");
    }
}
