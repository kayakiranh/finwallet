using FinWallet.Domain.Wallets;

namespace FinWallet.Application.Wallets;

/// <summary>
/// TR: Wallet use-case'lerini MSSQL implementasyonundan ayıran durable persistence sınırını tanımlar.
/// EN: Defines the durable persistence boundary that decouples wallet use cases from the MSSQL implementation.
/// </summary>
public interface IWalletStore
{
    /// <summary>
    /// TR: Wallet kimliği ve owner customer kimliği birlikte eşleşiyorsa cüzdanı yükler; ownership kontrolünü sorgunun parçası yapar.
    /// EN: Loads a wallet when both wallet identifier and owner-customer identifier match, making ownership validation part of the query.
    /// </summary>
    /// <param name="walletId">TR: Wallet kimliği. EN: Wallet identifier.</param>
    /// <param name="customerId">TR: Beklenen owner customer kimliği. EN: Expected owner-customer identifier.</param>
    /// <param name="cancellationToken">TR: SQL sorgu iptal sinyali. EN: SQL-query cancellation signal.</param>
    /// <returns>TR: Eşleşen wallet'ı; yoksa null döndürür. EN: Returns matching wallet, or null when not found.</returns>
    Task<Wallet?> FindOwnedAsync(Guid walletId, Guid customerId, CancellationToken cancellationToken);

    /// <summary>
    /// TR: Yeni wallet kaydını durable store'a ekler; DB unique constraint aynı müşteri/currency duplicate'ını son güvence olarak engeller.
    /// EN: Inserts a new wallet into the durable store; the DB unique constraint remains the final guarantee against duplicate customer/currency wallets.
    /// </summary>
    /// <param name="wallet">TR: Eklenecek wallet aggregate'i. EN: Wallet aggregate to insert.</param>
    /// <param name="cancellationToken">TR: SQL insert iptal sinyali. EN: SQL-insert cancellation signal.</param>
    Task InsertAsync(Wallet wallet, CancellationToken cancellationToken);
}
