using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FinWallet.Application.Banking;
using FinWallet.Domain.Ledger;
using FinWallet.Domain.Shared;
using FinWallet.Domain.Transactions;
using FinWallet.Domain.Wallets;
using Microsoft.Data.SqlClient;

namespace FinWallet.Infrastructure.Persistence.SqlServer;

/// <summary>
/// TR: Bank→Wallet deposit ve Wallet→Bank withdrawal işlemlerinin durable idempotency, fund blocking, provider state, wallet balance, double-entry ledger ve outbox state'ini MSSQL üzerinde yönetir.
/// EN: Manages durable idempotency, fund blocking, provider state, wallet balance, double-entry ledger and outbox state for Bank→Wallet deposits and Wallet→Bank withdrawals on MSSQL.
/// </summary>
public sealed class SqlBankMoneyMovementStore : IBankMoneyMovementStore
{
    private const byte IdempotencyProcessing = 1;
    private const byte IdempotencyCompleted = 2;
    private const byte IdempotencyFailed = 3;
    private const byte TransactionProcessing = 1;
    private const byte TransactionCompleted = 2;
    private const byte TransactionFailed = 3;
    private const byte ProviderPending = 1;
    private const byte ProviderCompleted = 2;
    private const byte ProviderFailed = 3;
    private const byte WalletActive = 1;
    private const byte BankAccountActive = 2;
    private readonly SqlConnectionFactory _connectionFactory;
    private readonly TimeProvider _timeProvider;

    /// <summary>TR: SQL connection factory ve test edilebilir UTC zaman kaynağıyla store'u oluşturur. EN: Creates the store with SQL connection factory and testable UTC time source.</summary>
    /// <param name="connectionFactory">TR: Pooled SQL connection factory. EN: Pooled SQL connection factory.</param>
    /// <param name="timeProvider">TR: Durable timestamp'ler için UTC zaman kaynağı. EN: UTC time source for durable timestamps.</param>
    public SqlBankMoneyMovementStore(SqlConnectionFactory connectionFactory, TimeProvider timeProvider)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <inheritdoc />
    public async Task<BankMoneyMovementContext?> FindContextAsync(Guid customerId, Guid bankAccountId, CancellationToken cancellationToken)
    {
        if (customerId == Guid.Empty || bankAccountId == Guid.Empty) return null;
        const string sql = """
            SELECT b.Id, b.WalletId, b.ExternalAccountId, b.Currency, c.CountryCode
            FROM dbo.BankAccounts b
            INNER JOIN dbo.Customers c ON c.Id = b.CustomerId
            INNER JOIN dbo.Wallets w ON w.Id = b.WalletId
            WHERE b.Id = @BankAccountId
              AND b.CustomerId = @CustomerId
              AND b.Status = @BankAccountActive
              AND b.ExternalAccountId IS NOT NULL
              AND w.Status = @WalletActive;
            """;

        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@BankAccountId", SqlDbType.UniqueIdentifier).Value = bankAccountId;
        command.Parameters.Add("@CustomerId", SqlDbType.UniqueIdentifier).Value = customerId;
        command.Parameters.Add("@BankAccountActive", SqlDbType.TinyInt).Value = BankAccountActive;
        command.Parameters.Add("@WalletActive", SqlDbType.TinyInt).Value = WalletActive;
        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return new BankMoneyMovementContext(
            reader.GetGuid(reader.GetOrdinal("Id")),
            reader.GetGuid(reader.GetOrdinal("WalletId")),
            reader.GetGuid(reader.GetOrdinal("ExternalAccountId")),
            (CurrencyCode)reader.GetByte(reader.GetOrdinal("Currency")),
            reader.GetString(reader.GetOrdinal("CountryCode")));
    }

    /// <inheritdoc />
    public async Task<BankMoneyMovementResult> PrepareAsync(BankMoneyMovementPreparation request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        FinancialAmountRules.EnsureStorageCompatible(request.Amount.Amount, nameof(request.Amount));
        var financialType = ToFinancialTransactionType(request.Type);
        var scope = ToIdempotencyScope(financialType);
        var requestHash = CreateRequestHash(request, financialType);
        var now = _timeProvider.GetUtcNow();

        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var existing = await FindIdempotencyForUpdateAsync(connection, transaction, scope, request.CustomerId, request.IdempotencyKey, cancellationToken);
        if (existing is not null)
        {
            if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(existing.RequestHash), Encoding.ASCII.GetBytes(requestHash)))
            {
                throw new BankMoneyMovementIdempotencyConflictException();
            }

