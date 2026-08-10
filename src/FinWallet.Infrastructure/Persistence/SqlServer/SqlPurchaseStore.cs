using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FinWallet.Application.Campaigns;
using FinWallet.Application.Purchases;
using FinWallet.Domain.Ledger;
using FinWallet.Domain.Shared;
using FinWallet.Domain.Transactions;
using FinWallet.Domain.Wallets;
using Microsoft.Data.SqlClient;

namespace FinWallet.Infrastructure.Persistence.SqlServer;

/// <summary>TR: Merchant purchase için wallet debit, campaign sponsor accounting, double-entry ledger, durable idempotency ve outbox kayıtlarını atomik MSSQL transaction içinde uygular. EN: Atomically applies wallet debit, campaign-sponsor accounting, double-entry ledger, durable idempotency and outbox records for merchant purchases in MSSQL.</summary>
public sealed class SqlPurchaseStore : IPurchaseStore
{
    private const string IdempotencyScope = "PURCHASE";
    private const byte IdempotencyCompleted = 2;
    private const byte TransactionCompleted = 2;
    private const byte WalletActive = 1;
    private const byte MerchantActive = 1;
    private readonly SqlConnectionFactory _connectionFactory;
    private readonly TimeProvider _timeProvider;

    /// <summary>TR: SQL connection factory ve UTC zaman kaynağıyla purchase store oluşturur. EN: Creates purchase store with SQL connection factory and UTC time source.</summary>
    /// <param name="connectionFactory">TR: Pooled SQL connection factory. EN: Pooled SQL connection factory.</param>
    /// <param name="timeProvider">TR: Posting UTC zaman kaynağı. EN: Posting UTC time source.</param>
    public SqlPurchaseStore(SqlConnectionFactory connectionFactory, TimeProvider timeProvider)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <inheritdoc />
    public async Task<PurchaseContext?> FindContextAsync(Guid customerId, Guid walletId, string merchantId, CancellationToken cancellationToken)
    {
        if (customerId == Guid.Empty || walletId == Guid.Empty || string.IsNullOrWhiteSpace(merchantId)) return null;
        const string sql = """
            SELECT w.Id,w.Currency,m.Id MerchantId
            FROM dbo.Wallets w
            INNER JOIN dbo.Merchants m ON m.Id=@MerchantId
            WHERE w.Id=@WalletId AND w.CustomerId=@CustomerId AND w.Status=@WalletStatus AND m.Status=@MerchantStatus;
            """;
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@MerchantId", SqlDbType.NVarChar, 64).Value = merchantId.Trim();
        command.Parameters.Add("@WalletId", SqlDbType.UniqueIdentifier).Value = walletId;
        command.Parameters.Add("@CustomerId", SqlDbType.UniqueIdentifier).Value = customerId;
        command.Parameters.Add("@WalletStatus", SqlDbType.TinyInt).Value = WalletActive;
        command.Parameters.Add("@MerchantStatus", SqlDbType.TinyInt).Value = MerchantActive;
        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return new PurchaseContext(reader.GetGuid(0), (CurrencyCode)reader.GetByte(1), reader.GetString(2));
    }

