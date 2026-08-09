namespace FinWallet.Domain.Ledger;

/// <summary>
/// TR: Ledger hesabının yeni journal entry kabul edip edemeyeceğini belirleyen yaşam döngüsü durumunu temsil eder.
/// EN: Represents the lifecycle state determining whether a ledger account may accept new journal entries.
/// </summary>
public enum LedgerAccountStatus
{
    /// <summary>TR: Hesap yeni journal entry kabul edebilir. EN: Account may accept new journal entries.</summary>
    Active = 1,

    /// <summary>TR: Hesap yeni finansal hareketlere kapalıdır ancak geçmiş ledger kayıtları korunur. EN: Account is closed to new financial movements while historical ledger entries remain preserved.</summary>
    Closed = 2
}
