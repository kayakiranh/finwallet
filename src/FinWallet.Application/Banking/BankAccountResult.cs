using FinWallet.Domain.BankAccounts;
using FinWallet.Domain.Shared;

namespace FinWallet.Application.Banking;

/// <summary>
/// TR: FinWallet BankAccount use-case sonucunda API katmanına taşınabilecek provider-bağımsız hesap state'ini temsil eder.
/// EN: Represents provider-independent account state returned by a FinWallet BankAccount use case to the API layer.
/// </summary>
public sealed class BankAccountResult
{
    /// <summary>TR: BankAccount sonucunu oluşturur. EN: Creates a BankAccount result.</summary>
    /// <param name="bankAccountId">TR: Internal BankAccount kimliği. EN: Internal BankAccount identifier.</param>
    /// <param name="walletId">TR: Bağlı internal wallet kimliği. EN: Linked internal wallet identifier.</param>
    /// <param name="currency">TR: Hesap currency değeri. EN: Account currency.</param>
    /// <param name="externalAccountId">TR: Provider hesap kimliği; henüz oluşmadıysa null. EN: Provider account identifier, or null before creation.</param>
    /// <param name="externalIban">TR: Provider IBAN-benzeri hesap değeri; henüz oluşmadıysa null. EN: Provider IBAN-like account value, or null before creation.</param>
    /// <param name="status">TR: Internal BankAccount lifecycle durumu. EN: Internal BankAccount lifecycle state.</param>
    public BankAccountResult(Guid bankAccountId, Guid walletId, CurrencyCode currency, Guid? externalAccountId, string? externalIban, BankAccountStatus status)
    {
        BankAccountId = bankAccountId;
        WalletId = walletId;
        Currency = currency;
        ExternalAccountId = externalAccountId;
        ExternalIban = externalIban;
        Status = status;
    }

    /// <summary>TR: Internal BankAccount kimliğini döndürür. EN: Gets internal BankAccount identifier.</summary>
    public Guid BankAccountId { get; }

    /// <summary>TR: Bağlı wallet kimliğini döndürür. EN: Gets linked wallet identifier.</summary>
    public Guid WalletId { get; }

    /// <summary>TR: Hesap currency değerini döndürür. EN: Gets account currency.</summary>
    public CurrencyCode Currency { get; }

    /// <summary>TR: Provider account kimliğini döndürür. EN: Gets provider account identifier.</summary>
    public Guid? ExternalAccountId { get; }

    /// <summary>TR: Kullanıcının kendi hesabı için görebileceği provider IBAN-benzeri değeri döndürür. EN: Gets provider IBAN-like value visible to the owner of the account.</summary>
    public string? ExternalIban { get; }

    /// <summary>TR: Internal BankAccount lifecycle durumunu döndürür. EN: Gets internal BankAccount lifecycle state.</summary>
    public BankAccountStatus Status { get; }
}
