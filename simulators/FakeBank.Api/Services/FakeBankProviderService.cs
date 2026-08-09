using System.Collections.Concurrent;
using FakeBank.Api.Contracts;
using FakeBank.Api.Models;

namespace FakeBank.Api.Services;

/// <summary>
/// TR: FakeBank simulatorının hesap, para hareketi, provider-side idempotency ve statement state'ini process ömrü boyunca thread-safe olarak yöneten servisidir; FinWallet ledger/balance state'ine erişmez.
/// EN: Thread-safe service managing account, money-movement, provider-side idempotency and statement state for the lifetime of the FakeBank process; it never accesses FinWallet ledger/balance state.
/// </summary>
public sealed class FakeBankProviderService
{
    private readonly ConcurrentDictionary<Guid, FakeBankAccount> _accounts = new();
    private readonly ConcurrentDictionary<Guid, FakeBankTransaction> _transactions = new();
    private readonly ConcurrentDictionary<string, AccountRequestRecord> _accountRequests = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, TransactionRequestRecord> _transactionRequests = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<Guid, object> _accountLocks = new();
    private readonly ConcurrentDictionary<string, object> _requestLocks = new(StringComparer.Ordinal);

    /// <summary>
    /// TR: Currency bazlı dış banka hesabı açar; aynı request key + aynı payload tekrarında aynı hesabı döndürür, farklı payload ile key reuse edilirse conflict üretir ve eşzamanlı ilk istekleri request-key lock altında serialize eder.
    /// EN: Opens a currency-specific external bank account; repeated use of the same request key with the same payload returns the same account, reuse with a different payload produces a conflict, and concurrent first requests are serialized under a request-key lock.
    /// </summary>
    /// <param name="request">TR: Hesap açılış provider isteği. EN: Provider account-opening request.</param>
    /// <param name="pending">TR: True ise hesabın Pending durumda bırakılıp daha sonra finalize edilmesini sağlar. EN: When true, leaves the account Pending for later finalization.</param>
    /// <returns>TR: Provider hesap açılış yanıtını döndürür. EN: Returns provider account-opening response.</returns>
    public OpenAccountResponse OpenAccount(OpenAccountRequest request, bool pending)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ExternalCustomerReference == Guid.Empty) throw new ArgumentException("External customer reference cannot be empty.", nameof(request));
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Currency);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.RequestKey);

        var key = request.RequestKey.Trim();
        var currency = request.Currency.Trim().ToUpperInvariant();
        var fingerprint = $"{request.ExternalCustomerReference:N}|{currency}";

        lock (GetRequestLock($"account:{key}"))
        {
            if (_accountRequests.TryGetValue(key, out var existing))
            {
                EnsureSameFingerprint(existing.Fingerprint, fingerprint);
                return ToAccountResponse(_accounts[existing.AccountId]);
            }

            var accountId = Guid.NewGuid();
            var account = new FakeBankAccount(
                accountId,
                request.ExternalCustomerReference,
                currency,
                CreateIbanLikeNumber(accountId, currency),
                FakeBankAccountStatus.Pending,
                DateTimeOffset.UtcNow);

            if (!pending)
            {
                account.Activate();
            }

            _accounts[accountId] = account;
            _accountRequests[key] = new AccountRequestRecord(fingerprint, accountId);
            return ToAccountResponse(account);
        }
    }

    /// <summary>
    /// TR: Pending hesap açılışını provider tarafında Active duruma getirir; tekrar finalize çağrısında mevcut final state'i döndürür.
    /// EN: Finalizes a Pending account opening as Active at the provider and returns the existing final state on repeated finalization calls.
    /// </summary>
    /// <param name="accountId">TR: Finalize edilecek provider hesap kimliği. EN: Provider account identifier to finalize.</param>
    /// <returns>TR: Güncel hesap yanıtını döndürür. EN: Returns current account response.</returns>
    public OpenAccountResponse ActivateAccount(Guid accountId)
    {
        if (!_accounts.TryGetValue(accountId, out var account)) throw new KeyNotFoundException("External bank account was not found.");
        lock (GetAccountLock(accountId))
        {
            if (account.Status == FakeBankAccountStatus.Pending) account.Activate();
            return ToAccountResponse(account);
        }
    }

    /// <summary>
    /// TR: Deposit/Withdrawal provider isteğini idempotent olarak oluşturur; eşzamanlı aynı request key isteklerini serialize eder ve pending değilse finansal etkiyi account lock altında yalnızca bir kez uygular.
    /// EN: Creates a Deposit/Withdrawal provider request idempotently, serializes concurrent requests using the same request key and, when not pending, applies the financial effect exactly once under the account lock.
    /// </summary>
    /// <param name="request">TR: Harici para hareketi isteği. EN: External money-movement request.</param>
    /// <param name="pending">TR: True ise finansal etki finalize aşamasına ertelenir. EN: When true, defers financial effect until finalization.</param>
    /// <returns>TR: Provider transaction yanıtını döndürür. EN: Returns provider transaction response.</returns>
    public BankMoneyMovementResponse StartMoneyMovement(BankMoneyMovementRequest request, bool pending)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!_accounts.TryGetValue(request.AccountId, out var account)) throw new KeyNotFoundException("External bank account was not found.");
        if (request.Amount <= 0) throw new ArgumentOutOfRangeException(nameof(request));
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Currency);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.RequestKey);

        var currency = request.Currency.Trim().ToUpperInvariant();
        var key = request.RequestKey.Trim();
        var fingerprint = $"{request.AccountId:N}|{request.TransactionType}|{request.Amount:G29}|{currency}";

        lock (GetRequestLock($"transaction:{key}"))
        {
            if (_transactionRequests.TryGetValue(key, out var existing))
            {
                EnsureSameFingerprint(existing.Fingerprint, fingerprint);
                return ToTransactionResponse(_transactions[existing.TransactionId], account.Balance);
            }

            var transaction = new FakeBankTransaction(
                Guid.NewGuid(),
                request.AccountId,
                key,
                request.TransactionType,
                request.Amount,
                currency,
                FakeBankTransactionStatus.Pending,
                DateTimeOffset.UtcNow);

            _transactions[transaction.TransactionId] = transaction;
            _transactionRequests[key] = new TransactionRequestRecord(fingerprint, transaction.TransactionId);

            if (!pending)
            {
                return FinalizeTransaction(transaction.TransactionId, succeed: true);
            }

            return ToTransactionResponse(transaction, account.Balance);
        }
    }

    /// <summary>
    /// TR: Pending provider transaction'ı başarı veya hata ile finalize eder; başarıda account financial effect'i lock altında bir kez uygular ve tekrar çağrıları idempotent davranır.
    /// EN: Finalizes a Pending provider transaction as success or failure; on success it applies the account financial effect once under lock and repeated calls behave idempotently.
    /// </summary>
    /// <param name="transactionId">TR: Finalize edilecek provider transaction kimliği. EN: Provider transaction identifier to finalize.</param>
    /// <param name="succeed">TR: True ise işlemi başarıyla tamamlar, false ise provider hatası olarak finalize eder. EN: When true completes successfully; when false finalizes as provider failure.</param>
    /// <returns>TR: Güncel provider transaction yanıtını döndürür. EN: Returns current provider transaction response.</returns>
    public BankMoneyMovementResponse FinalizeTransaction(Guid transactionId, bool succeed)
    {
        if (!_transactions.TryGetValue(transactionId, out var transaction)) throw new KeyNotFoundException("External bank transaction was not found.");
        if (!_accounts.TryGetValue(transaction.AccountId, out var account)) throw new InvalidOperationException("External bank account for transaction is missing.");

        lock (GetAccountLock(account.AccountId))
        {
            if (transaction.Status != FakeBankTransactionStatus.Pending)
            {
                return ToTransactionResponse(transaction, account.Balance);
            }

            var now = DateTimeOffset.UtcNow;
            if (!succeed)
            {
                transaction.Fail(now);
                return ToTransactionResponse(transaction, account.Balance);
            }

            if (transaction.Type == FakeBankTransactionType.Deposit)
            {
                account.Credit(transaction.Amount, transaction.Currency);
            }
            else
            {
                account.Debit(transaction.Amount, transaction.Currency);
            }

            transaction.Complete(now);
            return ToTransactionResponse(transaction, account.Balance);
        }
    }

    /// <summary>
    /// TR: Provider transaction'ı benzersiz kimliğiyle bulur ve güncel durum/bakiye yanıtını döndürür.
    /// EN: Finds a provider transaction by unique identifier and returns its current state/balance response.
    /// </summary>
    /// <param name="transactionId">TR: Provider transaction kimliği. EN: Provider transaction identifier.</param>
    /// <returns>TR: Güncel transaction yanıtını döndürür. EN: Returns current transaction response.</returns>
    public BankMoneyMovementResponse GetTransaction(Guid transactionId)
    {
        if (!_transactions.TryGetValue(transactionId, out var transaction)) throw new KeyNotFoundException("External bank transaction was not found.");
        if (!_accounts.TryGetValue(transaction.AccountId, out var account)) throw new InvalidOperationException("External bank account for transaction is missing.");
        return ToTransactionResponse(transaction, account.Balance);
    }

    /// <summary>
    /// TR: Reconciliation için belirtilen harici hesaba ait tamamlanmış provider hareketlerini kronolojik statement olarak döndürür.
    /// EN: Returns completed provider movements for the specified external account as a chronological statement for reconciliation.
    /// </summary>
    /// <param name="accountId">TR: Statement alınacak provider hesap kimliği. EN: Provider account identifier whose statement is requested.</param>
    /// <returns>TR: Tamamlanmış provider statement satırlarını döndürür. EN: Returns completed provider statement items.</returns>
    public IReadOnlyCollection<BankStatementItem> GetStatement(Guid accountId)
    {
        if (!_accounts.ContainsKey(accountId)) throw new KeyNotFoundException("External bank account was not found.");
        return _transactions.Values
            .Where(transaction => transaction.AccountId == accountId && transaction.Status == FakeBankTransactionStatus.Completed && transaction.CompletedAt.HasValue)
            .OrderBy(transaction => transaction.CompletedAt)
            .Select(transaction => new BankStatementItem(transaction.TransactionId, transaction.Type, transaction.Amount, transaction.Currency, transaction.CompletedAt!.Value))
            .ToArray();
    }

    /// <summary>TR: Account mutation'larını serialize etmek için hesaba özel lock nesnesi döndürür. EN: Returns account-specific lock object used to serialize account mutations.</summary>
    /// <param name="accountId">TR: Provider hesap kimliği. EN: Provider account identifier.</param>
    /// <returns>TR: Hesaba ait lock nesnesini döndürür. EN: Returns lock object associated with account.</returns>
    private object GetAccountLock(Guid accountId) => _accountLocks.GetOrAdd(accountId, static _ => new object());

    /// <summary>TR: Aynı provider request key ile yarışan ilk create isteklerini serialize etmek için operation-prefixed request lock nesnesi döndürür. EN: Returns an operation-prefixed request lock used to serialize concurrent first-create requests sharing the same provider request key.</summary>
    /// <param name="requestKey">TR: Operation prefix içeren normalize provider request key. EN: Normalized provider request key including the operation prefix.</param>
    /// <returns>TR: Request key'e ait process-lifetime lock nesnesini döndürür. EN: Returns the process-lifetime lock object associated with the request key.</returns>
    private object GetRequestLock(string requestKey) => _requestLocks.GetOrAdd(requestKey, static _ => new object());

    /// <summary>TR: Aynı request key'in farklı payload ile tekrar kullanımını engeller. EN: Rejects reuse of the same request key with a different payload.</summary>
    /// <param name="existing">TR: İlk isteğin fingerprint'i. EN: Fingerprint from first request.</param>
    /// <param name="current">TR: Yeni isteğin fingerprint'i. EN: Fingerprint from new request.</param>
    private static void EnsureSameFingerprint(string existing, string current)
    {
        if (!string.Equals(existing, current, StringComparison.Ordinal)) throw new InvalidOperationException("Provider request key was reused with a different payload.");
    }

    /// <summary>TR: Provider hesap domain nesnesini API response modeline dönüştürür. EN: Maps provider account domain object into API response model.</summary>
    /// <param name="account">TR: Provider hesap nesnesi. EN: Provider account object.</param>
    /// <returns>TR: Hesap response döndürür. EN: Returns account response.</returns>
    private static OpenAccountResponse ToAccountResponse(FakeBankAccount account) => new(account.AccountId, account.Iban, account.Currency, account.Status);

    /// <summary>TR: Provider transaction domain nesnesini API response modeline dönüştürür. EN: Maps provider transaction domain object into API response model.</summary>
    /// <param name="transaction">TR: Provider transaction nesnesi. EN: Provider transaction object.</param>
    /// <param name="balance">TR: Yanıt anındaki provider hesap bakiyesi. EN: Provider account balance at response time.</param>
    /// <returns>TR: Transaction response döndürür. EN: Returns transaction response.</returns>
    private static BankMoneyMovementResponse ToTransactionResponse(FakeBankTransaction transaction, decimal balance) => new(transaction.TransactionId, transaction.Status, balance);

    /// <summary>TR: Simulator için IBAN-benzeri fakat gerçek banka hesabı olmayan deterministik format üretir. EN: Creates an IBAN-like simulator format that is not a real bank account.</summary>
    /// <param name="accountId">TR: Provider hesap kimliği. EN: Provider account identifier.</param>
    /// <param name="currency">TR: Hesap currency kodu. EN: Account currency code.</param>
    /// <returns>TR: IBAN-benzeri simulator hesap numarasını döndürür. EN: Returns simulator IBAN-like account number.</returns>
    private static string CreateIbanLikeNumber(Guid accountId, string currency) => $"FW{currency}{accountId:N}"[..Math.Min(28, 2 + currency.Length + 32)].ToUpperInvariant();

    /// <summary>TR: Account opening idempotency fingerprint state'ini taşır. EN: Carries account-opening idempotency fingerprint state.</summary>
    /// <param name="Fingerprint">TR: İlk request payload fingerprint'i. EN: Fingerprint of first request payload.</param>
    /// <param name="AccountId">TR: Oluşturulan provider hesap kimliği. EN: Created provider account identifier.</param>
    private sealed record AccountRequestRecord(string Fingerprint, Guid AccountId);

    /// <summary>TR: Transaction idempotency fingerprint state'ini taşır. EN: Carries transaction idempotency fingerprint state.</summary>
    /// <param name="Fingerprint">TR: İlk request payload fingerprint'i. EN: Fingerprint of first request payload.</param>
    /// <param name="TransactionId">TR: Oluşturulan provider transaction kimliği. EN: Created provider transaction identifier.</param>
    private sealed record TransactionRequestRecord(string Fingerprint, Guid TransactionId);
}
