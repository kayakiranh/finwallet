namespace FinWallet.Application.Wallets;

/// <summary>
/// TR: Authenticated customer'a ait wallet'ları durable store'dan okuyup Application result modellerine dönüştürür.
/// EN: Reads wallets owned by an authenticated customer from the durable store and maps them into Application result models.
/// </summary>
public sealed class ListWalletsHandler
{
    private readonly IWalletStore _walletStore;

    /// <summary>TR: Durable wallet store bağımlılığıyla handler'ı oluşturur. EN: Creates the handler with its durable wallet-store dependency.</summary>
    /// <param name="walletStore">TR: Wallet persistence sınırı. EN: Wallet-persistence boundary.</param>
    public ListWalletsHandler(IWalletStore walletStore)
    {
        _walletStore = walletStore ?? throw new ArgumentNullException(nameof(walletStore));
    }

    /// <summary>TR: Customer'a ait tüm wallet'ları listeler. EN: Lists all wallets owned by the customer.</summary>
    /// <param name="customerId">TR: Authenticated customer kimliği. EN: Authenticated customer identifier.</param>
    /// <param name="cancellationToken">TR: SQL sorgusuna yayılan iptal sinyali. EN: Cancellation signal propagated to the SQL query.</param>
    /// <returns>TR: Wallet sonuç koleksiyonunu döndürür. EN: Returns wallet-result collection.</returns>
    public async Task<IReadOnlyCollection<WalletResult>> HandleAsync(Guid customerId, CancellationToken cancellationToken)
    {
        if (customerId == Guid.Empty) throw new ArgumentException("Customer identifier cannot be empty.", nameof(customerId));
        var wallets = await _walletStore.ListOwnedAsync(customerId, cancellationToken);
        return wallets.Select(static wallet => new WalletResult(wallet)).ToArray();
    }
}
