using FinWallet.Application.Wallets;
using FinWallet.Domain.BankAccounts;

namespace FinWallet.Application.Banking;

/// <summary>
/// TR: Owned wallet doğrulaması, durable Opening kaydı, idempotent dış banka çağrısı ve CAS state güncellemesini yöneten banka hesabı açılış use-case handler'ıdır.
/// EN: Bank-account-opening use-case handler coordinating owned-wallet validation, durable Opening state, idempotent external-bank calls and CAS state updates.
/// </summary>
public sealed class OpenBankAccountHandler
{
    private readonly IWalletStore _walletStore;
    private readonly IBankAccountStore _bankAccountStore;
    private readonly IBankProvider _bankProvider;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// TR: Bank-account opening bağımlılıklarıyla handler'ı oluşturur.
    /// EN: Creates the handler with its bank-account-opening dependencies.
    /// </summary>
    /// <param name="walletStore">TR: Wallet ownership ve durable state store'u. EN: Wallet ownership and durable-state store.</param>
    /// <param name="bankAccountStore">TR: Internal BankAccount durable state store'u. EN: Internal BankAccount durable-state store.</param>
    /// <param name="bankProvider">TR: Provider bağımsız dış banka sınırı. EN: Provider-independent external-bank boundary.</param>
    /// <param name="timeProvider">TR: Test edilebilir UTC zaman kaynağı. EN: Testable UTC time source.</param>
    public OpenBankAccountHandler(
        IWalletStore walletStore,
        IBankAccountStore bankAccountStore,
        IBankProvider bankProvider,
        TimeProvider timeProvider)
    {
        _walletStore = walletStore ?? throw new ArgumentNullException(nameof(walletStore));
        _bankAccountStore = bankAccountStore ?? throw new ArgumentNullException(nameof(bankAccountStore));
        _bankProvider = bankProvider ?? throw new ArgumentNullException(nameof(bankProvider));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <summary>
    /// TR: Customer-owned wallet için banka hesabı açar veya daha önce durable olarak başlatılmış Opening akışına güvenli biçimde devam eder.
    /// EN: Opens a bank account for a customer-owned wallet or safely resumes a previously persisted Opening flow.
    /// </summary>
    /// <param name="command">TR: Authenticated customer, wallet ve correlation bilgisini taşıyan command. EN: Command carrying authenticated customer, wallet and correlation information.</param>
    /// <param name="cancellationToken">TR: DB ve dış provider işlemlerine yayılan request iptal sinyali. EN: Request cancellation signal propagated to DB and external-provider operations.</param>
    /// <returns>TR: Güncel internal BankAccount state'ini döndürür. EN: Returns current internal BankAccount state.</returns>
    public async Task<BankAccountResult> HandleAsync(OpenBankAccountCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var wallet = await _walletStore.FindOwnedAsync(command.WalletId, command.CustomerId, cancellationToken)
            ?? throw new BankAccountWalletNotFoundException();

        var bankAccount = await _bankAccountStore.FindByWalletAsync(wallet.Id, command.CustomerId, cancellationToken);
        if (bankAccount is null)
        {
            var now = _timeProvider.GetUtcNow();
            var candidate = BankAccount.Create(Guid.NewGuid(), command.CustomerId, wallet.Id, wallet.Currency, now);
            var inserted = await _bankAccountStore.TryInsertAsync(candidate, cancellationToken);

            bankAccount = inserted
                ? candidate
                : await _bankAccountStore.FindByWalletAsync(wallet.Id, command.CustomerId, cancellationToken)
                    ?? throw new BankAccountConcurrencyException();
        }

        if (bankAccount.Status != BankAccountStatus.Opening)
        {
            return ToResult(bankAccount);
        }

        var expectedStatus = bankAccount.Status;
        var expectedUpdatedAt = bankAccount.UpdatedAt;
        var providerResult = bankAccount.ExternalAccountId is null
            ? await _bankProvider.OpenAccountAsync(
                command.CustomerId,
                bankAccount.Currency,
                CreateProviderRequestKey(bankAccount.Id),
                command.CorrelationId,
                cancellationToken)
            : await _bankProvider.GetAccountAsync(
                bankAccount.ExternalAccountId.Value,
                command.CorrelationId,
                cancellationToken);

        EnsureProviderIdentity(bankAccount, providerResult);

        var stateChanged = ApplyProviderState(bankAccount, providerResult, _timeProvider.GetUtcNow());
        if (!stateChanged)
        {
            return ToResult(bankAccount);
        }

        var updated = await _bankAccountStore.TryUpdateAsync(
            bankAccount,
            expectedStatus,
            expectedUpdatedAt,
            cancellationToken);

        if (updated)
        {
            return ToResult(bankAccount);
        }

        var winner = await _bankAccountStore.FindByWalletAsync(wallet.Id, command.CustomerId, cancellationToken)
            ?? throw new BankAccountConcurrencyException();

        return ToResult(winner);
    }

    /// <summary>
    /// TR: Provider account sonucunun internal BankAccount currency ve mevcut provider identity bilgisiyle tutarlı olduğunu doğrular.
    /// EN: Validates that provider account result is consistent with internal BankAccount currency and existing provider identity.
    /// </summary>
    /// <param name="bankAccount">TR: Internal BankAccount aggregate'i. EN: Internal BankAccount aggregate.</param>
    /// <param name="providerResult">TR: Dış provider account sonucu. EN: External-provider account result.</param>
    private static void EnsureProviderIdentity(BankAccount bankAccount, ExternalBankAccountResult providerResult)
    {
        if (providerResult.Currency != bankAccount.Currency)
        {
            throw InvalidProviderState("External bank account currency does not match the internal wallet currency.");
        }

        if (bankAccount.ExternalAccountId is not null && bankAccount.ExternalAccountId != providerResult.AccountId)
        {
            throw InvalidProviderState("External bank account identifier changed unexpectedly.");
        }

        if (bankAccount.ExternalIban is not null &&
            !string.Equals(bankAccount.ExternalIban, providerResult.Iban, StringComparison.OrdinalIgnoreCase))
        {
            throw InvalidProviderState("External bank account number changed unexpectedly.");
        }
    }

    /// <summary>
    /// TR: Provider account durumunu internal Opening lifecycle state'ine uygular ve aggregate değiştiyse true döndürür.
    /// EN: Applies provider account state to the internal Opening lifecycle and returns true when the aggregate changed.
    /// </summary>
    /// <param name="bankAccount">TR: Güncellenecek internal BankAccount. EN: Internal BankAccount to update.</param>
    /// <param name="providerResult">TR: Provider account sonucu. EN: Provider account result.</param>
    /// <param name="now">TR: State değişikliklerinde kullanılacak UTC zaman. EN: UTC time used for state changes.</param>
    /// <returns>TR: Provider identity veya lifecycle state değiştiyse true döndürür. EN: Returns true when provider identity or lifecycle state changed.</returns>
    private static bool ApplyProviderState(BankAccount bankAccount, ExternalBankAccountResult providerResult, DateTimeOffset now)
    {
        var changed = false;
        if (bankAccount.ExternalAccountId is null)
        {
            bankAccount.LinkExternalAccount(providerResult.AccountId, providerResult.Iban, now);
            changed = true;
        }

        switch (providerResult.Status)
        {
            case ExternalBankAccountStatus.Pending:
                return changed;
            case ExternalBankAccountStatus.Active:
                bankAccount.Activate(now);
                return true;
            case ExternalBankAccountStatus.Rejected:
                bankAccount.Reject(now);
                return true;
            case ExternalBankAccountStatus.Blocked:
            case ExternalBankAccountStatus.Closed:
                throw InvalidProviderState("External bank returned an invalid final state during account opening.");
            default:
                throw InvalidProviderState("External bank returned an unknown account state.");
        }
    }

    /// <summary>
    /// TR: Internal BankAccount kimliğinden deterministic provider idempotency anahtarı üretir; timeout sonrası retry aynı provider request'i tekrarlar.
    /// EN: Creates a deterministic provider idempotency key from the internal BankAccount identifier so retries after timeouts repeat the same provider request.
    /// </summary>
    /// <param name="bankAccountId">TR: Internal BankAccount kimliği. EN: Internal BankAccount identifier.</param>
    /// <returns>TR: Stabil provider request key değerini döndürür. EN: Returns stable provider request-key value.</returns>
    private static string CreateProviderRequestKey(Guid bankAccountId) => $"bank-account-open:{bankAccountId:N}";

    /// <summary>TR: BankAccount aggregate'ini Application result modeline dönüştürür. EN: Maps BankAccount aggregate into the Application result model.</summary>
    /// <param name="bankAccount">TR: Dönüştürülecek BankAccount aggregate'i. EN: BankAccount aggregate to map.</param>
    /// <returns>TR: API katmanına taşınabilecek BankAccount result döndürür. EN: Returns BankAccount result suitable for the API layer.</returns>
    private static BankAccountResult ToResult(BankAccount bankAccount)
    {
        return new BankAccountResult(
            bankAccount.Id,
            bankAccount.WalletId,
            bankAccount.Currency,
            bankAccount.ExternalAccountId,
            bankAccount.ExternalIban,
            bankAccount.Status);
    }

    /// <summary>TR: Provider sözleşme tutarsızlığı için non-retryable güvenli hata üretir. EN: Creates a safe non-retryable failure for a provider-contract inconsistency.</summary>
    /// <param name="message">TR: Hassas detay içermeyen güvenli açıklama. EN: Safe description without sensitive details.</param>
    /// <returns>TR: Provider contract exception döndürür. EN: Returns provider-contract exception.</returns>
    private static ExternalBankProviderException InvalidProviderState(string message)
    {
        return new ExternalBankProviderException(
            "BANK_PROVIDER_INVALID_ACCOUNT_STATE",
            message,
            isRetryable: false);
    }
}
