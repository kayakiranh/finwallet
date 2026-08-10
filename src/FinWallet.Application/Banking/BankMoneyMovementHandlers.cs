using FinWallet.Application.Cutoff;
using FinWallet.Domain.Shared;

namespace FinWallet.Application.Banking;

/// <summary>TR: Durable bank movement'ı FakeBank/real-bank provider state'iyle ilerleten ortak processor'dır. EN: Shared processor advancing a durable bank movement using FakeBank/real-bank provider state.</summary>
public sealed class BankMoneyMovementProcessor
{
    private readonly IBankMoneyMovementStore _store;
    private readonly IBankMoneyMovementFailureStore _failureStore;
    private readonly IBankProvider _bankProvider;
    private readonly TimeProvider _timeProvider;

    /// <summary>TR: Persistence, terminal-failure persistence, bank provider ve zaman bağımlılıklarıyla processor'ı oluşturur. EN: Creates the processor with persistence, terminal-failure persistence, bank-provider and time dependencies.</summary>
    /// <param name="store">TR: Durable bank movement store. EN: Durable bank-movement store.</param>
    /// <param name="failureStore">TR: Non-retryable provider hatasında blocked fund release/terminal state store'u. EN: Store releasing blocked funds and finalizing terminal state on non-retryable provider failure.</param>
    /// <param name="bankProvider">TR: External-bank ACL sınırı. EN: External-bank ACL boundary.</param>
    /// <param name="timeProvider">TR: Due-date kontrolü için UTC zaman kaynağı. EN: UTC time source for due-date checks.</param>
    public BankMoneyMovementProcessor(IBankMoneyMovementStore store, IBankMoneyMovementFailureStore failureStore, IBankProvider bankProvider, TimeProvider timeProvider)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _failureStore = failureStore ?? throw new ArgumentNullException(nameof(failureStore));
        _bankProvider = bankProvider ?? throw new ArgumentNullException(nameof(bankProvider));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <summary>TR: Scheduled işlem zamanı gelmişse provider POST/poll yapar, kalıcı provider hatasında güvenli Failed finalize eder ve normal provider sonucunu durable financial state'e uygular. EN: Starts/polls the provider when a scheduled operation is due, safely finalizes permanent provider failures and applies normal provider results to durable financial state.</summary>
    /// <param name="movement">TR: İşlenecek durable movement snapshot'ı. EN: Durable movement snapshot to process.</param>
    /// <param name="correlationId">TR: Provider'a propagate edilecek correlation kimliği. EN: Correlation identifier propagated to the provider.</param>
    /// <param name="cancellationToken">TR: HTTP/SQL iptal sinyali. EN: HTTP/SQL cancellation signal.</param>
    /// <returns>TR: Güncel durable sonucu döndürür. EN: Returns current durable result.</returns>
    public async Task<BankMoneyMovementResult> ProcessAsync(BankMoneyMovementResult movement, string correlationId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(movement);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        if (movement.State is BankMoneyMovementState.Completed or BankMoneyMovementState.Failed) return movement;

        var today = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);
        if (movement.State == BankMoneyMovementState.Scheduled && movement.ProcessingDate > today) return movement;

        try
        {
            ExternalBankTransactionResult providerResult;
            if (movement.ExternalTransactionId.HasValue)
            {
                providerResult = await _bankProvider.GetTransactionAsync(movement.ExternalTransactionId.Value, correlationId, cancellationToken);
            }
            else
            {
                providerResult = await _bankProvider.StartMoneyMovementAsync(
                    movement.ExternalAccountId,
                    movement.Amount,
                    movement.Type,
                    movement.TransactionId.ToString("N"),
                    correlationId,
                    cancellationToken);
            }

            return await _store.ApplyProviderResultAsync(movement.TransactionId, providerResult.TransactionId, providerResult.Status, cancellationToken);
        }
        catch (ExternalBankProviderException exception) when (!exception.IsRetryable)
        {
            return await _failureStore.FailAsync(movement.TransactionId, exception.Code, cancellationToken);
        }
    }
}

