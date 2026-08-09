namespace FinWallet.Domain.Transactions;

/// <summary>
/// TR: Durable finansal transaction'ın işlem başlangıcından final/reversal durumuna kadar lifecycle state'lerini MSSQL şemasıyla aynı stabil numeric değerlerle tanımlar.
/// EN: Defines durable financial-transaction lifecycle states using stable numeric values aligned with the MSSQL schema.
/// </summary>
public enum FinancialTransactionStatus
{
    /// <summary>TR: Transaction durable olarak oluşturulmuş ancak henüz final değildir. EN: Transaction exists durably but is not final yet.</summary>
    Processing = 1,

    /// <summary>TR: Transaction finansal etkisi başarıyla kesinleşmiştir. EN: Transaction financial effect completed successfully.</summary>
    Completed = 2,

    /// <summary>TR: Transaction final olarak başarısız olmuştur. EN: Transaction failed as a final outcome.</summary>
    Failed = 3,

    /// <summary>TR: Transaction etkisi ayrı reversal transaction/journal ile terslenmiştir. EN: Transaction effect was reversed by a separate reversal transaction/journal.</summary>
    Reversed = 4
}
