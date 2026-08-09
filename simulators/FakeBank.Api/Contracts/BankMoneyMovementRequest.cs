using FakeBank.Api.Models;

namespace FakeBank.Api.Contracts;

/// <summary>
/// TR: FakeBank harici hesabında deposit veya withdrawal başlatmak için account, tutar, currency, işlem tipi ve idempotency request anahtarını taşır.
/// EN: Carries account, amount, currency, transaction type and idempotency request key required to initiate a deposit or withdrawal on a FakeBank external account.
/// </summary>
public sealed class BankMoneyMovementRequest
{
    /// <summary>TR: İşlemin uygulanacağı provider hesap kimliğini döndürür veya ayarlar. EN: Gets or sets provider account identifier on which the transaction is applied.</summary>
    public Guid AccountId { get; init; }

    /// <summary>TR: Pozitif işlem tutarını döndürür veya ayarlar. EN: Gets or sets positive transaction amount.</summary>
    public decimal Amount { get; init; }

    /// <summary>TR: İşlem currency kodunu döndürür veya ayarlar. EN: Gets or sets transaction currency code.</summary>
    public string Currency { get; init; } = string.Empty;

    /// <summary>TR: Deposit veya Withdrawal işlem tipini döndürür veya ayarlar. EN: Gets or sets Deposit or Withdrawal transaction type.</summary>
    public FakeBankTransactionType TransactionType { get; init; }

    /// <summary>TR: Provider-side duplicate korumasında kullanılan request anahtarını döndürür veya ayarlar. EN: Gets or sets provider-side request key used for duplicate protection.</summary>
    public string RequestKey { get; init; } = string.Empty;
}
