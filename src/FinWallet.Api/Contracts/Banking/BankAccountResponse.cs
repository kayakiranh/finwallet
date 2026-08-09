using FinWallet.Application.Banking;

namespace FinWallet.Api.Contracts.Banking;

/// <summary>
/// TR: Internal BankAccount kimliği, bağlı wallet, provider account bilgisi ve lifecycle durumunu dış API sözleşmesinde temsil eder.
/// EN: Represents internal BankAccount identifier, linked wallet, provider-account information and lifecycle state in the external API contract.
/// </summary>
public sealed class BankAccountResponse
{
    /// <summary>
    /// TR: Application BankAccount sonucunu dış API response modeline dönüştürür.
    /// EN: Converts an Application BankAccount result into the external API response model.
    /// </summary>
    /// <param name="result">TR: Application katmanından gelen BankAccount sonucu. EN: BankAccount result returned by the Application layer.</param>
    public BankAccountResponse(BankAccountResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        BankAccountId = result.BankAccountId;
        WalletId = result.WalletId;
        Currency = result.Currency.ToString();
        ExternalAccountId = result.ExternalAccountId;
        ExternalIban = result.ExternalIban;
        Status = result.Status.ToString();
    }

    /// <summary>TR: Internal BankAccount kimliğini döndürür. EN: Gets the internal BankAccount identifier.</summary>
    public Guid BankAccountId { get; }

    /// <summary>TR: Bağlı internal wallet kimliğini döndürür. EN: Gets the linked internal wallet identifier.</summary>
    public Guid WalletId { get; }

    /// <summary>TR: Hesabın ISO-benzeri currency kodunu döndürür. EN: Gets the ISO-like account currency code.</summary>
    public string Currency { get; }

    /// <summary>TR: Dış provider hesap kimliğini; henüz oluşmadıysa null döndürür. EN: Gets the external-provider account identifier, or null before creation.</summary>
    public Guid? ExternalAccountId { get; }

    /// <summary>TR: Hesap sahibine gösterilebilen provider IBAN-benzeri değeri; henüz oluşmadıysa null döndürür. EN: Gets the provider IBAN-like value visible to the account owner, or null before creation.</summary>
    public string? ExternalIban { get; }

    /// <summary>TR: Internal BankAccount lifecycle durumunu metin olarak döndürür. EN: Gets internal BankAccount lifecycle state as text.</summary>
    public string Status { get; }
}
