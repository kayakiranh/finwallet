using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using FinWallet.Application.Transfers;
using FinWallet.Domain.Ledger;
using FinWallet.Domain.Shared;
using FinWallet.Domain.Transactions;
using FinWallet.Domain.Wallets;
using Microsoft.Data.SqlClient;

namespace FinWallet.Infrastructure.Persistence.SqlServer;

/// <summary>
/// TR: Wallet transfer'ın durable idempotency claim'i, wallet balance değişiklikleri, FinancialTransaction ve double-entry ledger posting'ini tek MSSQL transaction içinde atomik olarak uygular.
/// EN: Atomically applies durable idempotency claim, wallet-balance changes, FinancialTransaction and double-entry ledger posting for a wallet transfer within one MSSQL transaction.
/// </summary>
public sealed class SqlWalletTransferPostingStore : IWalletTransferPostingStore
{
    private const string IdempotencyScope = "WALLET_TRANSFER";
    private const string CompletedResultCode = "WALLET_TRANSFER_COMPLETED";
    private const byte IdempotencyProcessing = 1;
    private const byte IdempotencyCompleted = 2;
    private const byte IdempotencyFailed = 3;

    private readonly SqlConnectionFactory _connectionFactory;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// TR: SQL connection factory ve test edilebilir UTC zaman kaynağıyla atomic posting store'u oluşturur.
    /// EN: Creates the atomic posting store with its SQL connection factory and testable UTC time source.
    /// </summary>
    /// <param name="connectionFactory">TR: Pooled SQL connection factory. EN: Pooled SQL connection factory.</param>
    /// <param name="timeProvider">TR: Transaction/journal timestamp'leri için UTC zaman kaynağı. EN: UTC time source for transaction/journal timestamps.</param>
    public SqlWalletTransferPostingStore(SqlConnectionFactory connectionFactory, TimeProvider timeProvider)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <inheritdoc />
    public async Task<WalletTransferPostingResult> PostAsync(
        WalletTransferPostingRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var requestHash = CreateRequestHash(request);
        var transactionId = Guid.NewGuid();
        var idempotencyRecordId = Guid.NewGuid();
        var createdAt = _timeProvider.GetUtcNow();

        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var existingIdempotency = await FindIdempotencyForUpdateAsync(
            connection,
            transaction,
            request.CustomerId,
            request.IdempotencyKey,
            cancellationToken);

        if (existingIdempotency is not null)
        {
            EnsureSameRequestHash(existingIdempotency.RequestHash, requestHash);

            if (existingIdempotency.Status == IdempotencyCompleted)
            {
                if (existingIdempotency.ResourceId is null)
                {
                    throw new InvalidOperationException("Completed wallet-transfer idempotency record has no resource identifier.");
                }

                var replay = await LoadCompletedTransferAsync(
                    connection,
                    transaction,
                    existingIdempotency.ResourceId.Value,
                    request.CustomerId,
                    cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return replay;
            }

            if (existingIdempotency.Status == IdempotencyProcessing)
            {
                throw new WalletTransferInProgressException();
            }

            if (existingIdempotency.Status == IdempotencyFailed)
            {
                throw new InvalidOperationException("The idempotent wallet transfer previously reached a durable failed state.");
            }

            throw new InvalidOperationException("Unknown wallet-transfer idempotency state.");
        }

        await InsertIdempotencyProcessingAsync(
            connection,
            transaction,
            idempotencyRecordId,
            request,
            requestHash,
            transactionId,
            createdAt,
            cancellationToken);

        var lockedWallets = await LoadWalletsForUpdateAsync(
            connection,
            transaction,
            request.SourceWalletId,
            request.DestinationWalletId,
            cancellationToken);

        if (!lockedWallets.TryGetValue(request.SourceWalletId, out var sourceWallet) ||
            sourceWallet.CustomerId != request.CustomerId)
        {
            throw new WalletTransferSourceNotFoundException();
        }

        if (!lockedWallets.TryGetValue(request.DestinationWalletId, out var destinationWallet))
        {
            throw new WalletTransferDestinationNotFoundException();
        }

        if (sourceWallet.Status != WalletStatus.Active || destinationWallet.Status != WalletStatus.Active)
        {
            throw new WalletTransferUnavailableException();
        }

        if (sourceWallet.Currency != destinationWallet.Currency)
        {
            throw new CurrencyMismatchException(sourceWallet.Currency, destinationWallet.Currency);
        }

        var amount = new Money(request.Amount, sourceWallet.Currency);
        sourceWallet.Debit(amount);
        destinationWallet.Credit(amount);
        FinancialAmountRules.EnsureStorageCompatible(sourceWallet.AvailableBalance, nameof(sourceWallet.AvailableBalance));
        FinancialAmountRules.EnsureStorageCompatible(destinationWallet.AvailableBalance, nameof(destinationWallet.AvailableBalance));

        var sourceLedgerAccount = await GetOrCreateWalletLiabilityAccountAsync(
            connection,
            transaction,
            sourceWallet,
            createdAt,
            cancellationToken);
        var destinationLedgerAccount = await GetOrCreateWalletLiabilityAccountAsync(
            connection,
            transaction,
            destinationWallet,
            createdAt,
            cancellationToken);

        var financialTransaction = FinancialTransaction.CreateWalletTransfer(
            transactionId,
            request.CustomerId,
            request.SourceWalletId,
            request.DestinationWalletId,
            amount,
            createdAt);
        await InsertFinancialTransactionAsync(connection, transaction, financialTransaction, cancellationToken);

        var postedAt = _timeProvider.GetUtcNow();
        if (postedAt < createdAt)
        {
            postedAt = createdAt;
        }

        var journal = new LedgerJournal(Guid.NewGuid(), transactionId, sourceWallet.Currency.ToString(), createdAt);
        journal.AddDebit(sourceLedgerAccount, amount.Amount);
        journal.AddCredit(destinationLedgerAccount, amount.Amount);
        journal.Post(postedAt);

        await InsertPostedJournalAsync(connection, transaction, journal, sourceWallet.Currency, cancellationToken);
        await VerifyJournalBalanceAsync(connection, transaction, journal.Id, cancellationToken);
        await UpdateWalletBalanceAsync(connection, transaction, sourceWallet, cancellationToken);
        await UpdateWalletBalanceAsync(connection, transaction, destinationWallet, cancellationToken);

        financialTransaction.Complete(postedAt);
        await CompleteFinancialTransactionAsync(connection, transaction, financialTransaction, cancellationToken);
        await CompleteIdempotencyAsync(
            connection,
            transaction,
            idempotencyRecordId,
            requestHash,
            postedAt,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return ToResult(financialTransaction, wasReplay: false);
    }

    /// <summary>
    /// TR: Unique idempotency anahtarını Serializable transaction içinde UPDLOCK/HOLDLOCK ile arar; olmayan anahtar için de key-range lock alır.
    /// EN: Looks up the unique idempotency key with UPDLOCK/HOLDLOCK inside the Serializable transaction and acquires a key-range lock even when the key is absent.
    /// </summary>
    /// <param name="connection">TR: Açık SQL connection. EN: Open SQL connection.</param>
    /// <param name="transaction">TR: Aktif SQL transaction. EN: Active SQL transaction.</param>
    /// <param name="customerId">TR: Authenticated customer kimliği. EN: Authenticated customer identifier.</param>
    /// <param name="idempotencyKey">TR: Durable idempotency anahtarı. EN: Durable idempotency key.</param>
    /// <param name="cancellationToken">TR: SQL sorgu iptal sinyali. EN: SQL-query cancellation signal.</param>
    /// <returns>TR: Mevcut idempotency kaydını; yoksa null döndürür. EN: Returns existing idempotency record, or null when absent.</returns>
    private static async Task<IdempotencyState?> FindIdempotencyForUpdateAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        Guid customerId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT RequestHash, ResourceId, Status, ResultCode
            FROM dbo.IdempotencyRecords WITH (UPDLOCK, HOLDLOCK)
            WHERE Scope = @Scope
              AND CustomerId = @CustomerId
              AND IdempotencyKey = @IdempotencyKey;
            """;

        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@Scope", SqlDbType.NVarChar, 64).Value = IdempotencyScope;
        command.Parameters.Add("@CustomerId", SqlDbType.UniqueIdentifier).Value = customerId;
        command.Parameters.Add("@IdempotencyKey", SqlDbType.NVarChar, 128).Value = idempotencyKey;

        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var resourceOrdinal = reader.GetOrdinal("ResourceId");
        var resultCodeOrdinal = reader.GetOrdinal("ResultCode");
        return new IdempotencyState(
            reader.GetString(reader.GetOrdinal("RequestHash")),
            reader.IsDBNull(resourceOrdinal) ? null : reader.GetGuid(resourceOrdinal),
            reader.GetByte(reader.GetOrdinal("Status")),
            reader.IsDBNull(resultCodeOrdinal) ? null : reader.GetString(resultCodeOrdinal));
    }

    /// <summary>
    /// TR: Yeni Processing idempotency kaydını in-flight FinancialTransaction kimliğiyle aynı SQL transaction içine ekler.
    /// EN: Inserts a new Processing idempotency record with the in-flight FinancialTransaction identifier into the same SQL transaction.
    /// </summary>
    /// <param name="connection">TR: Açık SQL connection. EN: Open SQL connection.</param>
    /// <param name="transaction">TR: Aktif SQL transaction. EN: Active SQL transaction.</param>
    /// <param name="recordId">TR: Idempotency record kimliği. EN: Idempotency-record identifier.</param>
    /// <param name="request">TR: Transfer request. EN: Transfer request.</param>
    /// <param name="requestHash">TR: Canonical request SHA-256 hash'i. EN: Canonical request SHA-256 hash.</param>
    /// <param name="resourceId">TR: In-flight FinancialTransaction kimliği. EN: In-flight FinancialTransaction identifier.</param>
    /// <param name="createdAt">TR: Durable create UTC zamanı. EN: Durable UTC creation time.</param>
    /// <param name="cancellationToken">TR: SQL insert iptal sinyali. EN: SQL-insert cancellation signal.</param>
    private static async Task InsertIdempotencyProcessingAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        Guid recordId,
        WalletTransferPostingRequest request,
        string requestHash,
        Guid resourceId,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO dbo.IdempotencyRecords
                (Id, Scope, CustomerId, IdempotencyKey, RequestHash, ResourceId, Status, ResultCode, CreatedAt, UpdatedAt)
            VALUES
                (@Id, @Scope, @CustomerId, @IdempotencyKey, @RequestHash, @ResourceId, @Status, NULL, @CreatedAt, @UpdatedAt);
            """;

        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = recordId;
        command.Parameters.Add("@Scope", SqlDbType.NVarChar, 64).Value = IdempotencyScope;
        command.Parameters.Add("@CustomerId", SqlDbType.UniqueIdentifier).Value = request.CustomerId;
        command.Parameters.Add("@IdempotencyKey", SqlDbType.NVarChar, 128).Value = request.IdempotencyKey;
        command.Parameters.Add("@RequestHash", SqlDbType.Char, 64).Value = requestHash;
        command.Parameters.Add("@ResourceId", SqlDbType.UniqueIdentifier).Value = resourceId;
        command.Parameters.Add("@Status", SqlDbType.TinyInt).Value = IdempotencyProcessing;
        command.Parameters.Add("@CreatedAt", SqlDbType.DateTimeOffset).Value = createdAt;
        command.Parameters.Add("@UpdatedAt", SqlDbType.DateTimeOffset).Value = createdAt;
        EnsureSingleRow(await command.ExecuteNonQueryAsync(cancellationToken), "Idempotency insert");
    }

    /// <summary>
    /// TR: Source/destination wallet satırlarını deterministik GUID sırasıyla UPDLOCK altında yükler; karşılıklı transferlerde lock sırasını sabit tutar.
    /// EN: Loads source/destination wallet rows under UPDLOCK in deterministic GUID order, keeping lock order stable for opposite-direction transfers.
    /// </summary>
    /// <param name="connection">TR: Açık SQL connection. EN: Open SQL connection.</param>
    /// <param name="transaction">TR: Aktif SQL transaction. EN: Active SQL transaction.</param>
    /// <param name="sourceWalletId">TR: Source wallet kimliği. EN: Source-wallet identifier.</param>
    /// <param name="destinationWalletId">TR: Destination wallet kimliği. EN: Destination-wallet identifier.</param>
    /// <param name="cancellationToken">TR: SQL sorgu iptal sinyali. EN: SQL-query cancellation signal.</param>
    /// <returns>TR: Bulunan wallet'ları ID ile mapleyen sözlük döndürür. EN: Returns found wallets mapped by identifier.</returns>
    private static async Task<IReadOnlyDictionary<Guid, Wallet>> LoadWalletsForUpdateAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        Guid sourceWalletId,
        Guid destinationWalletId,
        CancellationToken cancellationToken)
    {
        var orderedIds = new[] { sourceWalletId, destinationWalletId };
        Array.Sort(orderedIds);
        var wallets = new Dictionary<Guid, Wallet>(2);

        foreach (var walletId in orderedIds)
        {
            var wallet = await LoadWalletForUpdateAsync(connection, transaction, walletId, cancellationToken);
            if (wallet is not null)
            {
                wallets.Add(wallet.Id, wallet);
            }
        }

        return wallets;
    }