    /// <inheritdoc />
    public async Task<PurchaseResult?> TryGetCompletedAsync(PurchaseCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var hash = CreateRequestHash(command);
        const string sql = "SELECT RequestHash,ResourceId,Status FROM dbo.IdempotencyRecords WHERE Scope=@Scope AND CustomerId=@CustomerId AND IdempotencyKey=@Key;";
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        await using var sqlCommand = new SqlCommand(sql, connection);
        sqlCommand.Parameters.Add("@Scope", SqlDbType.NVarChar, 64).Value = IdempotencyScope;
        sqlCommand.Parameters.Add("@CustomerId", SqlDbType.UniqueIdentifier).Value = command.CustomerId;
        sqlCommand.Parameters.Add("@Key", SqlDbType.NVarChar, 128).Value = command.IdempotencyKey;
        await using var reader = await sqlCommand.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        EnsureHash(reader.GetString(0), hash);
        if (reader.GetByte(2) != IdempotencyCompleted || reader.IsDBNull(1)) return null;
        var transactionId = reader.GetGuid(1);
        await reader.DisposeAsync();
        return await LoadResultAsync(connection, transaction: null, transactionId, wasReplay: true, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PurchaseResult> PostAsync(PurchasePostingRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var command = request.Command;
        var campaign = request.Campaign;
        var requestHash = CreateRequestHash(command);
        FinancialAmountRules.EnsureStorageCompatible(command.OriginalAmount, nameof(command.OriginalAmount));
        FinancialAmountRules.EnsureStorageCompatible(campaign.FinalAmount, nameof(campaign.FinalAmount));
        FinancialAmountRules.EnsureStorageCompatible(campaign.DiscountAmount, nameof(campaign.DiscountAmount));
        var now = _timeProvider.GetUtcNow();

        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var existingResource = await FindIdempotencyForUpdateAsync(connection, transaction, command, requestHash, cancellationToken);
        if (existingResource.HasValue)
        {
            var replay = await LoadResultAsync(connection, transaction, existingResource.Value, wasReplay: true, cancellationToken)
                ?? throw new InvalidOperationException("Completed purchase idempotency resource is missing.");
            await transaction.CommitAsync(cancellationToken);
            return replay;
        }

        var locked = await LoadPurchaseContextForUpdateAsync(connection, transaction, command.CustomerId, command.WalletId, command.MerchantId, cancellationToken)
            ?? throw new PurchaseUnavailableException();
        if (locked.Wallet.Currency != campaign.Currency) throw new CurrencyMismatchException(locked.Wallet.Currency, campaign.Currency);
        if (campaign.OriginalAmount != command.OriginalAmount) throw new InvalidOperationException("Campaign original amount does not match purchase request.");

        var finalMoney = new Money(campaign.FinalAmount, campaign.Currency);
        locked.Wallet.Debit(finalMoney);
        FinancialAmountRules.EnsureStorageCompatible(locked.Wallet.AvailableBalance, nameof(locked.Wallet.AvailableBalance));

        var transactionId = Guid.NewGuid();
        await InsertCompletedTransactionAsync(connection, transaction, transactionId, command.CustomerId, command.WalletId, finalMoney, now, cancellationToken);
        await InsertDetailsAsync(connection, transaction, transactionId, request, now, cancellationToken);

        var walletAccount = await GetOrCreateLedgerAccountAsync(connection, transaction, $"WALLET-LIABILITY:{command.WalletId:N}", campaign.Currency, LedgerAccountType.Liability, now, cancellationToken);
        var merchantAccount = await GetOrCreateLedgerAccountAsync(connection, transaction, $"MERCHANT-PAYABLE:{command.MerchantId}:{campaign.Currency}", campaign.Currency, LedgerAccountType.Liability, now, cancellationToken);
        var journal = new LedgerJournal(Guid.NewGuid(), transactionId, campaign.Currency.ToString(), now);
        journal.AddDebit(walletAccount, campaign.FinalAmount);

        if (campaign.Eligible && campaign.Sponsor == CampaignSponsor.Platform && campaign.DiscountAmount > 0m)
        {
            var expenseAccount = await GetOrCreateLedgerAccountAsync(connection, transaction, $"CAMPAIGN-EXPENSE:{campaign.Currency}", campaign.Currency, LedgerAccountType.Expense, now, cancellationToken);
            journal.AddDebit(expenseAccount, campaign.DiscountAmount);
            journal.AddCredit(merchantAccount, campaign.OriginalAmount);
        }
        else
        {
            journal.AddCredit(merchantAccount, campaign.FinalAmount);
        }
        journal.Post(now);

        await InsertJournalAsync(connection, transaction, journal, campaign.Currency, cancellationToken);
        await VerifyJournalBalanceAsync(connection, transaction, journal.Id, cancellationToken);
        await UpdateWalletAsync(connection, transaction, locked.Wallet, cancellationToken);
        await InsertCompletedIdempotencyAsync(connection, transaction, command, requestHash, transactionId, now, cancellationToken);
        await InsertOutboxAsync(connection, transaction, transactionId, command.CustomerId, command.MerchantId, campaign, now, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new PurchaseResult(transactionId, command.WalletId, command.MerchantId, campaign.OriginalAmount, campaign.DiscountAmount, campaign.FinalAmount, campaign.Currency, campaign.CampaignId, campaign.Sponsor, now, wasReplay: false);
    }

    private static async Task<Guid?> FindIdempotencyForUpdateAsync(SqlConnection connection, SqlTransaction transaction, PurchaseCommand command, string hash, CancellationToken cancellationToken)
    {
        const string sql = "SELECT RequestHash,ResourceId,Status FROM dbo.IdempotencyRecords WITH (UPDLOCK,HOLDLOCK) WHERE Scope=@Scope AND CustomerId=@CustomerId AND IdempotencyKey=@Key;";
        await using var sqlCommand = new SqlCommand(sql, connection, transaction);
        sqlCommand.Parameters.Add("@Scope", SqlDbType.NVarChar, 64).Value = IdempotencyScope;
        sqlCommand.Parameters.Add("@CustomerId", SqlDbType.UniqueIdentifier).Value = command.CustomerId;
        sqlCommand.Parameters.Add("@Key", SqlDbType.NVarChar, 128).Value = command.IdempotencyKey;
        await using var reader = await sqlCommand.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        EnsureHash(reader.GetString(0), hash);
        if (reader.GetByte(2) != IdempotencyCompleted || reader.IsDBNull(1)) throw new InvalidOperationException("Purchase idempotency state is not replayable.");
        return reader.GetGuid(1);
    }

    private static async Task<LockedPurchase?> LoadPurchaseContextForUpdateAsync(SqlConnection connection, SqlTransaction transaction, Guid customerId, Guid walletId, string merchantId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT w.Id,w.CustomerId,w.Currency,w.AvailableBalance,w.BlockedBalance,w.Status,w.CreatedAt,m.Id MerchantId
            FROM dbo.Wallets w WITH (UPDLOCK,ROWLOCK)
            INNER JOIN dbo.Merchants m WITH (UPDLOCK,HOLDLOCK) ON m.Id=@MerchantId
            WHERE w.Id=@WalletId AND w.CustomerId=@CustomerId AND w.Status=@WalletStatus AND m.Status=@MerchantStatus;
            """;
        await using var sqlCommand = new SqlCommand(sql, connection, transaction);
        sqlCommand.Parameters.Add("@MerchantId", SqlDbType.NVarChar, 64).Value = merchantId;
        sqlCommand.Parameters.Add("@WalletId", SqlDbType.UniqueIdentifier).Value = walletId;
        sqlCommand.Parameters.Add("@CustomerId", SqlDbType.UniqueIdentifier).Value = customerId;
        sqlCommand.Parameters.Add("@WalletStatus", SqlDbType.TinyInt).Value = WalletActive;
        sqlCommand.Parameters.Add("@MerchantStatus", SqlDbType.TinyInt).Value = MerchantActive;
        await using var reader = await sqlCommand.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        var wallet = Wallet.Restore(reader.GetGuid(0), reader.GetGuid(1), (CurrencyCode)reader.GetByte(2), reader.GetDecimal(3), reader.GetDecimal(4), (WalletStatus)reader.GetByte(5), reader.GetFieldValue<DateTimeOffset>(6));
        return new LockedPurchase(wallet, reader.GetString(7));
    }

    private static async Task InsertCompletedTransactionAsync(SqlConnection connection, SqlTransaction transaction, Guid id, Guid customerId, Guid walletId, Money amount, DateTimeOffset now, CancellationToken cancellationToken)
    {
        const string sql = "INSERT INTO dbo.FinancialTransactions (Id,CustomerId,Type,Status,SourceWalletId,DestinationWalletId,Currency,Amount,CreatedAt,FinalizedAt,ReversedAt,FailureCode) VALUES (@Id,@CustomerId,@Type,@Status,@WalletId,NULL,@Currency,@Amount,@Now,@Now,NULL,NULL);";
        await using var sqlCommand = new SqlCommand(sql, connection, transaction);
        sqlCommand.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = id;
        sqlCommand.Parameters.Add("@CustomerId", SqlDbType.UniqueIdentifier).Value = customerId;
        sqlCommand.Parameters.Add("@Type", SqlDbType.TinyInt).Value = (byte)FinancialTransactionType.Purchase;
        sqlCommand.Parameters.Add("@Status", SqlDbType.TinyInt).Value = TransactionCompleted;
        sqlCommand.Parameters.Add("@WalletId", SqlDbType.UniqueIdentifier).Value = walletId;
        sqlCommand.Parameters.Add("@Currency", SqlDbType.TinyInt).Value = (byte)amount.Currency;
        AddMoney(sqlCommand, "@Amount", amount.Amount);
        sqlCommand.Parameters.Add("@Now", SqlDbType.DateTimeOffset).Value = now;
        EnsureSingleRow(await sqlCommand.ExecuteNonQueryAsync(cancellationToken), "Purchase transaction insert");
    }

    private static async Task InsertDetailsAsync(SqlConnection connection, SqlTransaction transaction, Guid transactionId, PurchasePostingRequest request, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var campaign = request.Campaign;
        const string sql = """
            INSERT INTO dbo.FinancialTransactionDetails
                (FinancialTransactionId,MerchantId,CampaignReference,CampaignId,CampaignSponsorType,OriginalAmount,DiscountAmount,CorrelationId,CreatedAt)
            VALUES (@TransactionId,@MerchantId,@CampaignReference,@CampaignId,@Sponsor,@OriginalAmount,@DiscountAmount,@CorrelationId,@Now);
            """;
        await using var sqlCommand = new SqlCommand(sql, connection, transaction);
        sqlCommand.Parameters.Add("@TransactionId", SqlDbType.UniqueIdentifier).Value = transactionId;
        sqlCommand.Parameters.Add("@MerchantId", SqlDbType.NVarChar, 64).Value = request.Command.MerchantId;
        sqlCommand.Parameters.Add("@CampaignReference", SqlDbType.UniqueIdentifier).Value = campaign.ReferenceId;
        sqlCommand.Parameters.Add("@CampaignId", SqlDbType.NVarChar, 64).Value = (object?)campaign.CampaignId ?? DBNull.Value;
        sqlCommand.Parameters.Add("@Sponsor", SqlDbType.TinyInt).Value = campaign.Sponsor.HasValue ? (byte)campaign.Sponsor.Value : DBNull.Value;
        AddMoney(sqlCommand, "@OriginalAmount", campaign.OriginalAmount);
        AddMoney(sqlCommand, "@DiscountAmount", campaign.DiscountAmount);
        sqlCommand.Parameters.Add("@CorrelationId", SqlDbType.NVarChar, 64).Value = request.Command.CorrelationId;
        sqlCommand.Parameters.Add("@Now", SqlDbType.DateTimeOffset).Value = now;
        EnsureSingleRow(await sqlCommand.ExecuteNonQueryAsync(cancellationToken), "Purchase detail insert");
    }

    private static async Task UpdateWalletAsync(SqlConnection connection, SqlTransaction transaction, Wallet wallet, CancellationToken cancellationToken)
    {
        const string sql = "UPDATE dbo.Wallets SET AvailableBalance=@Available,BlockedBalance=@Blocked WHERE Id=@Id;";
        await using var sqlCommand = new SqlCommand(sql, connection, transaction);
        AddMoney(sqlCommand, "@Available", wallet.AvailableBalance);
        AddMoney(sqlCommand, "@Blocked", wallet.BlockedBalance);
        sqlCommand.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = wallet.Id;
        EnsureSingleRow(await sqlCommand.ExecuteNonQueryAsync(cancellationToken), "Purchase wallet update");
    }

    private static async Task<LedgerAccount> GetOrCreateLedgerAccountAsync(SqlConnection connection, SqlTransaction transaction, string code, CurrencyCode currency, LedgerAccountType type, DateTimeOffset now, CancellationToken cancellationToken)
    {
        const string selectSql = "SELECT Id,Code,Currency,Type,Status FROM dbo.LedgerAccounts WITH (UPDLOCK,HOLDLOCK) WHERE Code=@Code;";
        await using (var sqlCommand = new SqlCommand(selectSql, connection, transaction))
        {
            sqlCommand.Parameters.Add("@Code", SqlDbType.NVarChar, 128).Value = code;
            await using var reader = await sqlCommand.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                var existing = LedgerAccount.Restore(reader.GetGuid(0), reader.GetString(1), ((CurrencyCode)reader.GetByte(2)).ToString(), (LedgerAccountType)reader.GetByte(3), (LedgerAccountStatus)reader.GetByte(4));
                if (existing.Type != type || existing.Status != LedgerAccountStatus.Active || !string.Equals(existing.Currency, currency.ToString(), StringComparison.Ordinal)) throw new InvalidOperationException("Purchase ledger account mapping is inconsistent.");
                return existing;
            }
        }
        var account = new LedgerAccount(Guid.NewGuid(), code, currency.ToString(), type);
        const string insertSql = "INSERT INTO dbo.LedgerAccounts (Id,Code,Currency,Type,Status,CreatedAt) VALUES (@Id,@Code,@Currency,@Type,@Status,@Now);";
        await using var insert = new SqlCommand(insertSql, connection, transaction);
        insert.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = account.Id;
        insert.Parameters.Add("@Code", SqlDbType.NVarChar, 128).Value = account.Code;
        insert.Parameters.Add("@Currency", SqlDbType.TinyInt).Value = (byte)currency;
        insert.Parameters.Add("@Type", SqlDbType.TinyInt).Value = (byte)type;
        insert.Parameters.Add("@Status", SqlDbType.TinyInt).Value = (byte)account.Status;
        insert.Parameters.Add("@Now", SqlDbType.DateTimeOffset).Value = now;
        EnsureSingleRow(await insert.ExecuteNonQueryAsync(cancellationToken), "Purchase ledger account insert");
        return account;
    }

    private static async Task InsertJournalAsync(SqlConnection connection, SqlTransaction transaction, LedgerJournal journal, CurrencyCode currency, CancellationToken cancellationToken)
    {
        const string journalSql = "INSERT INTO dbo.LedgerJournals (Id,TransactionReference,Currency,Status,CreatedAt,PostedAt,ReversesJournalId) VALUES (@Id,@Reference,@Currency,@Status,@CreatedAt,@PostedAt,NULL);";
        await using (var sqlCommand = new SqlCommand(journalSql, connection, transaction))
        {
            sqlCommand.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = journal.Id;
            sqlCommand.Parameters.Add("@Reference", SqlDbType.UniqueIdentifier).Value = journal.TransactionReference;
            sqlCommand.Parameters.Add("@Currency", SqlDbType.TinyInt).Value = (byte)currency;
            sqlCommand.Parameters.Add("@Status", SqlDbType.TinyInt).Value = (byte)journal.Status;
            sqlCommand.Parameters.Add("@CreatedAt", SqlDbType.DateTimeOffset).Value = journal.CreatedAt;
            sqlCommand.Parameters.Add("@PostedAt", SqlDbType.DateTimeOffset).Value = journal.PostedAt!.Value;
            EnsureSingleRow(await sqlCommand.ExecuteNonQueryAsync(cancellationToken), "Purchase ledger journal insert");
        }
        const string entrySql = "INSERT INTO dbo.LedgerEntries (Id,JournalId,SequenceNumber,AccountId,Side,Amount,Currency) VALUES (@Id,@JournalId,@Sequence,@AccountId,@Side,@Amount,@Currency);";
        short sequence = 0;
        foreach (var entry in journal.Entries)
        {
            await using var sqlCommand = new SqlCommand(entrySql, connection, transaction);
            sqlCommand.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = entry.Id;
            sqlCommand.Parameters.Add("@JournalId", SqlDbType.UniqueIdentifier).Value = journal.Id;
            sqlCommand.Parameters.Add("@Sequence", SqlDbType.SmallInt).Value = ++sequence;
            sqlCommand.Parameters.Add("@AccountId", SqlDbType.UniqueIdentifier).Value = entry.AccountId;
            sqlCommand.Parameters.Add("@Side", SqlDbType.TinyInt).Value = (byte)entry.Side;
            AddMoney(sqlCommand, "@Amount", entry.Amount);
            sqlCommand.Parameters.Add("@Currency", SqlDbType.TinyInt).Value = (byte)currency;
            EnsureSingleRow(await sqlCommand.ExecuteNonQueryAsync(cancellationToken), "Purchase ledger entry insert");
        }
    }

    private static async Task VerifyJournalBalanceAsync(SqlConnection connection, SqlTransaction transaction, Guid journalId, CancellationToken cancellationToken)
    {
        const string sql = "SELECT COALESCE(SUM(CASE WHEN Side=1 THEN Amount ELSE 0 END),0),COALESCE(SUM(CASE WHEN Side=2 THEN Amount ELSE 0 END),0) FROM dbo.LedgerEntries WHERE JournalId=@Id;";
        await using var sqlCommand = new SqlCommand(sql, connection, transaction);
        sqlCommand.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = journalId;
        await using var reader = await sqlCommand.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) throw new InvalidOperationException("Purchase journal verification failed.");
        var debit = reader.GetDecimal(0); var credit = reader.GetDecimal(1);
        if (debit <= 0m || debit != credit) throw new UnbalancedLedgerJournalException(debit, credit);
    }

    private static async Task InsertCompletedIdempotencyAsync(SqlConnection connection, SqlTransaction transaction, PurchaseCommand command, string hash, Guid transactionId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        const string sql = "INSERT INTO dbo.IdempotencyRecords (Id,Scope,CustomerId,IdempotencyKey,RequestHash,ResourceId,Status,ResultCode,CreatedAt,UpdatedAt) VALUES (@Id,@Scope,@CustomerId,@Key,@Hash,@ResourceId,@Status,@Code,@Now,@Now);";
        await using var sqlCommand = new SqlCommand(sql, connection, transaction);
        sqlCommand.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = Guid.NewGuid();
        sqlCommand.Parameters.Add("@Scope", SqlDbType.NVarChar, 64).Value = IdempotencyScope;
        sqlCommand.Parameters.Add("@CustomerId", SqlDbType.UniqueIdentifier).Value = command.CustomerId;
        sqlCommand.Parameters.Add("@Key", SqlDbType.NVarChar, 128).Value = command.IdempotencyKey;
        sqlCommand.Parameters.Add("@Hash", SqlDbType.Char, 64).Value = hash;
        sqlCommand.Parameters.Add("@ResourceId", SqlDbType.UniqueIdentifier).Value = transactionId;
        sqlCommand.Parameters.Add("@Status", SqlDbType.TinyInt).Value = IdempotencyCompleted;
        sqlCommand.Parameters.Add("@Code", SqlDbType.NVarChar, 64).Value = "PURCHASE_COMPLETED";
        sqlCommand.Parameters.Add("@Now", SqlDbType.DateTimeOffset).Value = now;
        EnsureSingleRow(await sqlCommand.ExecuteNonQueryAsync(cancellationToken), "Purchase idempotency insert");
    }

    private static async Task InsertOutboxAsync(SqlConnection connection, SqlTransaction transaction, Guid transactionId, Guid customerId, string merchantId, CampaignEvaluationResult campaign, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(new { TransactionId = transactionId, CustomerId = customerId, MerchantId = merchantId, OriginalAmount = campaign.OriginalAmount, DiscountAmount = campaign.DiscountAmount, FinalAmount = campaign.FinalAmount, Currency = campaign.Currency.ToString(), CampaignId = campaign.CampaignId });
        const string sql = "INSERT INTO dbo.OutboxMessages (Id,MessageType,AggregateId,PayloadJson,CreatedAt,AvailableAt,AttemptCount) VALUES (@Id,@Type,@AggregateId,@Payload,@Now,@Now,0);";
        await using var sqlCommand = new SqlCommand(sql, connection, transaction);
        sqlCommand.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = Guid.NewGuid();
        sqlCommand.Parameters.Add("@Type", SqlDbType.NVarChar, 128).Value = "PURCHASE_COMPLETED";
        sqlCommand.Parameters.Add("@AggregateId", SqlDbType.UniqueIdentifier).Value = transactionId;
        sqlCommand.Parameters.Add("@Payload", SqlDbType.NVarChar, -1).Value = payload;
        sqlCommand.Parameters.Add("@Now", SqlDbType.DateTimeOffset).Value = now;
        EnsureSingleRow(await sqlCommand.ExecuteNonQueryAsync(cancellationToken), "Purchase outbox insert");
    }

    private static async Task<PurchaseResult?> LoadResultAsync(SqlConnection connection, SqlTransaction? transaction, Guid transactionId, bool wasReplay, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT t.Id,t.SourceWalletId,t.Currency,t.Amount,t.FinalizedAt,d.MerchantId,d.OriginalAmount,d.DiscountAmount,d.CampaignId,d.CampaignSponsorType
            FROM dbo.FinancialTransactions t
            INNER JOIN dbo.FinancialTransactionDetails d ON d.FinancialTransactionId=t.Id
            WHERE t.Id=@Id AND t.Type=@Type AND t.Status=@Status;
            """;
        await using var sqlCommand = new SqlCommand(sql, connection, transaction);
        sqlCommand.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = transactionId;
        sqlCommand.Parameters.Add("@Type", SqlDbType.TinyInt).Value = (byte)FinancialTransactionType.Purchase;
        sqlCommand.Parameters.Add("@Status", SqlDbType.TinyInt).Value = TransactionCompleted;
        await using var reader = await sqlCommand.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        var sponsorOrdinal = reader.GetOrdinal("CampaignSponsorType");
        CampaignSponsor? sponsor = reader.IsDBNull(sponsorOrdinal) ? null : (CampaignSponsor)reader.GetByte(sponsorOrdinal);
        var campaignOrdinal = reader.GetOrdinal("CampaignId");
        return new PurchaseResult(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetString(reader.GetOrdinal("MerchantId")),
            reader.GetDecimal(reader.GetOrdinal("OriginalAmount")),
            reader.GetDecimal(reader.GetOrdinal("DiscountAmount")),
            reader.GetDecimal(reader.GetOrdinal("Amount")),
            (CurrencyCode)reader.GetByte(reader.GetOrdinal("Currency")),
            reader.IsDBNull(campaignOrdinal) ? null : reader.GetString(campaignOrdinal),
            sponsor,
            reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("FinalizedAt")),
            wasReplay);
    }

    private static string CreateRequestHash(PurchaseCommand command)
    {
        var canonical = string.Join('|', command.WalletId.ToString("N"), command.MerchantId.ToUpperInvariant(), command.OriginalAmount.ToString("0.0000", CultureInfo.InvariantCulture));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private static void EnsureHash(string existing, string current)
    {
        if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(existing), Encoding.ASCII.GetBytes(current))) throw new PurchaseIdempotencyConflictException();
    }

    private static void AddMoney(SqlCommand command, string name, decimal value)
    {
        var parameter = command.Parameters.Add(name, SqlDbType.Decimal);
        parameter.Precision = 19; parameter.Scale = 4; parameter.Value = value;
    }

    private static void EnsureSingleRow(int count, string operation)
    {
        if (count != 1) throw new InvalidOperationException($"{operation} expected one affected row but affected {count}.");
    }

    private sealed record LockedPurchase(Wallet Wallet, string MerchantId);
}
