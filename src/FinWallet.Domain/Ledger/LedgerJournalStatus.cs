namespace FinWallet.Domain.Ledger;

/// <summary>
/// TR: Double-entry journal'ın entry ekleme ve finansal geçmişe kesinleşme lifecycle durumunu temsil eder.
/// EN: Represents the lifecycle state controlling entry addition and finalization of a double-entry journal into financial history.
/// </summary>
public enum LedgerJournalStatus
{
    /// <summary>TR: Journal henüz kesinleşmemiştir ve entry eklenebilir. EN: Journal is not finalized yet and may accept entries.</summary>
    Draft = 1,

    /// <summary>TR: Journal dengesi doğrulanmış ve finansal geçmişe kesinleşmiştir; değiştirilemez. EN: Journal balance was validated and finalized into financial history; it is immutable.</summary>
    Posted = 2
}