/// <summary>TR: Dış banka hesabından FinWallet wallet'a para girişini idempotent biçimde orkestre eder. EN: Orchestrates an idempotent money deposit from an external bank account into a FinWallet wallet.</summary>
public sealed class ExecuteBankDepositHandler
{
    private readonly IBankMoneyMovementStore _store;
    private readonly BankMoneyMovementProcessor _processor;
    private readonly TimeProvider _timeProvider;

    /// <summary>TR: Store, processor ve zaman kaynağıyla handler'ı oluşturur. EN: Creates the handler with store, processor and time source.</summary>
    /// <param name="store">TR: Durable bank movement store. EN: Durable bank-movement store.</param>
    /// <param name="processor">TR: Provider processing orchestrator'ı. EN: Provider-processing orchestrator.</param>
    /// <param name="timeProvider">TR: Operation timestamp kaynağı. EN: Operation timestamp source.</param>
    public ExecuteBankDepositHandler(IBankMoneyMovementStore store, BankMoneyMovementProcessor processor, TimeProvider timeProvider)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _processor = processor ?? throw new ArgumentNullException(nameof(processor));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <summary>TR: Banka hesabını server-side doğrular, durable idempotency kaydı oluşturur ve provider external account'tan para çekerek wallet'ı fonlar. EN: Validates the bank account server-side, creates durable idempotency state and funds the wallet by debiting the provider external account.</summary>
    /// <param name="customerId">TR: Authenticated customer kimliği. EN: Authenticated customer identifier.</param>
    /// <param name="bankAccountId">TR: Kaynak external account'a bağlı internal BankAccount kimliği. EN: Internal BankAccount identifier linked to the source external account.</param>
    /// <param name="amount">TR: Pozitif deposit tutarı. EN: Positive deposit amount.</param>
    /// <param name="idempotencyKey">TR: Client durable idempotency anahtarı. EN: Client durable-idempotency key.</param>
    /// <param name="correlationId">TR: Correlation kimliği. EN: Correlation identifier.</param>
    /// <param name="cancellationToken">TR: HTTP/SQL iptal sinyali. EN: HTTP/SQL cancellation signal.</param>
    /// <returns>TR: Completed/Pending/Failed durable deposit sonucunu döndürür. EN: Returns Completed/Pending/Failed durable deposit result.</returns>
    public async Task<BankMoneyMovementResult> HandleAsync(Guid customerId, Guid bankAccountId, decimal amount, string idempotencyKey, string correlationId, CancellationToken cancellationToken)
    {
        var context = await _store.FindContextAsync(customerId, bankAccountId, cancellationToken) ?? throw new BankMoneyMovementAccountUnavailableException();
        var now = _timeProvider.GetUtcNow();
        var date = DateOnly.FromDateTime(now.UtcDateTime);
        var prepared = await _store.PrepareAsync(
            new BankMoneyMovementPreparation(
                customerId,
                bankAccountId,
                new Money(amount, context.Currency),
                BankMoneyMovementType.Withdrawal,
                idempotencyKey,
                correlationId,
                cutoffReference: null,
                date,
                date,
                canProcessNow: true),
            cancellationToken);

        if (prepared.State is BankMoneyMovementState.Completed or BankMoneyMovementState.Failed) return prepared;
        try
        {
            return await _processor.ProcessAsync(prepared, correlationId, cancellationToken);
        }
        catch (ExternalBankProviderException exception) when (exception.IsRetryable)
        {
            return prepared;
        }
    }
}

/// <summary>TR: FinWallet wallet'tan dış banka hesabına para çıkışını cutoff ve durable fund blocking ile orkestre eder. EN: Orchestrates a withdrawal from FinWallet wallet to an external bank account using cutoff and durable fund blocking.</summary>
public sealed class ExecuteBankWithdrawalHandler
{
    private readonly IBankMoneyMovementStore _store;
    private readonly ICutoffProvider _cutoffProvider;
    private readonly BankMoneyMovementProcessor _processor;
    private readonly TimeProvider _timeProvider;

