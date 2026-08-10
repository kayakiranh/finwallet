using System.Security.Cryptography;
using System.Text;
using FinWallet.Application.Banking;

namespace FinWallet.Application.Inbox;

/// <summary>TR: Dış banka callback mesajının Inbox dedupe state'ini taşır. EN: Carries Inbox deduplication state of an external-bank callback message.</summary>
public sealed class InboxBeginResult
{
    /// <summary>TR: Inbox begin sonucunu oluşturur. EN: Creates Inbox-begin result.</summary>
    /// <param name="inboxId">TR: Durable Inbox row kimliği. EN: Durable Inbox-row identifier.</param>
    /// <param name="alreadyProcessed">TR: Aynı source/message daha önce başarıyla işlendi mi bilgisidir. EN: Indicates whether the same source/message was already processed successfully.</param>
    public InboxBeginResult(Guid inboxId, bool alreadyProcessed)
    {
        if (inboxId == Guid.Empty) throw new ArgumentException("Inbox identifier cannot be empty.", nameof(inboxId));
        InboxId = inboxId;
        AlreadyProcessed = alreadyProcessed;
    }

    /// <summary>TR: Durable Inbox kimliğini döndürür. EN: Gets durable Inbox identifier.</summary>
    public Guid InboxId { get; }
    /// <summary>TR: Duplicate processed bilgisini döndürür. EN: Gets duplicate-processed state.</summary>
    public bool AlreadyProcessed { get; }
}

/// <summary>TR: Inbox dedupe kayıtlarını ve external→internal bank transaction lookup'unu MSSQL implementasyonundan ayırır. EN: Decouples Inbox dedupe records and external→internal bank-transaction lookup from the MSSQL implementation.</summary>
public interface IBankCallbackInboxStore
{
    /// <summary>TR: Source+MessageId için durable Inbox kaydını oluşturur veya aynı payload duplicate'ını döndürür; aynı message id farklı payload ile gelirse conflict üretir. EN: Creates durable Inbox state for Source+MessageId or returns the same-payload duplicate; conflicting payload for the same message id is rejected.</summary>
    /// <param name="source">TR: Callback source provider adı. EN: Callback source-provider name.</param>
    /// <param name="messageId">TR: Provider mesaj kimliği. EN: Provider message identifier.</param>
    /// <param name="payloadHash">TR: Canonical callback payload SHA-256 hash'i. EN: SHA-256 hash of canonical callback payload.</param>
    /// <param name="receivedAt">TR: Callback receive UTC zamanı. EN: Callback-receive UTC timestamp.</param>
    /// <param name="cancellationToken">TR: SQL iptal sinyali. EN: SQL cancellation signal.</param>
    /// <returns>TR: Durable Inbox state'ini döndürür. EN: Returns durable Inbox state.</returns>
    Task<InboxBeginResult> BeginAsync(string source, string messageId, string payloadHash, DateTimeOffset receivedAt, CancellationToken cancellationToken);

    /// <summary>TR: Provider external transaction kimliğine bağlı FinWallet FinancialTransaction kimliğini bulur. EN: Finds FinWallet FinancialTransaction identifier linked to a provider external-transaction identifier.</summary>
    /// <param name="externalTransactionId">TR: Provider transaction kimliği. EN: Provider transaction identifier.</param>
    /// <param name="cancellationToken">TR: SQL sorgu iptal sinyali. EN: SQL-query cancellation signal.</param>
    /// <returns>TR: Internal transaction kimliği veya null döndürür. EN: Returns internal transaction identifier or null.</returns>
    Task<Guid?> FindInternalTransactionIdAsync(Guid externalTransactionId, CancellationToken cancellationToken);

    /// <summary>TR: Finansal state uygulandıktan sonra Inbox kaydını processed olarak finalize eder. EN: Finalizes Inbox state as processed after financial state is applied.</summary>
    /// <param name="inboxId">TR: Durable Inbox kimliği. EN: Durable Inbox identifier.</param>
    /// <param name="processedAt">TR: Processing completion UTC zamanı. EN: Processing-completion UTC timestamp.</param>
    /// <param name="cancellationToken">TR: SQL iptal sinyali. EN: SQL cancellation signal.</param>
    Task MarkProcessedAsync(Guid inboxId, DateTimeOffset processedAt, CancellationToken cancellationToken);
}

/// <summary>TR: Aynı provider MessageId farklı canonical payload ile tekrar kullanıldığında oluşur. EN: Raised when the same provider MessageId is reused with a different canonical payload.</summary>
public sealed class InboxMessageConflictException : Exception
{
    /// <summary>TR: Inbox payload conflict exception oluşturur. EN: Creates Inbox-payload-conflict exception.</summary>
    public InboxMessageConflictException() : base("The callback message identifier was already used with a different payload.") { }
}

/// <summary>TR: Callback external transaction FinWallet tarafında eşleşmediğinde oluşur. EN: Raised when a callback external transaction cannot be matched inside FinWallet.</summary>
public sealed class BankCallbackTransactionNotFoundException : Exception
{
    /// <summary>TR: Callback transaction not-found exception oluşturur. EN: Creates callback-transaction-not-found exception.</summary>
    public BankCallbackTransactionNotFoundException() : base("The callback transaction was not found in FinWallet.") { }
}

