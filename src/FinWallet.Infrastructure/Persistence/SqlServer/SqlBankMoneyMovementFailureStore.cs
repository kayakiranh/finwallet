using System.Data;
using System.Text.Json;
using FinWallet.Application.Banking;
using FinWallet.Domain.Shared;
using FinWallet.Domain.Transactions;
using FinWallet.Domain.Wallets;
using Microsoft.Data.SqlClient;

namespace FinWallet.Infrastructure.Persistence.SqlServer;

/// <summary>
/// TR: Bank provider'ın retry edilmeyecek hatalarında FinWallet durable state'ini terminal Failed yapar ve Wallet→Bank withdrawal için bloke fonu aynı MSSQL transaction içinde serbest bırakır.
/// EN: Moves FinWallet durable state to terminal Failed on non-retryable bank-provider failures and releases blocked funds for Wallet→Bank withdrawals inside the same MSSQL transaction.
/// </summary>
public sealed class SqlBankMoneyMovementFailureStore : IBankMoneyMovementFailureStore
{
    private const byte TransactionProcessing = 1;
    private const byte TransactionFailed = 3;
    private const byte ProviderFailed = 3;
    private const byte IdempotencyProcessing = 1;
    private const byte IdempotencyFailed = 3;
    private readonly SqlConnectionFactory _connectionFactory;
    private readonly TimeProvider _timeProvider;

