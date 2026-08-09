namespace FinWallet.Domain.Ledger;

/// <summary>
/// TR: Double-entry journal debit ve credit toplamları eşit olmadığı için finansal geçmişe post edilemediğinde oluşur.
/// EN: Thrown when a double-entry journal cannot be posted to financial history because total debit and credit amounts are not equal.
/// </summary>
public sealed class UnbalancedLedgerJournalException : InvalidOperationException
{
    /// <summary>
    /// TR: Dengesiz journal için debit/credit toplamlarını açıklayan domain hatasını oluşturur.
    /// EN: Creates the domain error describing debit/credit totals for an unbalanced journal.
    /// </summary>
    /// <param name="totalDebit">TR: Journal debit toplamı. EN: Journal total debit.</param>
    /// <param name="totalCredit">TR: Journal credit toplamı. EN: Journal total credit.</param>
    public UnbalancedLedgerJournalException(decimal totalDebit, decimal totalCredit)
        : base($"Ledger journal is unbalanced. Debit={totalDebit}, Credit={totalCredit}.")
    {
    }
}
