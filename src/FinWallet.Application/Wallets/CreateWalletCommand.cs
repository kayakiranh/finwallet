using FinWallet.Domain.Shared;

namespace FinWallet.Application.Wallets;

/// <summary>
/// TR: Authenticated customer için belirtilen currency'de wallet oluşturma isteğini taşır.
/// EN: Carries a request to create a wallet in the specified currency for an authenticated customer.
/// </summary>
public sealed class CreateWalletCommand
{
    /// <summary>TR: Create-wallet command'ını oluşturur. EN: Creates the create-wallet command.</summary>
    /// <param name="customerId">TR: Authenticated customer kimliği. EN: Authenticated customer identifier.</param>
    /// <param name="currency">TR: Oluşturulacak wallet currency değeri. EN: Currency of the wallet to create.</param>
    public CreateWalletCommand(Guid customerId, CurrencyCode currency)
    {
        if (customerId == Guid.Empty) throw new ArgumentException("Customer identifier cannot be empty.", nameof(customerId));
        CustomerId = customerId;
        Currency = currency;
    }

    /// <summary>TR: Authenticated customer kimliğini döndürür. EN: Gets authenticated customer identifier.</summary>
    public Guid CustomerId { get; }

    /// <summary>TR: Wallet currency değerini döndürür. EN: Gets wallet currency.</summary>
    public CurrencyCode Currency { get; }
}