    /// <summary>TR: SQL connection factory ve UTC zaman kaynağıyla failure store oluşturur. EN: Creates failure store with SQL connection factory and UTC time source.</summary>
    /// <param name="connectionFactory">TR: Pooled SQL connection factory. EN: Pooled SQL connection factory.</param>
    /// <param name="timeProvider">TR: Failure UTC zaman kaynağı. EN: Failure UTC time source.</param>
    public SqlBankMoneyMovementFailureStore(SqlConnectionFactory connectionFactory, TimeProvider timeProvider)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <inheritdoc />
    public async Task<BankMoneyMovementResult> FailAsync(Guid transactionId, string failureCode, CancellationToken cancellationToken)
    {
        if (transactionId == Guid.Empty) throw new ArgumentException("Transaction identifier cannot be empty.", nameof(transactionId));
        ArgumentException.ThrowIfNullOrWhiteSpace(failureCode);
        if (failureCode.Trim().Length > 64) failureCode = failureCode.Trim()[..64];
        var now = _timeProvider.GetUtcNow();

        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var state = await LoadStateAsync(connection, transaction, transactionId, cancellationToken)
            ?? throw new KeyNotFoundException("Bank money movement was not found.");
        if (state.Status != TransactionProcessing)
        {
            await transaction.CommitAsync(cancellationToken);
            return ToResult(state, wasReplay: true);
        }

        if (state.Type == FinancialTransactionType.BankWithdrawal)
        {
            var wallet = await LoadWalletAsync(connection, transaction, state.WalletId, cancellationToken)
                ?? throw new InvalidOperationException("Withdrawal wallet is missing.");
            wallet.ReleaseBlockedFunds(state.Amount);
            await UpdateWalletAsync(connection, transaction, wallet, cancellationToken);
        }

        const string transactionSql = "UPDATE dbo.FinancialTransactions SET Status=@Failed,FinalizedAt=@Now,FailureCode=@FailureCode WHERE Id=@Id AND Status=@Processing;";
        await using (var command = new SqlCommand(transactionSql, connection, transaction))
        {
            command.Parameters.Add("@Failed", SqlDbType.TinyInt).Value = TransactionFailed;
            command.Parameters.Add("@Now", SqlDbType.DateTimeOffset).Value = now;
            command.Parameters.Add("@FailureCode", SqlDbType.NVarChar, 64).Value = failureCode.Trim();
            command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = transactionId;
            command.Parameters.Add("@Processing", SqlDbType.TinyInt).Value = TransactionProcessing;
            EnsureSingleRow(await command.ExecuteNonQueryAsync(cancellationToken), "Bank movement terminal failure");
        }

        const string detailsSql = "UPDATE dbo.FinancialTransactionDetails SET ProviderState=@ProviderFailed,NextAttemptAt=NULL WHERE FinancialTransactionId=@Id;";
        await using (var command = new SqlCommand(detailsSql, connection, transaction))
        {
            command.Parameters.Add("@ProviderFailed", SqlDbType.TinyInt).Value = ProviderFailed;
            command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = transactionId;
            EnsureSingleRow(await command.ExecuteNonQueryAsync(cancellationToken), "Bank movement provider failure state");
        }

        var scope = state.Type == FinancialTransactionType.BankDeposit ? "BANK_DEPOSIT" : "BANK_WITHDRAWAL";
        const string idempotencySql = "UPDATE dbo.IdempotencyRecords SET Status=@Failed,ResultCode=@Code,UpdatedAt=@Now WHERE Scope=@Scope AND ResourceId=@Id AND Status=@Processing;";
        await using (var command = new SqlCommand(idempotencySql, connection, transaction))
        {
            command.Parameters.Add("@Failed", SqlDbType.TinyInt).Value = IdempotencyFailed;
            command.Parameters.Add("@Code", SqlDbType.NVarChar, 64).Value = "BANK_MOVEMENT_FAILED";
            command.Parameters.Add("@Now", SqlDbType.DateTimeOffset).Value = now;
            command.Parameters.Add("@Scope", SqlDbType.NVarChar, 64).Value = scope;
            command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = transactionId;
            command.Parameters.Add("@Processing", SqlDbType.TinyInt).Value = IdempotencyProcessing;
            EnsureSingleRow(await command.ExecuteNonQueryAsync(cancellationToken), "Bank movement failure idempotency update");
        }

        var payload = JsonSerializer.Serialize(new
        {
            TransactionId = state.TransactionId,
            CustomerId = state.CustomerId,
            Type = state.Type.ToString(),
            Amount = state.Amount.Amount,
            Currency = state.Amount.Currency.ToString(),
            FailureCode = failureCode.Trim()
        });
        const string outboxSql = "INSERT INTO dbo.OutboxMessages (Id,MessageType,AggregateId,PayloadJson,CreatedAt,AvailableAt,AttemptCount) VALUES (@Id,@Type,@AggregateId,@Payload,@Now,@Now,0);";
        await using (var command = new SqlCommand(outboxSql, connection, transaction))
        {
            command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = Guid.NewGuid();
            command.Parameters.Add("@Type", SqlDbType.NVarChar, 128).Value = "BANK_MOVEMENT_FAILED";
            command.Parameters.Add("@AggregateId", SqlDbType.UniqueIdentifier).Value = transactionId;
            command.Parameters.Add("@Payload", SqlDbType.NVarChar, -1).Value = payload;
            command.Parameters.Add("@Now", SqlDbType.DateTimeOffset).Value = now;
            EnsureSingleRow(await command.ExecuteNonQueryAsync(cancellationToken), "Bank movement failure outbox insert");
        }

        await transaction.CommitAsync(cancellationToken);
        return new BankMoneyMovementResult(
            state.TransactionId,
            state.BankAccountId,
            state.ExternalAccountId,
            state.ExternalTransactionId,
            ToProviderDirection(state.Type),
            state.Amount,
            BankMoneyMovementState.Failed,
            state.ProcessingDate,
            state.SettlementDate,
            wasReplay: false);
    }

