using FinWallet.Domain.Wallets;

namespace FinWallet.Application.Wallets;

/// <summary>
/// TR: Customer/currency başına tek durable wallet kuralını DB unique constraint ile birlikte idempotent ve concurrency-safe biçimde uygular.
/// EN: Enforces one durable wallet per customer/currency idempotently and concurrency-safely together with the database unique constraint.
/// </summary>
public sealed class CreateWalletHandler
{
    private readonly IWalletStore _walletStore;
    private readonly TimeProvider _timeProvider;

    /// <summary>TR: Wallet store ve test edilebilir zaman kaynağıyla handler'ı oluşturur. EN: Creates the handler with wallet store and testable time source.</summary>
    /// <param name="walletStore">TR: Durable wallet persistence sınırı. EN: Durable wallet-persistence boundary.</param>
    /// <param name="timeProvider">TR: Test edilebilir UTC zaman kaynağı. EN: Testable UTC time source.</param>
    public CreateWalletHandler(IWalletStore walletStore, TimeProvider timeProvider)
    {
        _walletStore = walletStore ?? throw new ArgumentNullException(nameof(walletStore));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <summary>
    /// TR: Wallet mevcutsa onu döndürür; yoksa create etmeyi dener ve concurrent yarışta DB winner kaydını yeniden yükler.
    /// EN: Returns an existing wallet when present; otherwise attempts creation and reloads the database winner after a concurrent race.
    /// </summary>
    /// <param name="command">TR: Customer ve currency bilgisini taşıyan create command. EN: Create command carrying customer and currency.</param>
    /// <param name="cancellationToken">TR: SQL işlemlerine yayılan iptal sinyali. EN: Cancellation signal propagated to SQL operations.</param>
    /// <returns>TR: Güncel wallet ve bu request'in create edip etmediği bilgisini döndürür. EN: Returns current wallet and whether this request created it.</returns>
    public async Task<CreateWalletResult> HandleAsync(CreateWalletCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var existing = await _walletStore.FindByCurrencyAsync(command.CustomerId, command.Currency, cancellationToken);
        if (existing is not null)
        {
            return new CreateWalletResult(new WalletResult(existing), wasCreated: false);
        }

        var candidate = Wallet.Create(Guid.NewGuid(), command.CustomerId, command.Currency, _timeProvider.GetUtcNow());
        var inserted = await _walletStore.TryInsertAsync(candidate, cancellationToken);
        if (inserted)
        {
            return new CreateWalletResult(new WalletResult(candidate), wasCreated: true);
        }

        var winner = await _walletStore.FindByCurrencyAsync(command.CustomerId, command.Currency, cancellationToken)
            ?? throw new WalletConcurrencyException();

        return new CreateWalletResult(new WalletResult(winner), wasCreated: false);
    }
}
