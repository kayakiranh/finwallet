namespace FinWallet.Application.Transfers;

/// <summary>
/// TR: Wallet balance, FinancialTransaction, LedgerJournal/Entries ve durable idempotency sonucunu tek MSSQL transaction içinde atomik olarak post eden persistence sınırını tanımlar.
/// EN: Defines the persistence boundary that atomically posts wallet balances, FinancialTransaction, LedgerJournal/Entries and durable idempotency result within one MSSQL transaction.
/// </summary>
public interface IWalletTransferPostingStore
{
    /// <summary>
    /// TR: Transfer request'ini atomik olarak post eder veya aynı idempotency key/request için daha önce tamamlanmış immutable transaction sonucunu döndürür.
    /// EN: Atomically posts a transfer request or returns the previously completed immutable transaction result for the same idempotency key/request.
    /// </summary>
    /// <param name="request">TR: Source/destination wallet, amount ve idempotency bilgisini taşıyan request. EN: Request carrying source/destination wallet, amount and idempotency information.</param>
    /// <param name="cancellationToken">TR: MSSQL transaction ve komutlarına yayılan iptal sinyali. EN: Cancellation signal propagated to the MSSQL transaction and commands.</param>
    /// <returns>TR: Yeni veya replay edilmiş Completed transfer sonucunu döndürür. EN: Returns newly completed or replayed Completed transfer result.</returns>
    Task<WalletTransferPostingResult> PostAsync(WalletTransferPostingRequest request, CancellationToken cancellationToken);
}
