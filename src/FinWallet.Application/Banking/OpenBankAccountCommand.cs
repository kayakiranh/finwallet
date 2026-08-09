namespace FinWallet.Application.Banking;

/// <summary>
/// TR: Authenticated customer'ın bir wallet için dış banka hesabı açma veya yarım kalmış açılışa devam etme isteğini taşır.
/// EN: Carries an authenticated customer's request to open an external bank account for a wallet or resume an incomplete opening.
/// </summary>
public sealed class OpenBankAccountCommand
{
    /// <summary>
    /// TR: Bank account opening command'ını oluşturur.
    /// EN: Creates the bank-account-opening command.
    /// </summary>
    /// <param name="customerId">TR: Authenticated customer kimliği. EN: Authenticated customer identifier.</param>
    /// <param name="walletId">TR: Banka hesabına bağlanacak owned wallet kimliği. EN: Owned wallet identifier to link to the bank account.</param>
    /// <param name="correlationId">TR: Request/provider izlenebilirlik correlation kimliği. EN: Request/provider tracing correlation identifier.</param>
    public OpenBankAccountCommand(Guid customerId, Guid walletId, string correlationId)
    {
        if (customerId == Guid.Empty) throw new ArgumentException("Customer identifier cannot be empty.", nameof(customerId));
        if (walletId == Guid.Empty) throw new ArgumentException("Wallet identifier cannot be empty.", nameof(walletId));
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        CustomerId = customerId;
        WalletId = walletId;
        CorrelationId = correlationId.Trim();
    }

    /// <summary>TR: Authenticated customer kimliğini döndürür. EN: Gets authenticated customer identifier.</summary>
    public Guid CustomerId { get; }

    /// <summary>TR: BankAccount ile bağlanacak wallet kimliğini döndürür. EN: Gets wallet identifier linked to the BankAccount.</summary>
    public Guid WalletId { get; }

    /// <summary>TR: Provider çağrısına propagate edilecek correlation kimliğini döndürür. EN: Gets correlation identifier propagated to the provider call.</summary>
    public string CorrelationId { get; }
}