    private static async Task<State?> LoadStateAsync(SqlConnection connection, SqlTransaction transaction, Guid transactionId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT t.Id,t.CustomerId,t.Type,t.Status,t.SourceWalletId,t.DestinationWalletId,t.Currency,t.Amount,
                   d.BankAccountId,d.ExternalTransactionId,d.ProcessingDate,d.SettlementDate,b.ExternalAccountId
            FROM dbo.FinancialTransactions t WITH (UPDLOCK,ROWLOCK)
            INNER JOIN dbo.FinancialTransactionDetails d ON d.FinancialTransactionId=t.Id
            INNER JOIN dbo.BankAccounts b ON b.Id=d.BankAccountId
            WHERE t.Id=@Id AND t.Type IN (2,3);
            """;
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = transactionId;
        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        var type = (FinancialTransactionType)reader.GetByte(reader.GetOrdinal("Type"));
        var walletOrdinal = type == FinancialTransactionType.BankDeposit ? reader.GetOrdinal("DestinationWalletId") : reader.GetOrdinal("SourceWalletId");
        var externalTransactionOrdinal = reader.GetOrdinal("ExternalTransactionId");
        return new State(
            reader.GetGuid(reader.GetOrdinal("Id")),
            reader.GetGuid(reader.GetOrdinal("CustomerId")),
            type,
            reader.GetByte(reader.GetOrdinal("Status")),
            reader.GetGuid(walletOrdinal),
            reader.GetGuid(reader.GetOrdinal("BankAccountId")),
            reader.GetGuid(reader.GetOrdinal("ExternalAccountId")),
            reader.IsDBNull(externalTransactionOrdinal) ? null : reader.GetGuid(externalTransactionOrdinal),
            new Money(reader.GetDecimal(reader.GetOrdinal("Amount")), (CurrencyCode)reader.GetByte(reader.GetOrdinal("Currency"))),
            DateOnly.FromDateTime(reader.GetDateTime(reader.GetOrdinal("ProcessingDate"))),
            DateOnly.FromDateTime(reader.GetDateTime(reader.GetOrdinal("SettlementDate"))));
    }

    private static async Task<Wallet?> LoadWalletAsync(SqlConnection connection, SqlTransaction transaction, Guid walletId, CancellationToken cancellationToken)
    {
        const string sql = "SELECT Id,CustomerId,Currency,AvailableBalance,BlockedBalance,Status,CreatedAt FROM dbo.Wallets WITH (UPDLOCK,ROWLOCK) WHERE Id=@Id;";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = walletId;
        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return Wallet.Restore(reader.GetGuid(0), reader.GetGuid(1), (CurrencyCode)reader.GetByte(2), reader.GetDecimal(3), reader.GetDecimal(4), (WalletStatus)reader.GetByte(5), reader.GetFieldValue<DateTimeOffset>(6));
    }

    private static async Task UpdateWalletAsync(SqlConnection connection, SqlTransaction transaction, Wallet wallet, CancellationToken cancellationToken)
    {
        const string sql = "UPDATE dbo.Wallets SET AvailableBalance=@Available,BlockedBalance=@Blocked WHERE Id=@Id;";
        await using var command = new SqlCommand(sql, connection, transaction);
        AddMoney(command, "@Available", wallet.AvailableBalance);
        AddMoney(command, "@Blocked", wallet.BlockedBalance);
        command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = wallet.Id;
        EnsureSingleRow(await command.ExecuteNonQueryAsync(cancellationToken), "Bank movement failure wallet update");
    }

    private static BankMoneyMovementResult ToResult(State state, bool wasReplay)
    {
        var resultState = state.Status == TransactionFailed ? BankMoneyMovementState.Failed : BankMoneyMovementState.Pending;
        return new BankMoneyMovementResult(state.TransactionId, state.BankAccountId, state.ExternalAccountId, state.ExternalTransactionId, ToProviderDirection(state.Type), state.Amount, resultState, state.ProcessingDate, state.SettlementDate, wasReplay);
    }

    private static BankMoneyMovementType ToProviderDirection(FinancialTransactionType type) => type switch
    {
        FinancialTransactionType.BankDeposit => BankMoneyMovementType.Withdrawal,
        FinancialTransactionType.BankWithdrawal => BankMoneyMovementType.Deposit,
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    private static void AddMoney(SqlCommand command, string name, decimal value)
    {
        var parameter = command.Parameters.Add(name, SqlDbType.Decimal);
        parameter.Precision = 19;
        parameter.Scale = 4;
        parameter.Value = value;
    }

    private static void EnsureSingleRow(int count, string operation)
    {
        if (count != 1) throw new InvalidOperationException($"{operation} expected one affected row but affected {count}.");
    }

    private sealed record State(Guid TransactionId, Guid CustomerId, FinancialTransactionType Type, byte Status, Guid WalletId, Guid BankAccountId, Guid ExternalAccountId, Guid? ExternalTransactionId, Money Amount, DateOnly ProcessingDate, DateOnly SettlementDate);
}
