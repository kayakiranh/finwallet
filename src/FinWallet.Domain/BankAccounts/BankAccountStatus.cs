namespace FinWallet.Domain.BankAccounts;

/// <summary>
/// TR: FinWallet içindeki banka hesabı bağlantısının dış banka açılış sürecinden kapanışa kadar geçebileceği yaşam döngüsü durumlarını tanımlar.
/// EN: Defines lifecycle states of a FinWallet bank-account connection from external-bank opening through closure.
/// </summary>
public enum BankAccountStatus
{
    /// <summary>TR: Hesap açılış isteğinin dış bankada oluşturulduğunu ancak henüz aktifleşmediğini belirtir. EN: Indicates that account opening was created at the external bank but is not yet active.</summary>
    Opening = 1,

    /// <summary>TR: Dış banka hesabının aktif ve kullanılabilir olduğunu belirtir. EN: Indicates that the external bank account is active and usable.</summary>
    Active = 2,

    /// <summary>TR: Dış bankanın hesap açılışını reddettiğini belirtir. EN: Indicates that the external bank rejected the account opening.</summary>
    Rejected = 3,

    /// <summary>TR: Dış banka hesabının yeni finansal hareketlere kapalı olacak şekilde bloke edildiğini belirtir. EN: Indicates that the external bank account is blocked from new financial movements.</summary>
    Blocked = 4,

    /// <summary>TR: Banka hesabı bağlantısının kalıcı olarak kapatıldığını belirtir. EN: Indicates that the bank-account connection has been permanently closed.</summary>
    Closed = 5
}