/// <summary>TR: Idempotent bank callback işleme sonucunu taşır. EN: Carries idempotent bank-callback processing result.</summary>
public sealed class BankCallbackResult
{
    /// <summary>TR: Callback sonucunu oluşturur. EN: Creates callback result.</summary>
    /// <param name="transactionId">TR: Internal FinancialTransaction kimliği. EN: Internal FinancialTransaction identifier.</param>
    /// <param name="state">TR: Güncel bank movement state'i. EN: Current bank-movement state.</param>
    /// <param name="wasDuplicate">TR: Callback daha önce işlendi mi bilgisidir. EN: Indicates whether callback had already been processed.</param>
    public BankCallbackResult(Guid transactionId, BankMoneyMovementState state, bool wasDuplicate)
    {
        TransactionId = transactionId;
        State = state;
        WasDuplicate = wasDuplicate;
    }

    /// <summary>TR: Internal transaction kimliğini döndürür. EN: Gets internal transaction identifier.</summary>
    public Guid TransactionId { get; }
    /// <summary>TR: Güncel movement state'ini döndürür. EN: Gets current movement state.</summary>
    public BankMoneyMovementState State { get; }
    /// <summary>TR: Duplicate callback bilgisini döndürür. EN: Gets duplicate-callback state.</summary>
    public bool WasDuplicate { get; }
}

/// <summary>TR: Bank callback'ını durable Inbox dedupe + bank movement finalization sırasıyla işler. EN: Processes bank callback using durable Inbox dedupe followed by bank-movement finalization.</summary>
public sealed class ProcessBankCallbackHandler
{
    private readonly IBankCallbackInboxStore _inboxStore;
    private readonly IBankMoneyMovementStore _movementStore;
    private readonly TimeProvider _timeProvider;

    /// <summary>TR: Inbox store, bank movement store ve UTC zaman kaynağıyla handler'ı oluşturur. EN: Creates handler with Inbox store, bank-movement store and UTC time source.</summary>
    /// <param name="inboxStore">TR: Durable callback dedupe store. EN: Durable callback-dedupe store.</param>
    /// <param name="movementStore">TR: Bank movement financial state store. EN: Bank-movement financial-state store.</param>
    /// <param name="timeProvider">TR: Receive/process UTC zaman kaynağı. EN: Receive/process UTC time source.</param>
    public ProcessBankCallbackHandler(IBankCallbackInboxStore inboxStore, IBankMoneyMovementStore movementStore, TimeProvider timeProvider)
    {
        _inboxStore = inboxStore ?? throw new ArgumentNullException(nameof(inboxStore));
        _movementStore = movementStore ?? throw new ArgumentNullException(nameof(movementStore));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <summary>TR: Aynı callback'in tekrarlarını dedupe eder; crash sonrası unprocessed Inbox tekrarında terminal financial apply'i replay-safe biçimde yeniden çalıştırır. EN: Deduplicates repeated callbacks and replay-safely re-applies terminal financial state when an unprocessed Inbox row is retried after a crash.</summary>
    /// <param name="messageId">TR: Provider message kimliği. EN: Provider message identifier.</param>
    /// <param name="externalTransactionId">TR: Provider transaction kimliği. EN: Provider transaction identifier.</param>
    /// <param name="status">TR: Provider transaction durumu. EN: Provider transaction status.</param>
    /// <param name="cancellationToken">TR: SQL iptal sinyali. EN: SQL cancellation signal.</param>
    /// <returns>TR: Internal transaction + current state + duplicate bilgisini döndürür. EN: Returns internal transaction, current state and duplicate state.</returns>
    public async Task<BankCallbackResult> HandleAsync(string messageId, Guid externalTransactionId, ExternalBankTransactionStatus status, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
        if (externalTransactionId == Guid.Empty) throw new ArgumentException("External transaction identifier cannot be empty.", nameof(externalTransactionId));
        if (!Enum.IsDefined(status)) throw new ArgumentOutOfRangeException(nameof(status));

        var canonical = $"{externalTransactionId:N}|{(int)status}";
        var payloadHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
        var begin = await _inboxStore.BeginAsync("FakeBank", messageId.Trim(), payloadHash, _timeProvider.GetUtcNow(), cancellationToken);
        var transactionId = await _inboxStore.FindInternalTransactionIdAsync(externalTransactionId, cancellationToken)
            ?? throw new BankCallbackTransactionNotFoundException();

        if (begin.AlreadyProcessed)
        {
            return new BankCallbackResult(transactionId, status switch
            {
                ExternalBankTransactionStatus.Completed => BankMoneyMovementState.Completed,
                ExternalBankTransactionStatus.Failed => BankMoneyMovementState.Failed,
                _ => BankMoneyMovementState.Pending
            }, wasDuplicate: true);
        }

        var movement = await _movementStore.ApplyProviderResultAsync(transactionId, externalTransactionId, status, cancellationToken);
        await _inboxStore.MarkProcessedAsync(begin.InboxId, _timeProvider.GetUtcNow(), cancellationToken);
        return new BankCallbackResult(transactionId, movement.State, wasDuplicate: false);
    }
}
