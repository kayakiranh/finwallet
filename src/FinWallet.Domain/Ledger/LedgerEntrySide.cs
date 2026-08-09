namespace FinWallet.Domain.Ledger;

/// <summary>
/// TR: Double-entry journal satırının debit veya credit tarafında yer aldığını belirtir.
/// EN: Identifies whether a double-entry journal line belongs to the debit or credit side.
/// </summary>
public enum LedgerEntrySide
{
    /// <summary>TR: Journal satırı debit tarafındadır. EN: Journal line belongs to the debit side.</summary>
    Debit = 1,

    /// <summary>TR: Journal satırı credit tarafındadır. EN: Journal line belongs to the credit side.</summary>
    Credit = 2
}