            if (existing.ResourceId is null) throw new InvalidOperationException("Bank-movement idempotency record has no resource identifier.");
            var replay = await LoadOperationAsync(connection, transaction, existing.ResourceId.Value, lockForUpdate: false, cancellationToken)
                ?? throw new InvalidOperationException("Bank-movement idempotency resource is missing.");
            await transaction.CommitAsync(cancellationToken);
            return ToResult(replay, wasReplay: true);
        }

        var context = await LoadContextForUpdateAsync(connection, transaction, request.CustomerId, request.BankAccountId, cancellationToken)
            ?? throw new BankMoneyMovementAccountUnavailableException();
        if (context.Wallet.Currency != request.Amount.Currency) throw new CurrencyMismatchException(context.Wallet.Currency, request.Amount.Currency);

        if (financialType == FinancialTransactionType.BankWithdrawal)
        {
            context.Wallet.BlockFunds(request.Amount);
            await UpdateWalletAsync(connection, transaction, context.Wallet, cancellationToken);
        }

        var transactionId = Guid.NewGuid();
        var sourceWallet = financialType == FinancialTransactionType.BankWithdrawal ? context.Wallet.Id : (Guid?)null;
        var destinationWallet = financialType == FinancialTransactionType.BankDeposit ? context.Wallet.Id : (Guid?)null;
        await InsertFinancialTransactionAsync(connection, transaction, transactionId, request.CustomerId, financialType, sourceWallet, destinationWallet, request.Amount, now, cancellationToken);

        var nextAttemptAt = request.CanProcessNow
            ? now
            : new DateTimeOffset(request.ProcessingDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        if (nextAttemptAt < now) nextAttemptAt = now;

        await InsertDetailsAsync(connection, transaction, transactionId, request, nextAttemptAt, now, cancellationToken);
        await InsertIdempotencyAsync(connection, transaction, Guid.NewGuid(), scope, request.CustomerId, request.IdempotencyKey, requestHash, transactionId, now, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var created = new OperationState(
            transactionId,
            request.CustomerId,
            financialType,
            TransactionProcessing,
            context.Wallet.Id,
            request.BankAccountId,
            context.ExternalAccountId,
            null,
            request.Amount,
            request.ProcessingDate,
            request.SettlementDate,
            nextAttemptAt,
            now,
            null);
        return ToResult(created, wasReplay: false);
    }

    /// <inheritdoc />
    public async Task<BankMoneyMovementResult> ApplyProviderResultAsync(Guid transactionId, Guid externalTransactionId, ExternalBankTransactionStatus providerStatus, CancellationToken cancellationToken)
    {
        if (transactionId == Guid.Empty || externalTransactionId == Guid.Empty) throw new ArgumentException("Transaction identifiers cannot be empty.");
        if (!Enum.IsDefined(providerStatus)) throw new ArgumentOutOfRangeException(nameof(providerStatus));
        var now = _timeProvider.GetUtcNow();

        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var state = await LoadOperationAsync(connection, transaction, transactionId, lockForUpdate: true, cancellationToken)
            ?? throw new KeyNotFoundException("Bank money movement was not found.");

        if (state.ExternalTransactionId.HasValue && state.ExternalTransactionId.Value != externalTransactionId)
        {
            throw new InvalidOperationException("Provider transaction identifier conflicts with durable bank-movement state.");
        }

        if (state.TransactionStatus != TransactionProcessing)
        {
            await transaction.CommitAsync(cancellationToken);
            return ToResult(state, wasReplay: true);
        }

        if (providerStatus == ExternalBankTransactionStatus.Pending)
        {
            await UpdateProviderStateAsync(connection, transaction, transactionId, externalTransactionId, ProviderPending, now.AddSeconds(10), cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return ToResult(state.WithProvider(externalTransactionId, now.AddSeconds(10)), wasReplay: false);
        }

        var context = await LoadWalletForUpdateAsync(connection, transaction, state.WalletId, cancellationToken)
            ?? throw new InvalidOperationException("Wallet for bank money movement is missing.");
        if (context.Currency != state.Amount.Currency) throw new InvalidOperationException("Bank movement wallet currency changed unexpectedly.");

        if (providerStatus == ExternalBankTransactionStatus.Failed)
        {
            if (state.FinancialType == FinancialTransactionType.BankWithdrawal)
            {
                context.ReleaseBlockedFunds(state.Amount);
                await UpdateWalletAsync(connection, transaction, context, cancellationToken);
            }

            await FailTransactionAsync(connection, transaction, transactionId, now, "BANK_PROVIDER_FAILED", cancellationToken);
            await UpdateProviderStateAsync(connection, transaction, transactionId, externalTransactionId, ProviderFailed, nextAttemptAt: null, cancellationToken);
            await CompleteIdempotencyAsync(connection, transaction, transactionId, ToIdempotencyScope(state.FinancialType), IdempotencyFailed, "BANK_MOVEMENT_FAILED", now, cancellationToken);
            await InsertOutboxAsync(connection, transaction, transactionId, state.CustomerId, "BANK_MOVEMENT_FAILED", state.FinancialType, state.Amount, now, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return ToResult(state.AsTerminal(TransactionFailed, externalTransactionId, now), wasReplay: false);
        }

        if (state.FinancialType == FinancialTransactionType.BankDeposit)
        {
            context.Credit(state.Amount);
        }
        else
        {
            context.SettleBlockedFunds(state.Amount);
        }
        FinancialAmountRules.EnsureStorageCompatible(context.AvailableBalance, nameof(context.AvailableBalance));
        FinancialAmountRules.EnsureStorageCompatible(context.BlockedBalance, nameof(context.BlockedBalance));

        var walletAccount = await GetOrCreateLedgerAccountAsync(connection, transaction, $"WALLET-LIABILITY:{context.Id:N}", context.Currency, LedgerAccountType.Liability, now, cancellationToken);
        var settlementAccount = await GetOrCreateLedgerAccountAsync(connection, transaction, $"BANK-SETTLEMENT:{context.Currency}", context.Currency, LedgerAccountType.Asset, now, cancellationToken);
        var journal = new LedgerJournal(Guid.NewGuid(), transactionId, context.Currency.ToString(), now);
        if (state.FinancialType == FinancialTransactionType.BankDeposit)
        {
            journal.AddDebit(settlementAccount, state.Amount.Amount);
            journal.AddCredit(walletAccount, state.Amount.Amount);
        }
        else
        {
            journal.AddDebit(walletAccount, state.Amount.Amount);
            journal.AddCredit(settlementAccount, state.Amount.Amount);
        }
        journal.Post(now);

        await InsertPostedJournalAsync(connection, transaction, journal, context.Currency, cancellationToken);
        await VerifyJournalBalanceAsync(connection, transaction, journal.Id, cancellationToken);
        await UpdateWalletAsync(connection, transaction, context, cancellationToken);
        await CompleteTransactionAsync(connection, transaction, transactionId, now, cancellationToken);
        await UpdateProviderStateAsync(connection, transaction, transactionId, externalTransactionId, ProviderCompleted, nextAttemptAt: null, cancellationToken);
        await CompleteIdempotencyAsync(connection, transaction, transactionId, ToIdempotencyScope(state.FinancialType), IdempotencyCompleted, "BANK_MOVEMENT_COMPLETED", now, cancellationToken);
        await InsertOutboxAsync(connection, transaction, transactionId, state.CustomerId, "BANK_MOVEMENT_COMPLETED", state.FinancialType, state.Amount, now, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToResult(state.AsTerminal(TransactionCompleted, externalTransactionId, now), wasReplay: false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<BankMoneyMovementResult>> ListDueAsync(DateTimeOffset now, int take, CancellationToken cancellationToken)
    {
        if (take < 1 || take > 100) throw new ArgumentOutOfRangeException(nameof(take));
        const string sql = """
            SELECT TOP (@Take) t.Id
            FROM dbo.FinancialTransactions t
            INNER JOIN dbo.FinancialTransactionDetails d ON d.FinancialTransactionId = t.Id
            WHERE t.Type IN (@BankDeposit, @BankWithdrawal)
              AND t.Status = @Processing
              AND d.NextAttemptAt IS NOT NULL
              AND d.NextAttemptAt <= @Now
            ORDER BY d.NextAttemptAt, t.CreatedAt;
            """;

        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        var ids = new List<Guid>();
        await using (var command = new SqlCommand(sql, connection))
        {
            command.Parameters.Add("@Take", SqlDbType.Int).Value = take;
            command.Parameters.Add("@BankDeposit", SqlDbType.TinyInt).Value = (byte)FinancialTransactionType.BankDeposit;
            command.Parameters.Add("@BankWithdrawal", SqlDbType.TinyInt).Value = (byte)FinancialTransactionType.BankWithdrawal;
            command.Parameters.Add("@Processing", SqlDbType.TinyInt).Value = TransactionProcessing;
            command.Parameters.Add("@Now", SqlDbType.DateTimeOffset).Value = now;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken)) ids.Add(reader.GetGuid(0));
        }

        var results = new List<BankMoneyMovementResult>(ids.Count);
        foreach (var id in ids)
        {
            var state = await LoadOperationAsync(connection, transaction: null, id, lockForUpdate: false, cancellationToken);
            if (state is not null) results.Add(ToResult(state, wasReplay: true));
        }
        return results;
    }

    private static FinancialTransactionType ToFinancialTransactionType(BankMoneyMovementType providerDirection) => providerDirection switch
    {
        BankMoneyMovementType.Withdrawal => FinancialTransactionType.BankDeposit,
        BankMoneyMovementType.Deposit => FinancialTransactionType.BankWithdrawal,
        _ => throw new ArgumentOutOfRangeException(nameof(providerDirection))
    };

    private static BankMoneyMovementType ToProviderDirection(FinancialTransactionType financialType) => financialType switch
    {
        FinancialTransactionType.BankDeposit => BankMoneyMovementType.Withdrawal,
        FinancialTransactionType.BankWithdrawal => BankMoneyMovementType.Deposit,
        _ => throw new ArgumentOutOfRangeException(nameof(financialType))
    };

    private static string ToIdempotencyScope(FinancialTransactionType financialType) => financialType == FinancialTransactionType.BankDeposit ? "BANK_DEPOSIT" : "BANK_WITHDRAWAL";

    private static string CreateRequestHash(BankMoneyMovementPreparation request, FinancialTransactionType financialType)
    {
        var canonical = string.Join('|', financialType, request.BankAccountId.ToString("N"), request.Amount.Currency, request.Amount.Amount.ToString("0.0000", CultureInfo.InvariantCulture));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private static async Task<IdempotencyState?> FindIdempotencyForUpdateAsync(SqlConnection connection, SqlTransaction transaction, string scope, Guid customerId, string key, CancellationToken cancellationToken)
    {
        const string sql = "SELECT RequestHash, ResourceId FROM dbo.IdempotencyRecords WITH (UPDLOCK, HOLDLOCK) WHERE Scope=@Scope AND CustomerId=@CustomerId AND IdempotencyKey=@Key;";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@Scope", SqlDbType.NVarChar, 64).Value = scope;
        command.Parameters.Add("@CustomerId", SqlDbType.UniqueIdentifier).Value = customerId;
        command.Parameters.Add("@Key", SqlDbType.NVarChar, 128).Value = key;
        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        var resourceOrdinal = reader.GetOrdinal("ResourceId");
        return new IdempotencyState(reader.GetString(0), reader.IsDBNull(resourceOrdinal) ? null : reader.GetGuid(resourceOrdinal));
    }

    private static async Task<LockedContext?> LoadContextForUpdateAsync(SqlConnection connection, SqlTransaction transaction, Guid customerId, Guid bankAccountId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT b.Id, b.ExternalAccountId, w.Id WalletId, w.CustomerId, w.Currency, w.AvailableBalance, w.BlockedBalance, w.Status, w.CreatedAt
            FROM dbo.BankAccounts b WITH (UPDLOCK, ROWLOCK)
            INNER JOIN dbo.Wallets w WITH (UPDLOCK, ROWLOCK) ON w.Id=b.WalletId
            WHERE b.Id=@BankAccountId AND b.CustomerId=@CustomerId AND b.Status=@BankStatus AND b.ExternalAccountId IS NOT NULL AND w.Status=@WalletStatus;
            """;
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@BankAccountId", SqlDbType.UniqueIdentifier).Value = bankAccountId;
        command.Parameters.Add("@CustomerId", SqlDbType.UniqueIdentifier).Value = customerId;
        command.Parameters.Add("@BankStatus", SqlDbType.TinyInt).Value = BankAccountActive;
        command.Parameters.Add("@WalletStatus", SqlDbType.TinyInt).Value = WalletActive;
        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        var wallet = Wallet.Restore(
            reader.GetGuid(reader.GetOrdinal("WalletId")),
            reader.GetGuid(reader.GetOrdinal("CustomerId")),
            (CurrencyCode)reader.GetByte(reader.GetOrdinal("Currency")),
            reader.GetDecimal(reader.GetOrdinal("AvailableBalance")),
            reader.GetDecimal(reader.GetOrdinal("BlockedBalance")),
            (WalletStatus)reader.GetByte(reader.GetOrdinal("Status")),
            reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("CreatedAt")));
        return new LockedContext(reader.GetGuid(reader.GetOrdinal("Id")), reader.GetGuid(reader.GetOrdinal("ExternalAccountId")), wallet);
    }

    private static async Task<Wallet?> LoadWalletForUpdateAsync(SqlConnection connection, SqlTransaction transaction, Guid walletId, CancellationToken cancellationToken)
    {
        const string sql = "SELECT Id,CustomerId,Currency,AvailableBalance,BlockedBalance,Status,CreatedAt FROM dbo.Wallets WITH (UPDLOCK,ROWLOCK) WHERE Id=@Id;";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = walletId;
        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return Wallet.Restore(reader.GetGuid(0), reader.GetGuid(1), (CurrencyCode)reader.GetByte(2), reader.GetDecimal(3), reader.GetDecimal(4), (WalletStatus)reader.GetByte(5), reader.GetFieldValue<DateTimeOffset>(6));
    }

    private static async Task InsertFinancialTransactionAsync(SqlConnection connection, SqlTransaction transaction, Guid id, Guid customerId, FinancialTransactionType type, Guid? sourceWalletId, Guid? destinationWalletId, Money amount, DateTimeOffset now, CancellationToken cancellationToken)
    {
        const string sql = "INSERT INTO dbo.FinancialTransactions (Id,CustomerId,Type,Status,SourceWalletId,DestinationWalletId,Currency,Amount,CreatedAt,FinalizedAt,ReversedAt,FailureCode) VALUES (@Id,@CustomerId,@Type,@Status,@Source,@Destination,@Currency,@Amount,@CreatedAt,NULL,NULL,NULL);";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = id;
        command.Parameters.Add("@CustomerId", SqlDbType.UniqueIdentifier).Value = customerId;
        command.Parameters.Add("@Type", SqlDbType.TinyInt).Value = (byte)type;
        command.Parameters.Add("@Status", SqlDbType.TinyInt).Value = TransactionProcessing;
        command.Parameters.Add("@Source", SqlDbType.UniqueIdentifier).Value = (object?)sourceWalletId ?? DBNull.Value;
        command.Parameters.Add("@Destination", SqlDbType.UniqueIdentifier).Value = (object?)destinationWalletId ?? DBNull.Value;
        command.Parameters.Add("@Currency", SqlDbType.TinyInt).Value = (byte)amount.Currency;
        AddMoney(command, "@Amount", amount.Amount);
        command.Parameters.Add("@CreatedAt", SqlDbType.DateTimeOffset).Value = now;
        EnsureSingleRow(await command.ExecuteNonQueryAsync(cancellationToken), "Financial transaction insert");
    }

    private static async Task InsertDetailsAsync(SqlConnection connection, SqlTransaction transaction, Guid transactionId, BankMoneyMovementPreparation request, DateTimeOffset nextAttemptAt, DateTimeOffset now, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO dbo.FinancialTransactionDetails
                (FinancialTransactionId,BankAccountId,CutoffReference,ProcessingDate,SettlementDate,CorrelationId,ProviderState,NextAttemptAt,CreatedAt)
            VALUES (@TransactionId,@BankAccountId,@CutoffReference,@ProcessingDate,@SettlementDate,@CorrelationId,@ProviderState,@NextAttemptAt,@CreatedAt);
            """;
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@TransactionId", SqlDbType.UniqueIdentifier).Value = transactionId;
        command.Parameters.Add("@BankAccountId", SqlDbType.UniqueIdentifier).Value = request.BankAccountId;
        command.Parameters.Add("@CutoffReference", SqlDbType.UniqueIdentifier).Value = (object?)request.CutoffReference ?? DBNull.Value;
        command.Parameters.Add("@ProcessingDate", SqlDbType.Date).Value = request.ProcessingDate.ToDateTime(TimeOnly.MinValue);
        command.Parameters.Add("@SettlementDate", SqlDbType.Date).Value = request.SettlementDate.ToDateTime(TimeOnly.MinValue);
        command.Parameters.Add("@CorrelationId", SqlDbType.NVarChar, 64).Value = request.CorrelationId;
        command.Parameters.Add("@ProviderState", SqlDbType.TinyInt).Value = ProviderPending;
        command.Parameters.Add("@NextAttemptAt", SqlDbType.DateTimeOffset).Value = nextAttemptAt;
        command.Parameters.Add("@CreatedAt", SqlDbType.DateTimeOffset).Value = now;
        EnsureSingleRow(await command.ExecuteNonQueryAsync(cancellationToken), "Bank movement detail insert");
    }

    private static async Task InsertIdempotencyAsync(SqlConnection connection, SqlTransaction transaction, Guid id, string scope, Guid customerId, string key, string hash, Guid resourceId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        const string sql = "INSERT INTO dbo.IdempotencyRecords (Id,Scope,CustomerId,IdempotencyKey,RequestHash,ResourceId,Status,ResultCode,CreatedAt,UpdatedAt) VALUES (@Id,@Scope,@CustomerId,@Key,@Hash,@ResourceId,@Status,NULL,@Now,@Now);";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = id;
        command.Parameters.Add("@Scope", SqlDbType.NVarChar, 64).Value = scope;
        command.Parameters.Add("@CustomerId", SqlDbType.UniqueIdentifier).Value = customerId;
        command.Parameters.Add("@Key", SqlDbType.NVarChar, 128).Value = key;
        command.Parameters.Add("@Hash", SqlDbType.Char, 64).Value = hash;
        command.Parameters.Add("@ResourceId", SqlDbType.UniqueIdentifier).Value = resourceId;
        command.Parameters.Add("@Status", SqlDbType.TinyInt).Value = IdempotencyProcessing;
        command.Parameters.Add("@Now", SqlDbType.DateTimeOffset).Value = now;
        EnsureSingleRow(await command.ExecuteNonQueryAsync(cancellationToken), "Bank movement idempotency insert");
    }

    private static async Task<OperationState?> LoadOperationAsync(SqlConnection connection, SqlTransaction? transaction, Guid transactionId, bool lockForUpdate, CancellationToken cancellationToken)
    {
        var lockHint = lockForUpdate ? " WITH (UPDLOCK, ROWLOCK)" : string.Empty;
        var sql = $"""
            SELECT t.Id,t.CustomerId,t.Type,t.Status,t.SourceWalletId,t.DestinationWalletId,t.Currency,t.Amount,t.CreatedAt,t.FinalizedAt,
                   d.BankAccountId,d.ExternalTransactionId,d.ProcessingDate,d.SettlementDate,d.NextAttemptAt,b.ExternalAccountId
            FROM dbo.FinancialTransactions t{lockHint}
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
        var externalOrdinal = reader.GetOrdinal("ExternalTransactionId");
        var nextOrdinal = reader.GetOrdinal("NextAttemptAt");
        var finalizedOrdinal = reader.GetOrdinal("FinalizedAt");
        return new OperationState(
            reader.GetGuid(reader.GetOrdinal("Id")),
            reader.GetGuid(reader.GetOrdinal("CustomerId")),
            type,
            reader.GetByte(reader.GetOrdinal("Status")),
            reader.GetGuid(walletOrdinal),
            reader.GetGuid(reader.GetOrdinal("BankAccountId")),
            reader.GetGuid(reader.GetOrdinal("ExternalAccountId")),
            reader.IsDBNull(externalOrdinal) ? null : reader.GetGuid(externalOrdinal),
            new Money(reader.GetDecimal(reader.GetOrdinal("Amount")), (CurrencyCode)reader.GetByte(reader.GetOrdinal("Currency"))),
            DateOnly.FromDateTime(reader.GetDateTime(reader.GetOrdinal("ProcessingDate"))),
            DateOnly.FromDateTime(reader.GetDateTime(reader.GetOrdinal("SettlementDate"))),
            reader.IsDBNull(nextOrdinal) ? null : reader.GetFieldValue<DateTimeOffset>(nextOrdinal),
            reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("CreatedAt")),
            reader.IsDBNull(finalizedOrdinal) ? null : reader.GetFieldValue<DateTimeOffset>(finalizedOrdinal));
    }

    private static async Task UpdateProviderStateAsync(SqlConnection connection, SqlTransaction transaction, Guid transactionId, Guid externalTransactionId, byte providerState, DateTimeOffset? nextAttemptAt, CancellationToken cancellationToken)
    {
        const string sql = "UPDATE dbo.FinancialTransactionDetails SET ExternalTransactionId=COALESCE(ExternalTransactionId,@ExternalTransactionId),ProviderState=@ProviderState,NextAttemptAt=@NextAttemptAt WHERE FinancialTransactionId=@TransactionId AND (ExternalTransactionId IS NULL OR ExternalTransactionId=@ExternalTransactionId);";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@ExternalTransactionId", SqlDbType.UniqueIdentifier).Value = externalTransactionId;
        command.Parameters.Add("@ProviderState", SqlDbType.TinyInt).Value = providerState;
        command.Parameters.Add("@NextAttemptAt", SqlDbType.DateTimeOffset).Value = (object?)nextAttemptAt ?? DBNull.Value;
        command.Parameters.Add("@TransactionId", SqlDbType.UniqueIdentifier).Value = transactionId;
        EnsureSingleRow(await command.ExecuteNonQueryAsync(cancellationToken), "Provider state update");
    }

    private static async Task UpdateWalletAsync(SqlConnection connection, SqlTransaction transaction, Wallet wallet, CancellationToken cancellationToken)
    {
        const string sql = "UPDATE dbo.Wallets SET AvailableBalance=@Available,BlockedBalance=@Blocked WHERE Id=@Id;";
        await using var command = new SqlCommand(sql, connection, transaction);
        AddMoney(command, "@Available", wallet.AvailableBalance);
        AddMoney(command, "@Blocked", wallet.BlockedBalance);
        command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = wallet.Id;
        EnsureSingleRow(await command.ExecuteNonQueryAsync(cancellationToken), "Wallet balance update");
    }

    private static async Task<LedgerAccount> GetOrCreateLedgerAccountAsync(SqlConnection connection, SqlTransaction transaction, string code, CurrencyCode currency, LedgerAccountType type, DateTimeOffset now, CancellationToken cancellationToken)
    {
        const string selectSql = "SELECT Id,Code,Currency,Type,Status FROM dbo.LedgerAccounts WITH (UPDLOCK,HOLDLOCK) WHERE Code=@Code;";
        await using (var command = new SqlCommand(selectSql, connection, transaction))
        {
            command.Parameters.Add("@Code", SqlDbType.NVarChar, 128).Value = code;
            await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                var existing = LedgerAccount.Restore(reader.GetGuid(0), reader.GetString(1), ((CurrencyCode)reader.GetByte(2)).ToString(), (LedgerAccountType)reader.GetByte(3), (LedgerAccountStatus)reader.GetByte(4));
                if (existing.Type != type || !string.Equals(existing.Currency, currency.ToString(), StringComparison.Ordinal) || existing.Status != LedgerAccountStatus.Active) throw new InvalidOperationException("Ledger account mapping is inconsistent.");
                return existing;
            }
        }

        var account = new LedgerAccount(Guid.NewGuid(), code, currency.ToString(), type);
        const string insertSql = "INSERT INTO dbo.LedgerAccounts (Id,Code,Currency,Type,Status,CreatedAt) VALUES (@Id,@Code,@Currency,@Type,@Status,@CreatedAt);";
        await using var insert = new SqlCommand(insertSql, connection, transaction);
        insert.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = account.Id;
        insert.Parameters.Add("@Code", SqlDbType.NVarChar, 128).Value = account.Code;
        insert.Parameters.Add("@Currency", SqlDbType.TinyInt).Value = (byte)currency;
        insert.Parameters.Add("@Type", SqlDbType.TinyInt).Value = (byte)type;
        insert.Parameters.Add("@Status", SqlDbType.TinyInt).Value = (byte)account.Status;
        insert.Parameters.Add("@CreatedAt", SqlDbType.DateTimeOffset).Value = now;
        EnsureSingleRow(await insert.ExecuteNonQueryAsync(cancellationToken), "Ledger account insert");
        return account;
    }

    private static async Task InsertPostedJournalAsync(SqlConnection connection, SqlTransaction transaction, LedgerJournal journal, CurrencyCode currency, CancellationToken cancellationToken)
    {
        const string journalSql = "INSERT INTO dbo.LedgerJournals (Id,TransactionReference,Currency,Status,CreatedAt,PostedAt,ReversesJournalId) VALUES (@Id,@TransactionReference,@Currency,@Status,@CreatedAt,@PostedAt,NULL);";
        await using (var command = new SqlCommand(journalSql, connection, transaction))
        {
            command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = journal.Id;
            command.Parameters.Add("@TransactionReference", SqlDbType.UniqueIdentifier).Value = journal.TransactionReference;
            command.Parameters.Add("@Currency", SqlDbType.TinyInt).Value = (byte)currency;
            command.Parameters.Add("@Status", SqlDbType.TinyInt).Value = (byte)journal.Status;
            command.Parameters.Add("@CreatedAt", SqlDbType.DateTimeOffset).Value = journal.CreatedAt;
            command.Parameters.Add("@PostedAt", SqlDbType.DateTimeOffset).Value = journal.PostedAt!.Value;
            EnsureSingleRow(await command.ExecuteNonQueryAsync(cancellationToken), "Ledger journal insert");
        }

        const string entrySql = "INSERT INTO dbo.LedgerEntries (Id,JournalId,SequenceNumber,AccountId,Side,Amount,Currency) VALUES (@Id,@JournalId,@Sequence,@AccountId,@Side,@Amount,@Currency);";
        short sequence = 0;
        foreach (var entry in journal.Entries)
        {
            sequence++;
            await using var command = new SqlCommand(entrySql, connection, transaction);
            command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = entry.Id;
            command.Parameters.Add("@JournalId", SqlDbType.UniqueIdentifier).Value = journal.Id;
            command.Parameters.Add("@Sequence", SqlDbType.SmallInt).Value = sequence;
            command.Parameters.Add("@AccountId", SqlDbType.UniqueIdentifier).Value = entry.AccountId;
            command.Parameters.Add("@Side", SqlDbType.TinyInt).Value = (byte)entry.Side;
            AddMoney(command, "@Amount", entry.Amount);
            command.Parameters.Add("@Currency", SqlDbType.TinyInt).Value = (byte)currency;
            EnsureSingleRow(await command.ExecuteNonQueryAsync(cancellationToken), "Ledger entry insert");
        }
    }

    private static async Task VerifyJournalBalanceAsync(SqlConnection connection, SqlTransaction transaction, Guid journalId, CancellationToken cancellationToken)
    {
        const string sql = "SELECT COALESCE(SUM(CASE WHEN Side=1 THEN Amount ELSE 0 END),0),COALESCE(SUM(CASE WHEN Side=2 THEN Amount ELSE 0 END),0) FROM dbo.LedgerEntries WHERE JournalId=@JournalId;";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@JournalId", SqlDbType.UniqueIdentifier).Value = journalId;
        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) throw new InvalidOperationException("Persisted ledger journal could not be verified.");
        if (reader.GetDecimal(0) != reader.GetDecimal(1) || reader.GetDecimal(0) <= 0m) throw new UnbalancedLedgerJournalException(reader.GetDecimal(0), reader.GetDecimal(1));
    }

    private static async Task CompleteTransactionAsync(SqlConnection connection, SqlTransaction transaction, Guid transactionId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        const string sql = "UPDATE dbo.FinancialTransactions SET Status=@Status,FinalizedAt=@Now WHERE Id=@Id AND Status=@Processing;";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@Status", SqlDbType.TinyInt).Value = TransactionCompleted;
        command.Parameters.Add("@Now", SqlDbType.DateTimeOffset).Value = now;
        command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = transactionId;
        command.Parameters.Add("@Processing", SqlDbType.TinyInt).Value = TransactionProcessing;
        EnsureSingleRow(await command.ExecuteNonQueryAsync(cancellationToken), "Financial transaction completion");
    }

    private static async Task FailTransactionAsync(SqlConnection connection, SqlTransaction transaction, Guid transactionId, DateTimeOffset now, string failureCode, CancellationToken cancellationToken)
    {
        const string sql = "UPDATE dbo.FinancialTransactions SET Status=@Status,FinalizedAt=@Now,FailureCode=@FailureCode WHERE Id=@Id AND Status=@Processing;";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@Status", SqlDbType.TinyInt).Value = TransactionFailed;
        command.Parameters.Add("@Now", SqlDbType.DateTimeOffset).Value = now;
        command.Parameters.Add("@FailureCode", SqlDbType.NVarChar, 64).Value = failureCode;
        command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = transactionId;
        command.Parameters.Add("@Processing", SqlDbType.TinyInt).Value = TransactionProcessing;
        EnsureSingleRow(await command.ExecuteNonQueryAsync(cancellationToken), "Financial transaction failure");
    }

    private static async Task CompleteIdempotencyAsync(SqlConnection connection, SqlTransaction transaction, Guid transactionId, string scope, byte status, string resultCode, DateTimeOffset now, CancellationToken cancellationToken)
    {
        const string sql = "UPDATE dbo.IdempotencyRecords SET Status=@Status,ResultCode=@ResultCode,UpdatedAt=@Now WHERE Scope=@Scope AND ResourceId=@ResourceId AND Status=@Processing;";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@Status", SqlDbType.TinyInt).Value = status;
        command.Parameters.Add("@ResultCode", SqlDbType.NVarChar, 64).Value = resultCode;
        command.Parameters.Add("@Now", SqlDbType.DateTimeOffset).Value = now;
        command.Parameters.Add("@Scope", SqlDbType.NVarChar, 64).Value = scope;
        command.Parameters.Add("@ResourceId", SqlDbType.UniqueIdentifier).Value = transactionId;
        command.Parameters.Add("@Processing", SqlDbType.TinyInt).Value = IdempotencyProcessing;
        EnsureSingleRow(await command.ExecuteNonQueryAsync(cancellationToken), "Idempotency completion");
    }

    private static async Task InsertOutboxAsync(SqlConnection connection, SqlTransaction transaction, Guid transactionId, Guid customerId, string messageType, FinancialTransactionType financialType, Money amount, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(new { TransactionId = transactionId, CustomerId = customerId, Type = financialType.ToString(), Amount = amount.Amount, Currency = amount.Currency.ToString() });
        const string sql = "INSERT INTO dbo.OutboxMessages (Id,MessageType,AggregateId,PayloadJson,CreatedAt,AvailableAt,AttemptCount) VALUES (@Id,@MessageType,@AggregateId,@Payload,@Now,@Now,0);";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = Guid.NewGuid();
        command.Parameters.Add("@MessageType", SqlDbType.NVarChar, 128).Value = messageType;
        command.Parameters.Add("@AggregateId", SqlDbType.UniqueIdentifier).Value = transactionId;
        command.Parameters.Add("@Payload", SqlDbType.NVarChar, -1).Value = payload;
        command.Parameters.Add("@Now", SqlDbType.DateTimeOffset).Value = now;
        EnsureSingleRow(await command.ExecuteNonQueryAsync(cancellationToken), "Outbox insert");
    }

    private static BankMoneyMovementResult ToResult(OperationState state, bool wasReplay)
    {
        var operationState = state.TransactionStatus switch
        {
            TransactionCompleted => BankMoneyMovementState.Completed,
            TransactionFailed => BankMoneyMovementState.Failed,
            _ when state.ExternalTransactionId is null && state.NextAttemptAt.HasValue && state.NextAttemptAt.Value > state.CreatedAt => BankMoneyMovementState.Scheduled,
            _ => BankMoneyMovementState.Pending
        };
        return new BankMoneyMovementResult(state.TransactionId, state.BankAccountId, state.ExternalAccountId, state.ExternalTransactionId, ToProviderDirection(state.FinancialType), state.Amount, operationState, state.ProcessingDate, state.SettlementDate, wasReplay);
    }

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

    private sealed record IdempotencyState(string RequestHash, Guid? ResourceId);
    private sealed record LockedContext(Guid BankAccountId, Guid ExternalAccountId, Wallet Wallet);

    private sealed class OperationState
    {
        public OperationState(Guid transactionId, Guid customerId, FinancialTransactionType financialType, byte transactionStatus, Guid walletId, Guid bankAccountId, Guid externalAccountId, Guid? externalTransactionId, Money amount, DateOnly processingDate, DateOnly settlementDate, DateTimeOffset? nextAttemptAt, DateTimeOffset createdAt, DateTimeOffset? finalizedAt)
        {
            TransactionId = transactionId; CustomerId = customerId; FinancialType = financialType; TransactionStatus = transactionStatus; WalletId = walletId; BankAccountId = bankAccountId; ExternalAccountId = externalAccountId; ExternalTransactionId = externalTransactionId; Amount = amount; ProcessingDate = processingDate; SettlementDate = settlementDate; NextAttemptAt = nextAttemptAt; CreatedAt = createdAt; FinalizedAt = finalizedAt;
        }
        public Guid TransactionId { get; }
        public Guid CustomerId { get; }
        public FinancialTransactionType FinancialType { get; }
        public byte TransactionStatus { get; }
        public Guid WalletId { get; }
        public Guid BankAccountId { get; }
        public Guid ExternalAccountId { get; }
        public Guid? ExternalTransactionId { get; }
        public Money Amount { get; }
        public DateOnly ProcessingDate { get; }
        public DateOnly SettlementDate { get; }
        public DateTimeOffset? NextAttemptAt { get; }
        public DateTimeOffset CreatedAt { get; }
        public DateTimeOffset? FinalizedAt { get; }
        public OperationState WithProvider(Guid externalTransactionId, DateTimeOffset nextAttemptAt) => new(TransactionId, CustomerId, FinancialType, TransactionStatus, WalletId, BankAccountId, ExternalAccountId, externalTransactionId, Amount, ProcessingDate, SettlementDate, nextAttemptAt, CreatedAt, FinalizedAt);
        public OperationState AsTerminal(byte status, Guid externalTransactionId, DateTimeOffset finalizedAt) => new(TransactionId, CustomerId, FinancialType, status, WalletId, BankAccountId, ExternalAccountId, externalTransactionId, Amount, ProcessingDate, SettlementDate, null, CreatedAt, finalizedAt);
    }
}
