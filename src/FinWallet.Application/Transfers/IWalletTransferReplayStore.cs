namespace FinWallet.Application.Transfers;

/// <summary>
/// TR: Pahalı fraud preflight'i tekrar çalıştırmadan önce durable idempotency state'inden tamamlanmış wallet-transfer sonucunu read-only olarak çözümleyen sınırı tanımlar.
/// EN: Defines a read-only boundary that resolves a completed wallet-transfer result from durable idempotency state before re-running expensive fraud preflight work.
/// </summary>
public interface IWalletTransferReplayStore
{
    /// <summary>
    /// TR: Aynı customer/idempotency key için Completed transfer varsa immutable sonucu döndürür; kayıt yoksa null, payload farklıysa conflict üretir.
    /// EN: Returns the immutable result when a Completed transfer exists for the same customer/idempotency key; returns null when absent and raises a conflict when the payload differs.
    /// </summary>
    /// <param name="request">TR: Replay eşleşmesi yapılacak wallet-transfer request. EN: Wallet-transfer request used for replay matching.</param>
    /// <param name="cancellationToken">TR: MSSQL read işlemlerine yayılan iptal sinyali. EN: Cancellation signal propagated to MSSQL reads.</param>
    /// <returns>TR: Completed replay sonucu veya idempotency kaydı yoksa null döndürür. EN: Returns the completed replay result, or null when no idempotency record exists.</returns>
    Task<WalletTransferPostingResult?> TryGetCompletedAsync(WalletTransferPostingRequest request, CancellationToken cancellationToken);
}
