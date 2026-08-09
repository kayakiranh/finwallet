namespace FinWallet.Api.Contracts.Banking;

/// <summary>
/// TR: Authenticated customer'ın sahip olduğu wallet için banka hesabı açma isteğini temsil eder.
/// EN: Represents a request by an authenticated customer to open a bank account for an owned wallet.
/// </summary>
public sealed class OpenBankAccountRequest
{
    /// <summary>TR: BankAccount ile bağlanacak internal wallet kimliğini döndürür veya ayarlar. EN: Gets or sets the internal wallet identifier to link with the BankAccount.</summary>
    public Guid WalletId { get; init; }
}