    /// <summary>TR: Store, cutoff provider, bank processor ve zaman kaynağıyla handler'ı oluşturur. EN: Creates the handler with store, cutoff provider, bank processor and time source.</summary>
    /// <param name="store">TR: Durable bank movement store. EN: Durable bank-movement store.</param>
    /// <param name="cutoffProvider">TR: External business-calendar/cutoff ACL sınırı. EN: External business-calendar/cutoff ACL boundary.</param>
    /// <param name="processor">TR: Provider processing orchestrator'ı. EN: Provider-processing orchestrator.</param>
    /// <param name="timeProvider">TR: Operation timestamp kaynağı. EN: Operation timestamp source.</param>
    public ExecuteBankWithdrawalHandler(IBankMoneyMovementStore store, ICutoffProvider cutoffProvider, BankMoneyMovementProcessor processor, TimeProvider timeProvider)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _cutoffProvider = cutoffProvider ?? throw new ArgumentNullException(nameof(cutoffProvider));
        _processor = processor ?? throw new ArgumentNullException(nameof(processor));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <summary>TR: Server-side account/country bilgisini doğrular, cutoff sonucuna göre fonu bloklar ve zamanı geldiyse external banka hesabına provider deposit başlatır. EN: Validates server-side account/country data, blocks funds according to cutoff and starts a provider deposit into the external bank account when due.</summary>
    /// <param name="customerId">TR: Authenticated customer kimliği. EN: Authenticated customer identifier.</param>
    /// <param name="bankAccountId">TR: Hedef external account'a bağlı internal BankAccount kimliği. EN: Internal BankAccount identifier linked to the destination external account.</param>
    /// <param name="amount">TR: Pozitif withdrawal tutarı. EN: Positive withdrawal amount.</param>
    /// <param name="idempotencyKey">TR: Client durable idempotency anahtarı. EN: Client durable-idempotency key.</param>
    /// <param name="correlationId">TR: Correlation kimliği. EN: Correlation identifier.</param>
    /// <param name="cancellationToken">TR: Cutoff/HTTP/SQL iptal sinyali. EN: Cutoff/HTTP/SQL cancellation signal.</param>
    /// <returns>TR: Scheduled/Pending/Completed/Failed durable withdrawal sonucunu döndürür. EN: Returns Scheduled/Pending/Completed/Failed durable withdrawal result.</returns>
    public async Task<BankMoneyMovementResult> HandleAsync(Guid customerId, Guid bankAccountId, decimal amount, string idempotencyKey, string correlationId, CancellationToken cancellationToken)
    {
        var context = await _store.FindContextAsync(customerId, bankAccountId, cancellationToken) ?? throw new BankMoneyMovementAccountUnavailableException();
        var now = _timeProvider.GetUtcNow();
        var money = new Money(amount, context.Currency);
        var cutoff = await _cutoffProvider.EvaluateAsync(context.CountryCode, context.Currency, "Withdrawal", now, correlationId, cancellationToken);
        var prepared = await _store.PrepareAsync(
            new BankMoneyMovementPreparation(
                customerId,
                bankAccountId,
                money,
                BankMoneyMovementType.Deposit,
                idempotencyKey,
                correlationId,
                cutoff.ReferenceId,
                cutoff.ProcessingDate,
                cutoff.SettlementDate,
                cutoff.CanProcessNow),
            cancellationToken);

        if (prepared.State is BankMoneyMovementState.Completed or BankMoneyMovementState.Failed or BankMoneyMovementState.Scheduled) return prepared;
        try
        {
            return await _processor.ProcessAsync(prepared, correlationId, cancellationToken);
        }
        catch (ExternalBankProviderException exception) when (exception.IsRetryable)
        {
            return prepared;
        }
    }
}
