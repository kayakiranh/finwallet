namespace FinWallet.Application.Transfers;

/// <summary>
/// TR: Wallet transfer fraud değerlendirmesinde kullanılacak server-side risk sinyallerini durable state'ten üreten read boundary'yi tanımlar.
/// EN: Defines the read boundary that derives server-side risk signals from durable state for wallet-transfer fraud evaluation.
/// </summary>
public interface IWalletTransferRiskSignalStore
{
    /// <summary>
    /// TR: Aktif server-side session, source/destination wallet, customer country, device history ve geçmiş transaction verisinden fraud sinyallerini üretir.
    /// EN: Derives fraud signals from active server-side session, source/destination wallets, customer country, device history and historical transaction data.
    /// </summary>
    /// <param name="customerId">TR: Authenticated customer kimliği. EN: Authenticated customer identifier.</param>
    /// <param name="sessionId">TR: JWT `sid` claim'inden gelen server-side session kimliği. EN: Server-side session identifier from the JWT `sid` claim.</param>
    /// <param name="sourceWalletId">TR: Source wallet kimliği. EN: Source-wallet identifier.</param>
    /// <param name="destinationWalletId">TR: Destination wallet kimliği. EN: Destination-wallet identifier.</param>
    /// <param name="evaluatedAt">TR: Risk window hesaplamaları için UTC evaluation zamanı. EN: UTC evaluation time used for risk windows.</param>
    /// <param name="cancellationToken">TR: MSSQL read işlemlerine yayılan iptal sinyali. EN: Cancellation signal propagated to MSSQL reads.</param>
    /// <returns>TR: Server-derived transfer risk signal setini döndürür. EN: Returns the server-derived transfer risk-signal set.</returns>
    Task<WalletTransferRiskSignals> GetAsync(Guid customerId, Guid sessionId, Guid sourceWalletId, Guid destinationWalletId, DateTimeOffset evaluatedAt, CancellationToken cancellationToken);
}
