namespace FinWallet.Application.Banking;

/// <summary>
/// TR: Application katmanında dış bankanın hesap durumunu provider enumundan bağımsız biçimde temsil eder.
/// EN: Represents an external-bank account state in the Application layer independently from provider enums.
/// </summary>
public enum ExternalBankAccountStatus
{
    /// <summary>TR: Hesap açılışı provider tarafında beklemektedir. EN: Account opening is pending at the provider.</summary>
    Pending = 1,

    /// <summary>TR: Harici banka hesabı aktiftir. EN: External bank account is active.</summary>
    Active = 2,

    /// <summary>TR: Hesap açılışı reddedilmiştir. EN: Account opening was rejected.</summary>
    Rejected = 3,

    /// <summary>TR: Harici banka hesabı bloke edilmiştir. EN: External bank account is blocked.</summary>
    Blocked = 4,

    /// <summary>TR: Harici banka hesabı kapatılmıştır. EN: External bank account is closed.</summary>
    Closed = 5
}