    /// <summary>TR: Tek wallet satırını balance update için UPDLOCK/ROWLOCK altında yükler. EN: Loads one wallet row under UPDLOCK/ROWLOCK for balance update.</summary>
    /// <param name="connection">TR: Açık SQL connection. EN: Open SQL connection.</param>
    /// <param name="transaction">TR: Aktif SQL transaction. EN: Active SQL transaction.</param>
    /// <param name="walletId">TR: Yüklenecek wallet kimliği. EN: Wallet identifier to load.</param>
    /// <param name="cancellationToken">TR: SQL sorgu iptal sinyali. EN: SQL-query cancellation signal.</param>
    /// <returns>TR: Rehydrate edilmiş wallet; yoksa null. EN: Rehydrated wallet, or null when absent.</returns>
    private static async Task<Wallet?> LoadWalletForUpdateAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        Guid walletId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT Id, CustomerId, Currency, AvailableBalance, BlockedBalance, Status, CreatedAt
            FROM dbo.Wallets WITH (UPDLOCK, ROWLOCK)
            WHERE Id = @Id;
            """;

        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = walletId;
        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return Wallet.Restore(
            reader.GetGuid(reader.GetOrdinal("Id")),
            reader.GetGuid(reader.GetOrdinal("CustomerId")),
            (CurrencyCode)reader.GetByte(reader.GetOrdinal("Currency")),
            reader.GetDecimal(reader.GetOrdinal("AvailableBalance")),
            reader.GetDecimal(reader.GetOrdinal("BlockedBalance")),
            (WalletStatus)reader.GetByte(reader.GetOrdinal("Status")),
            reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("CreatedAt")));
    }

    /// <summary>
    /// TR: Wallet için stabil liability ledger account kodunu aynı SQL transaction içinde bulur veya oluşturur.
    /// EN: Finds or creates the stable wallet-liability ledger account inside the same SQL transaction.
    /// </summary>
    /// <param name="connection">TR: Açık SQL connection. EN: Open SQL connection.</param>
    /// <param name="transaction">TR: Aktif SQL transaction. EN: Active SQL transaction.</param>
    /// <param name="wallet">TR: Ledger liability account'ı gereken wallet. EN: Wallet requiring a ledger liability account.</param>
    /// <param name="createdAt">TR: Yeni account için create UTC zamanı. EN: UTC creation time for a new account.</param>
    /// <param name="cancellationToken">TR: SQL işlemleri iptal sinyali. EN: SQL-operation cancellation signal.</param>
    /// <returns>TR: Aktif wallet liability LedgerAccount döndürür. EN: Returns active wallet-liability LedgerAccount.</returns>
    private static async Task<LedgerAccount> GetOrCreateWalletLiabilityAccountAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        Wallet wallet,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken)
    {
        var code = $"WALLET-LIABILITY:{wallet.Id:N}";
        var existing = await FindLedgerAccountByCodeAsync(connection, transaction, code, cancellationToken);
        if (existing is not null)
        {
            if (!string.Equals(existing.Currency, wallet.Currency.ToString(), StringComparison.Ordinal) ||
                existing.Type != LedgerAccountType.Liability ||
                existing.Status != LedgerAccountStatus.Active)
            {
                throw new InvalidOperationException("Wallet ledger-account mapping is inconsistent.");
            }

            return existing;
        }

        var account = new LedgerAccount(Guid.NewGuid(), code, wallet.Currency.ToString(), LedgerAccountType.Liability);
        const string insertSql = """
            INSERT INTO dbo.LedgerAccounts (Id, Code, Currency, Type, Status, CreatedAt)
            VALUES (@Id, @Code, @Currency, @Type, @Status, @CreatedAt);
            """;

        await using var command = new SqlCommand(insertSql, connection, transaction);
        command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = account.Id;
        command.Parameters.Add("@Code", SqlDbType.NVarChar, 128).Value = account.Code;
        command.Parameters.Add("@Currency", SqlDbType.TinyInt).Value = (byte)wallet.Currency;
        command.Parameters.Add("@Type", SqlDbType.TinyInt).Value = (byte)account.Type;
        command.Parameters.Add("@Status", SqlDbType.TinyInt).Value = (byte)account.Status;
        command.Parameters.Add("@CreatedAt", SqlDbType.DateTimeOffset).Value = createdAt;
        EnsureSingleRow(await command.ExecuteNonQueryAsync(cancellationToken), "Ledger-account insert");
        return account;
    }

    /// <summary>TR: Ledger account'ı stabil code üzerinden transaction-scoped lock altında yükler. EN: Loads a ledger account by stable code under a transaction-scoped lock.</summary>
    /// <param name="connection">TR: Açık SQL connection. EN: Open SQL connection.</param>
    /// <param name="transaction">TR: Aktif SQL transaction. EN: Active SQL transaction.</param>
    /// <param name="code">TR: Ledger account code. EN: Ledger-account code.</param>
    /// <param name="cancellationToken">TR: SQL sorgu iptal sinyali. EN: SQL-query cancellation signal.</param>
    /// <returns>TR: Eşleşen account; yoksa null. EN: Matching account, or null when absent.</returns>
    private static async Task<LedgerAccount?> FindLedgerAccountByCodeAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        string code,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT Id, Code, Currency, Type, Status
            FROM dbo.LedgerAccounts WITH (UPDLOCK, HOLDLOCK)
            WHERE Code = @Code;
            """;

        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@Code", SqlDbType.NVarChar, 128).Value = code;
        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var currency = (CurrencyCode)reader.GetByte(reader.GetOrdinal("Currency"));
        return LedgerAccount.Restore(
            reader.GetGuid(reader.GetOrdinal("Id")),
            reader.GetString(reader.GetOrdinal("Code")),
            currency.ToString(),
            (LedgerAccountType)reader.GetByte(reader.GetOrdinal("Type")),
            (LedgerAccountStatus)reader.GetByte(reader.GetOrdinal("Status")));
    }

    /// <summary>TR: Processing FinancialTransaction kaydını aynı SQL transaction içine ekler. EN: Inserts a Processing FinancialTransaction record into the same SQL transaction.</summary>
    /// <param name="connection">TR: Açık SQL connection. EN: Open SQL connection.</param>
    /// <param name="transaction">TR: Aktif SQL transaction. EN: Active SQL transaction.</param>
    /// <param name="financialTransaction">TR: Processing FinancialTransaction aggregate'i. EN: Processing FinancialTransaction aggregate.</param>
    /// <param name="cancellationToken">TR: SQL insert iptal sinyali. EN: SQL-insert cancellation signal.</param>
    private static async Task InsertFinancialTransactionAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        FinancialTransaction financialTransaction,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO dbo.FinancialTransactions
                (Id, CustomerId, Type, Status, SourceWalletId, DestinationWalletId, Currency, Amount, CreatedAt, FinalizedAt, ReversedAt, FailureCode)
            VALUES
                (@Id, @CustomerId, @Type, @Status, @SourceWalletId, @DestinationWalletId, @Currency, @Amount, @CreatedAt, NULL, NULL, NULL);
            """;

        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = financialTransaction.Id;
        command.Parameters.Add("@CustomerId", SqlDbType.UniqueIdentifier).Value = financialTransaction.CustomerId;
        command.Parameters.Add("@Type", SqlDbType.TinyInt).Value = (byte)financialTransaction.Type;
        command.Parameters.Add("@Status", SqlDbType.TinyInt).Value = (byte)financialTransaction.Status;
        command.Parameters.Add("@SourceWalletId", SqlDbType.UniqueIdentifier).Value = financialTransaction.SourceWalletId!.Value;
        command.Parameters.Add("@DestinationWalletId", SqlDbType.UniqueIdentifier).Value = financialTransaction.DestinationWalletId!.Value;
        command.Parameters.Add("@Currency", SqlDbType.TinyInt).Value = (byte)financialTransaction.Amount.Currency;
        AddMoneyParameter(command, "@Amount", financialTransaction.Amount.Amount);
        command.Parameters.Add("@CreatedAt", SqlDbType.DateTimeOffset).Value = financialTransaction.CreatedAt;
        EnsureSingleRow(await command.ExecuteNonQueryAsync(cancellationToken), "FinancialTransaction insert");
    }

    /// <summary>TR: Posted journal ve entry'lerini aynı SQL transaction içine append eder. EN: Appends a Posted journal and its entries inside the same SQL transaction.</summary>
    /// <param name="connection">TR: Açık SQL connection. EN: Open SQL connection.</param>
    /// <param name="transaction">TR: Aktif SQL transaction. EN: Active SQL transaction.</param>
    /// <param name="journal">TR: Posted LedgerJournal aggregate'i. EN: Posted LedgerJournal aggregate.</param>
    /// <param name="currency">TR: Journal CurrencyCode değeri. EN: Journal CurrencyCode value.</param>
    /// <param name="cancellationToken">TR: SQL insert iptal sinyali. EN: SQL-insert cancellation signal.</param>
    private static async Task InsertPostedJournalAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        LedgerJournal journal,
        CurrencyCode currency,
        CancellationToken cancellationToken)
    {
        if (journal.Status != LedgerJournalStatus.Posted || journal.PostedAt is null)
        {
            throw new InvalidOperationException("Only a Posted ledger journal may be persisted by the wallet-transfer posting store.");
        }

        const string journalSql = """
            INSERT INTO dbo.LedgerJournals
                (Id, TransactionReference, Currency, Status, CreatedAt, PostedAt, ReversesJournalId)
            VALUES
                (@Id, @TransactionReference, @Currency, @Status, @CreatedAt, @PostedAt, @ReversesJournalId);
            """;

        await using (var command = new SqlCommand(journalSql, connection, transaction))
        {
            command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = journal.Id;
            command.Parameters.Add("@TransactionReference", SqlDbType.UniqueIdentifier).Value = journal.TransactionReference;
            command.Parameters.Add("@Currency", SqlDbType.TinyInt).Value = (byte)currency;
            command.Parameters.Add("@Status", SqlDbType.TinyInt).Value = (byte)journal.Status;
            command.Parameters.Add("@CreatedAt", SqlDbType.DateTimeOffset).Value = journal.CreatedAt;
            command.Parameters.Add("@PostedAt", SqlDbType.DateTimeOffset).Value = journal.PostedAt.Value;
            command.Parameters.Add("@ReversesJournalId", SqlDbType.UniqueIdentifier).Value = (object?)journal.ReversesJournalId ?? DBNull.Value;
            EnsureSingleRow(await command.ExecuteNonQueryAsync(cancellationToken), "LedgerJournal insert");
        }

        const string entrySql = """
            INSERT INTO dbo.LedgerEntries (Id, JournalId, SequenceNumber, AccountId, Side, Amount, Currency)
            VALUES (@Id, @JournalId, @SequenceNumber, @AccountId, @Side, @Amount, @Currency);
            """;

        short sequence = 0;
        foreach (var entry in journal.Entries)
        {
            sequence++;
            await using var command = new SqlCommand(entrySql, connection, transaction);
            command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = entry.Id;
            command.Parameters.Add("@JournalId", SqlDbType.UniqueIdentifier).Value = journal.Id;
            command.Parameters.Add("@SequenceNumber", SqlDbType.SmallInt).Value = sequence;
            command.Parameters.Add("@AccountId", SqlDbType.UniqueIdentifier).Value = entry.AccountId;
            command.Parameters.Add("@Side", SqlDbType.TinyInt).Value = (byte)entry.Side;
            AddMoneyParameter(command, "@Amount", entry.Amount);
            command.Parameters.Add("@Currency", SqlDbType.TinyInt).Value = (byte)currency;
            EnsureSingleRow(await command.ExecuteNonQueryAsync(cancellationToken), "LedgerEntry insert");
        }
    }

    /// <summary>
    /// TR: DB'ye yazılmış journal entry'lerini aynı transaction içinde tekrar aggregate ederek total Debit = total Credit invariant'ını COMMIT öncesi doğrular.
    /// EN: Re-aggregates persisted journal entries inside the same transaction and validates total Debit = total Credit before COMMIT.
    /// </summary>
    /// <param name="connection">TR: Açık SQL connection. EN: Open SQL connection.</param>
    /// <param name="transaction">TR: Aktif SQL transaction. EN: Active SQL transaction.</param>
    /// <param name="journalId">TR: Doğrulanacak journal kimliği. EN: Journal identifier to validate.</param>
    /// <param name="cancellationToken">TR: SQL sorgu iptal sinyali. EN: SQL-query cancellation signal.</param>
    private static async Task VerifyJournalBalanceAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        Guid journalId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                SUM(CASE WHEN Side = 1 THEN Amount ELSE CAST(0 AS DECIMAL(19,4)) END) AS TotalDebit,
                SUM(CASE WHEN Side = 2 THEN Amount ELSE CAST(0 AS DECIMAL(19,4)) END) AS TotalCredit,
                COUNT_BIG(*) AS EntryCount
            FROM dbo.LedgerEntries
            WHERE JournalId = @JournalId;
            """;

        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@JournalId", SqlDbType.UniqueIdentifier).Value = journalId;
        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException("Persisted ledger journal could not be verified.");
        }

        var totalDebit = reader.IsDBNull(reader.GetOrdinal("TotalDebit")) ? 0m : reader.GetDecimal(reader.GetOrdinal("TotalDebit"));
        var totalCredit = reader.IsDBNull(reader.GetOrdinal("TotalCredit")) ? 0m : reader.GetDecimal(reader.GetOrdinal("TotalCredit"));
        var entryCount = reader.GetInt64(reader.GetOrdinal("EntryCount"));
        if (entryCount < 2 || totalDebit <= 0m || totalCredit <= 0m || totalDebit != totalCredit)
        {
            throw new UnbalancedLedgerJournalException(totalDebit, totalCredit);
        }
    }

    /// <summary>TR: Lock altında materialize edilmiş wallet'ın yeni available/blocked balance değerlerini tek satır UPDATE ile kalıcılaştırır. EN: Persists new available/blocked balances of a locked materialized wallet with a single-row UPDATE.</summary>
    /// <param name="connection">TR: Açık SQL connection. EN: Open SQL connection.</param>
    /// <param name="transaction">TR: Aktif SQL transaction. EN: Active SQL transaction.</param>
    /// <param name="wallet">TR: Yeni balance state'ini taşıyan wallet. EN: Wallet carrying new balance state.</param>
    /// <param name="cancellationToken">TR: SQL update iptal sinyali. EN: SQL-update cancellation signal.</param>
    private static async Task UpdateWalletBalanceAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        Wallet wallet,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE dbo.Wallets
            SET AvailableBalance = @AvailableBalance,
                BlockedBalance = @BlockedBalance
            WHERE Id = @Id
              AND CustomerId = @CustomerId
              AND Currency = @Currency
              AND Status = @Status;
            """;

        await using var command = new SqlCommand(sql, connection, transaction);
        AddMoneyParameter(command, "@AvailableBalance", wallet.AvailableBalance);
        AddMoneyParameter(command, "@BlockedBalance", wallet.BlockedBalance);
        command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = wallet.Id;
        command.Parameters.Add("@CustomerId", SqlDbType.UniqueIdentifier).Value = wallet.CustomerId;
        command.Parameters.Add("@Currency", SqlDbType.TinyInt).Value = (byte)wallet.Currency;
        command.Parameters.Add("@Status", SqlDbType.TinyInt).Value = (byte)wallet.Status;
        EnsureSingleRow(await command.ExecuteNonQueryAsync(cancellationToken), "Wallet balance update");
    }

    /// <summary>TR: Processing FinancialTransaction kaydını Completed duruma final timestamp'i koruyarak geçirir. EN: Finalizes a Processing FinancialTransaction as Completed while preserving its final timestamp.</summary>
    /// <param name="connection">TR: Açık SQL connection. EN: Open SQL connection.</param>
    /// <param name="transaction">TR: Aktif SQL transaction. EN: Active SQL transaction.</param>
    /// <param name="financialTransaction">TR: Completed state'e geçirilmiş domain aggregate'i. EN: Domain aggregate transitioned to Completed.</param>
    /// <param name="cancellationToken">TR: SQL update iptal sinyali. EN: SQL-update cancellation signal.</param>
    private static async Task CompleteFinancialTransactionAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        FinancialTransaction financialTransaction,
        CancellationToken cancellationToken)
    {
        if (financialTransaction.Status != FinancialTransactionStatus.Completed || financialTransaction.FinalizedAt is null)
        {
            throw new InvalidOperationException("Financial transaction must be Completed before persistence finalization.");
        }

        const string sql = """
            UPDATE dbo.FinancialTransactions
            SET Status = @CompletedStatus,
                FinalizedAt = @FinalizedAt
            WHERE Id = @Id
              AND Status = @ProcessingStatus
              AND FinalizedAt IS NULL
              AND ReversedAt IS NULL
              AND FailureCode IS NULL;
            """;

        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@CompletedStatus", SqlDbType.TinyInt).Value = (byte)FinancialTransactionStatus.Completed;
        command.Parameters.Add("@FinalizedAt", SqlDbType.DateTimeOffset).Value = financialTransaction.FinalizedAt.Value;
        command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = financialTransaction.Id;
        command.Parameters.Add("@ProcessingStatus", SqlDbType.TinyInt).Value = (byte)FinancialTransactionStatus.Processing;
        EnsureSingleRow(await command.ExecuteNonQueryAsync(cancellationToken), "FinancialTransaction completion");
    }

    /// <summary>TR: Processing idempotency kaydını immutable transaction sonucuna işaret eden Completed state'e geçirir. EN: Transitions the Processing idempotency record into Completed state pointing at the immutable transaction result.</summary>
    /// <param name="connection">TR: Açık SQL connection. EN: Open SQL connection.</param>
    /// <param name="transaction">TR: Aktif SQL transaction. EN: Active SQL transaction.</param>
    /// <param name="recordId">TR: Idempotency record kimliği. EN: Idempotency-record identifier.</param>
    /// <param name="requestHash">TR: Beklenen canonical request hash'i. EN: Expected canonical request hash.</param>
    /// <param name="completedAt">TR: Idempotency final UTC zamanı. EN: UTC idempotency finalization time.</param>
    /// <param name="cancellationToken">TR: SQL update iptal sinyali. EN: SQL-update cancellation signal.</param>
    private static async Task CompleteIdempotencyAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        Guid recordId,
        string requestHash,
        DateTimeOffset completedAt,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE dbo.IdempotencyRecords
            SET Status = @CompletedStatus,
                ResultCode = @ResultCode,
                UpdatedAt = @UpdatedAt
            WHERE Id = @Id
              AND Scope = @Scope
              AND RequestHash = @RequestHash
              AND Status = @ProcessingStatus;
            """;

        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@CompletedStatus", SqlDbType.TinyInt).Value = IdempotencyCompleted;
        command.Parameters.Add("@ResultCode", SqlDbType.NVarChar, 64).Value = CompletedResultCode;
        command.Parameters.Add("@UpdatedAt", SqlDbType.DateTimeOffset).Value = completedAt;
        command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = recordId;
        command.Parameters.Add("@Scope", SqlDbType.NVarChar, 64).Value = IdempotencyScope;
        command.Parameters.Add("@RequestHash", SqlDbType.Char, 64).Value = requestHash;
        command.Parameters.Add("@ProcessingStatus", SqlDbType.TinyInt).Value = IdempotencyProcessing;
        EnsureSingleRow(await command.ExecuteNonQueryAsync(cancellationToken), "Idempotency completion");
    }

    /// <summary>
    /// TR: Completed idempotency replay için immutable FinancialTransaction bilgisini yükler; wallet'ın güncel değişebilir bakiyelerini response'a katmaz.
    /// EN: Loads immutable FinancialTransaction information for a completed idempotency replay and excludes current mutable wallet balances from the response.
    /// </summary>
    /// <param name="connection">TR: Açık SQL connection. EN: Open SQL connection.</param>
    /// <param name="transaction">TR: Aktif SQL transaction. EN: Active SQL transaction.</param>
    /// <param name="transactionId">TR: Replay edilecek FinancialTransaction kimliği. EN: FinancialTransaction identifier to replay.</param>
    /// <param name="customerId">TR: Idempotency owner customer kimliği. EN: Idempotency owner-customer identifier.</param>
    /// <param name="cancellationToken">TR: SQL sorgu iptal sinyali. EN: SQL-query cancellation signal.</param>
    /// <returns>TR: WasReplay=true immutable transfer sonucu döndürür. EN: Returns immutable transfer result with WasReplay=true.</returns>
    private static async Task<WalletTransferPostingResult> LoadCompletedTransferAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        Guid transactionId,
        Guid customerId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT Type, Status, SourceWalletId, DestinationWalletId, Currency, Amount, FinalizedAt
            FROM dbo.FinancialTransactions
            WHERE Id = @Id
              AND CustomerId = @CustomerId;
            """;

        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = transactionId;
        command.Parameters.Add("@CustomerId", SqlDbType.UniqueIdentifier).Value = customerId;
        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException("Completed idempotency resource transaction was not found.");
        }

        var type = (FinancialTransactionType)reader.GetByte(reader.GetOrdinal("Type"));
        var status = (FinancialTransactionStatus)reader.GetByte(reader.GetOrdinal("Status"));
        var sourceOrdinal = reader.GetOrdinal("SourceWalletId");
        var destinationOrdinal = reader.GetOrdinal("DestinationWalletId");
        var finalizedOrdinal = reader.GetOrdinal("FinalizedAt");
        if (type != FinancialTransactionType.WalletTransfer ||
            status != FinancialTransactionStatus.Completed ||
            reader.IsDBNull(sourceOrdinal) ||
            reader.IsDBNull(destinationOrdinal) ||
            reader.IsDBNull(finalizedOrdinal))
        {
            throw new InvalidOperationException("Completed idempotency resource is not a valid completed wallet transfer.");
        }

        var amount = new Money(
            reader.GetDecimal(reader.GetOrdinal("Amount")),
            (CurrencyCode)reader.GetByte(reader.GetOrdinal("Currency")));
        return new WalletTransferPostingResult(
            transactionId,
            reader.GetGuid(sourceOrdinal),
            reader.GetGuid(destinationOrdinal),
            amount,
            reader.GetFieldValue<DateTimeOffset>(finalizedOrdinal),
            wasReplay: true);
    }

    /// <summary>TR: Completed FinancialTransaction aggregate'ini immutable transfer result modeline dönüştürür. EN: Maps a Completed FinancialTransaction aggregate into the immutable transfer-result model.</summary>
    /// <param name="financialTransaction">TR: Completed WalletTransfer aggregate'i. EN: Completed WalletTransfer aggregate.</param>
    /// <param name="wasReplay">TR: Sonucun idempotent replay olup olmadığını belirtir. EN: Indicates whether the result is an idempotent replay.</param>
    /// <returns>TR: Transfer posting result döndürür. EN: Returns transfer-posting result.</returns>
    private static WalletTransferPostingResult ToResult(FinancialTransaction financialTransaction, bool wasReplay)
    {
        if (financialTransaction.Type != FinancialTransactionType.WalletTransfer ||
            financialTransaction.Status != FinancialTransactionStatus.Completed ||
            financialTransaction.SourceWalletId is null ||
            financialTransaction.DestinationWalletId is null ||
            financialTransaction.FinalizedAt is null)
        {
            throw new InvalidOperationException("Financial transaction is not a completed wallet transfer.");
        }

        return new WalletTransferPostingResult(
            financialTransaction.Id,
            financialTransaction.SourceWalletId.Value,
            financialTransaction.DestinationWalletId.Value,
            financialTransaction.Amount,
            financialTransaction.FinalizedAt.Value,
            wasReplay);
    }

    /// <summary>TR: Canonical source/destination/amount payload'ından SHA-256 request fingerprint üretir. EN: Creates a SHA-256 request fingerprint from canonical source/destination/amount payload.</summary>
    /// <param name="request">TR: Hash üretilecek transfer request. EN: Transfer request to fingerprint.</param>
    /// <returns>TR: 64 karakter uppercase hexadecimal SHA-256 hash döndürür. EN: Returns a 64-character uppercase hexadecimal SHA-256 hash.</returns>
    private static string CreateRequestHash(WalletTransferPostingRequest request)
    {
        var canonical = string.Create(
            CultureInfo.InvariantCulture,
            $"{request.SourceWalletId:N}|{request.DestinationWalletId:N}|{request.Amount:G29}");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    /// <summary>TR: Aynı idempotency key'in canonical request hash'inin değişmediğini doğrular. EN: Validates that the canonical request hash did not change for the same idempotency key.</summary>
    /// <param name="existingHash">TR: Durable önceki request hash'i. EN: Durable previous request hash.</param>
    /// <param name="currentHash">TR: Yeni request hash'i. EN: New request hash.</param>
    private static void EnsureSameRequestHash(string existingHash, string currentHash)
    {
        if (!string.Equals(existingHash, currentHash, StringComparison.Ordinal))
        {
            throw new WalletTransferIdempotencyConflictException();
        }
    }

    /// <summary>TR: SQL DECIMAL(19,4) finansal parametre ekler. EN: Adds a SQL DECIMAL(19,4) financial parameter.</summary>
    /// <param name="command">TR: Parametre eklenecek SQL komutu. EN: SQL command receiving the parameter.</param>
    /// <param name="name">TR: SQL parametre adı. EN: SQL parameter name.</param>
    /// <param name="value">TR: Finansal decimal değer. EN: Financial decimal value.</param>
    private static void AddMoneyParameter(SqlCommand command, string name, decimal value)
    {
        FinancialAmountRules.EnsureStorageCompatible(value, name);
        var parameter = command.Parameters.Add(name, SqlDbType.Decimal);
        parameter.Precision = 19;
        parameter.Scale = 4;
        parameter.Value = value;
    }

    /// <summary>TR: Tek satır değiştirmesi gereken SQL komutunu doğrular. EN: Validates a SQL command that must affect exactly one row.</summary>
    /// <param name="affectedRows">TR: Etkilenen satır sayısı. EN: Number of affected rows.</param>
    /// <param name="operation">TR: Tanılama operasyon adı. EN: Diagnostic operation name.</param>
    private static void EnsureSingleRow(int affectedRows, string operation)
    {
        if (affectedRows != 1)
        {
            throw new InvalidOperationException($"{operation} did not affect exactly one row.");
        }
    }

    /// <summary>TR: Durable idempotency lookup state'ini Infrastructure içinde taşır. EN: Carries durable idempotency lookup state inside Infrastructure.</summary>
    /// <param name="RequestHash">TR: Canonical request hash. EN: Canonical request hash.</param>
    /// <param name="ResourceId">TR: İsteğe bağlı resource transaction kimliği. EN: Optional resource-transaction identifier.</param>
    /// <param name="Status">TR: Numeric durable idempotency status. EN: Numeric durable idempotency status.</param>
    /// <param name="ResultCode">TR: Final result code; Processing ise null. EN: Final result code, or null while Processing.</param>
    private sealed record IdempotencyState(string RequestHash, Guid? ResourceId, byte Status, string? ResultCode);
}
