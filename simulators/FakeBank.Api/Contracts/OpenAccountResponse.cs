using FakeBank.Api.Models;

namespace FakeBank.Api.Contracts;

/// <summary>
/// TR: FakeBank hesap açılış sonucunda provider hesap kimliği, IBAN-benzeri numara, currency ve harici hesap durumunu döndürür.
/// EN: Returns provider account identifier, IBAN-like number, currency and external-account state after FakeBank account opening.
/// </summary>
public sealed class OpenAccountResponse
{
    /// <summary>TR: Hesap açılış yanıtını oluşturur. EN: Creates account-opening response.</summary>
    /// <param name="accountId">TR: Provider hesap kimliği. EN: Provider account identifier.</param>
    /// <param name="iban">TR: IBAN-benzeri provider hesap numarası. EN: IBAN-like provider account number.</param>
    /// <param name="currency">TR: Hesap currency kodu. EN: Account currency code.</param>
    /// <param name="status">TR: Provider hesap durumu. EN: Provider account state.</param>
    public OpenAccountResponse(Guid accountId, string iban, string currency, FakeBankAccountStatus status)
    {
        AccountId = accountId;
        Iban = iban;
        Currency = currency;
        Status = status;
    }

    /// <summary>TR: Provider hesap kimliğini döndürür. EN: Gets provider account identifier.</summary>
    public Guid AccountId { get; }

    /// <summary>TR: Provider IBAN-benzeri hesap numarasını döndürür. EN: Gets provider IBAN-like account number.</summary>
    public string Iban { get; }

    /// <summary>TR: Hesap currency kodunu döndürür. EN: Gets account currency code.</summary>
    public string Currency { get; }

    /// <summary>TR: Harici hesap durumunu döndürür. EN: Gets external-account state.</summary>
    public FakeBankAccountStatus Status { get; }
}
