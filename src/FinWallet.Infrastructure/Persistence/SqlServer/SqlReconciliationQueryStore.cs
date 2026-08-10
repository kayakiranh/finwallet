using System.Data;
using FinWallet.Application.Reconciliation;
using FinWallet.Domain.Shared;
using Microsoft.Data.SqlClient;

namespace FinWallet.Infrastructure.Persistence.SqlServer;

/// <summary>TR: Reconciliation run summary ve mismatch issue read-model'lerini MSSQL üzerinden salt-okunur sorgularla sunar. EN: Exposes reconciliation-run summaries and mismatch-issue read models through read-only MSSQL queries.</summary>
public sealed class SqlReconciliationQueryStore : IReconciliationQueryStore
{
    private readonly SqlConnectionFactory _connectionFactory;

    /// <summary>TR: Pooled SQL connection factory ile reconciliation query store oluşturur. EN: Creates reconciliation-query store with pooled SQL connection factory.</summary>
    /// <param name="connectionFactory">TR: SQL connection factory. EN: SQL connection factory.</param>
    public SqlReconciliationQueryStore(SqlConnectionFactory connectionFactory)=>_connectionFactory=connectionFactory??throw new ArgumentNullException(nameof(connectionFactory));

    /// <inheritdoc />
    public async Task<ReconciliationRunResult?> GetRunAsync(Guid runId,CancellationToken cancellationToken)
    {
        if(runId==Guid.Empty)return null;
        const string sql="SELECT Id,Scope,Status,StartedAt,CompletedAt,IssueCount FROM dbo.ReconciliationRuns WHERE Id=@Id;";
        await using var connection=_connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command=new SqlCommand(sql,connection);
        command.Parameters.Add("@Id",SqlDbType.UniqueIdentifier).Value=runId;
        await using var reader=await command.ExecuteReaderAsync(CommandBehavior.SingleRow,cancellationToken);
        if(!await reader.ReadAsync(cancellationToken))return null;
        var completedOrdinal=reader.GetOrdinal("CompletedAt");
        return new ReconciliationRunResult(reader.GetGuid(0),(ReconciliationScope)reader.GetByte(1),(ReconciliationRunStatus)reader.GetByte(2),reader.GetInt32(5),reader.GetFieldValue<DateTimeOffset>(3),reader.IsDBNull(completedOrdinal)?null:reader.GetFieldValue<DateTimeOffset>(completedOrdinal));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<ReconciliationIssueRecord>> ListIssuesAsync(Guid runId,int take,CancellationToken cancellationToken)
    {
        if(runId==Guid.Empty)return Array.Empty<ReconciliationIssueRecord>();
        if(take<1||take>500)throw new ArgumentOutOfRangeException(nameof(take));
        const string sql="""
            SELECT TOP (@Take) Id,RunId,IssueType,TransactionId,WalletId,BankAccountId,ExternalTransactionId,Currency,ExpectedAmount,ActualAmount,Details,CreatedAt,ResolvedAt
            FROM dbo.ReconciliationIssues
            WHERE RunId=@RunId
            ORDER BY CreatedAt,Id;
            """;
        await using var connection=_connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command=new SqlCommand(sql,connection);
        command.Parameters.Add("@Take",SqlDbType.Int).Value=take;
        command.Parameters.Add("@RunId",SqlDbType.UniqueIdentifier).Value=runId;
        var result=new List<ReconciliationIssueRecord>(take);
        await using var reader=await command.ExecuteReaderAsync(cancellationToken);
        while(await reader.ReadAsync(cancellationToken))
        {
            result.Add(new ReconciliationIssueRecord(
                reader.GetGuid(0),reader.GetGuid(1),(ReconciliationIssueType)reader.GetByte(2),
                GetNullableGuid(reader,3),GetNullableGuid(reader,4),GetNullableGuid(reader,5),GetNullableGuid(reader,6),
                reader.IsDBNull(7)?null:(CurrencyCode)reader.GetByte(7),
                reader.IsDBNull(8)?null:reader.GetDecimal(8),reader.IsDBNull(9)?null:reader.GetDecimal(9),reader.GetString(10),reader.GetFieldValue<DateTimeOffset>(11),reader.IsDBNull(12)?null:reader.GetFieldValue<DateTimeOffset>(12)));
        }
        return result;
    }

    private static Guid? GetNullableGuid(SqlDataReader reader,int ordinal)=>reader.IsDBNull(ordinal)?null:reader.GetGuid(ordinal);
}
