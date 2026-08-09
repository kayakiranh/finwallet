namespace FakeBank.Api.Models;

/// <summary>
/// TR: FakeBank tarafındaki harici banka hesabının yaşam döngüsü durumunu temsil eder ve FinWallet içindeki Wallet/BankAccount durumlarından bağımsızdır.
/// EN: Represents the lifecycle state of an external bank account inside FakeBank and remains independent from FinWallet Wallet/BankAccount states.
/// </summary>
public enum FakeBankAccountStatus
{
    /// <summary>TR: Hesap açılış talebi provider tarafından alınmış ancak henüz sonuçlanmamıştır. EN: Account-opening request was accepted by the provider but has not completed yet.</summary>
    Pending = 1,

    /// <summary>TR: Harici banka hesabı aktif ve banka işlemlerine açıktır. EN: External bank account is active and available for banking operations.</summary>
    Active = 2,

    /// <summary>TR: Hesap açılış talebi provider kuralları tarafından reddedilmiştir. EN: Account-opening request was rejected by provider rules.</summary>
    Rejected = 3,

    /// <summary>TR: Harici banka hesabı provider tarafından bloke edilmiştir. EN: External bank account has been blocked by the provider.</summary>
    Blocked = 4,

    /// <summary>TR: Harici banka hesabı kalıcı olarak kapatılmıştır. EN: External bank account has been permanently closed.</summary>
    Closed = 5
}
