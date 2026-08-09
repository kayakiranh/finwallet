using FinWallet.Domain.Shared;

namespace FinWallet.Application.Banking;

/// <summary>
/// TR: FinWallet use-case'lerini FakeBank veya gerçek banka HTTP sözleşmesinden ayıran dış banka provider sınırını tanımlar.
/// EN: Defines the external-bank provider boundary that decouples FinWallet use cases from FakeBank or real-bank HTTP contracts.
/// </summary>
public interface IBankProvider
{
    /// <summary>
    /// TR: Currency-specific dış banka hesabı açılışını provider idempotency anahtarıyla başlatır.
    /// EN: Starts currency-specific external-bank account opening using a provider idempotency key.
    /// </summary>
    /// <param name="externalCustomerReference">TR: PII içermeyen dış müşteri referansı. EN: Non-PII external customer reference.</param>
    /// <param name="currency">TR: Açılacak hesabın currency değeri. EN: Currency of the account to open.</param>
    /// <param name="requestKey">TR: Provider-side duplicate protection anahtarı. EN: Provider-side duplicate-protection key.</param>
    /// <param name="correlationId">TR: Dağıtık izlenebilirlik correlation kimliği. EN: Distributed-tracing correlation identifier.</param>
    /// <param name="cancellationToken">TR: Dış HTTP çağrısı iptal sinyali. EN: Cancellation signal for the external HTTP call.</param>
    /// <returns>TR: Provider hesap kimliği, IBAN, currency ve durum sonucunu döndürür. EN: Returns provider account identifier, IBAN, currency and state.</returns>
    Task<ExternalBankAccountResult> OpenAccountAsync(Guid externalCustomerReference, CurrencyCode currency, string requestKey, string correlationId, CancellationToken cancellationToken);

    /// <summary>
    /// TR: Mevcut dış banka hesabının provider durumunu sorgular.
    /// EN: Queries current provider state of an external-bank account.
    /// </summary>
    /// <param name="externalAccountId">TR: Provider hesap kimliği. EN: Provider account identifier.</param>
    /// <param name="correlationId">TR: Dağıtık izlenebilirlik correlation kimliği. EN: Distributed-tracing correlation identifier.</param>
    /// <param name="cancellationToken">TR: Dış HTTP çağrısı iptal sinyali. EN: Cancellation signal for the external HTTP call.</param>
    /// <returns>TR: Güncel provider hesap sonucunu döndürür. EN: Returns current provider account result.</returns>
    Task<ExternalBankAccountResult> GetAccountAsync(Guid externalAccountId, string correlationId, CancellationToken cancellationToken);

    /// <summary>
    /// TR: Dış banka hesabında deposit veya withdrawal işlemini provider idempotency anahtarıyla başlatır.
    /// EN: Starts a deposit or withdrawal on an external-bank account using a provider idempotency key.
    /// </summary>
    /// <param name="externalAccountId">TR: Provider hesap kimliği. EN: Provider account identifier.</param>
    /// <param name="amount">TR: Pozitif ve hesap currency'siyle uyumlu para değeri. EN: Positive monetary value matching the account currency.</param>
    /// <param name="transactionType">TR: Deposit veya Withdrawal yönü. EN: Deposit or Withdrawal direction.</param>
    /// <param name="requestKey">TR: Provider-side duplicate protection anahtarı. EN: Provider-side duplicate-protection key.</param>
    /// <param name="correlationId">TR: Dağıtık izlenebilirlik correlation kimliği. EN: Distributed-tracing correlation identifier.</param>
    /// <param name="cancellationToken">TR: Dış HTTP çağrısı iptal sinyali. EN: Cancellation signal for the external HTTP call.</param>
    /// <returns>TR: Provider transaction durumu ve bakiye snapshot sonucunu döndürür. EN: Returns provider transaction state and balance snapshot.</returns>
    Task<ExternalBankTransactionResult> StartMoneyMovementAsync(Guid externalAccountId, Money amount, BankMoneyMovementType transactionType, string requestKey, string correlationId, CancellationToken cancellationToken);

    /// <summary>
    /// TR: Provider transaction'ın güncel durumunu sorgular.
    /// EN: Queries current state of a provider transaction.
    /// </summary>
    /// <param name="externalTransactionId">TR: Provider transaction kimliği. EN: Provider transaction identifier.</param>
    /// <param name="correlationId">TR: Dağıtık izlenebilirlik correlation kimliği. EN: Distributed-tracing correlation identifier.</param>
    /// <param name="cancellationToken">TR: Dış HTTP çağrısı iptal sinyali. EN: Cancellation signal for the external HTTP call.</param>
    /// <returns>TR: Güncel provider transaction sonucunu döndürür. EN: Returns current provider transaction result.</returns>
    Task<ExternalBankTransactionResult> GetTransactionAsync(Guid externalTransactionId, string correlationId, CancellationToken cancellationToken);

    /// <summary>
    /// TR: Reconciliation için provider hesabının tamamlanmış statement hareketlerini döndürür.
    /// EN: Returns completed provider statement movements for account reconciliation.
    /// </summary>
    /// <param name="externalAccountId">TR: Provider hesap kimliği. EN: Provider account identifier.</param>
    /// <param name="correlationId">TR: Dağıtık izlenebilirlik correlation kimliği. EN: Distributed-tracing correlation identifier.</param>
    /// <param name="cancellationToken">TR: Dış HTTP çağrısı iptal sinyali. EN: Cancellation signal for the external HTTP call.</param>
    /// <returns>TR: Kronolojik provider statement satırlarını döndürür. EN: Returns chronological provider statement items.</returns>
    Task<IReadOnlyCollection<ExternalBankStatementItem>> GetStatementAsync(Guid externalAccountId, string correlationId, CancellationToken cancellationToken);
}
