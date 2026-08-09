using FinWallet.Domain.Shared;
using FinWallet.Domain.Wallets;

namespace FinWallet.Application.Wallets;

/// <summary>
/// TR: Wallet use-case'lerini MSSQL implementasyonundan ayıran durable persistence sınırını tanımlar.
/// EN: Defines the durable persistence boundary that decouples wallet use cases from the MSSQL implementation.
/// </summary>
public interface IWalletStore
{
    /// <summary>TR: Wallet kimliği ve owner customer birlikte eşleşiyorsa cüzdanı yükler. EN: Loads a wallet when wallet identifier and owner customer both match.</summary>
    /// <param name="walletId">TR: Wallet kimliği. EN: Wallet identifier.</param>
    /// <param name="customerId">TR: Beklenen owner customer kimliği. EN: Expected owner-customer identifier.</param>
    /// <param name="cancellationToken">TR: SQL sorgu iptal sinyali. EN: SQL-query cancellation signal.</param>
    /// <returns>TR: Eşleşen wallet'ı; yoksa null döndürür. EN: Returns matching wallet, or null when not found.</returns>
    Task<Wallet?> FindOwnedAsync(Guid walletId, Guid customerId, CancellationToken cancellationToken);

    /// <summary>TR: Customer'ın belirtilen currency'deki wallet'ını yükler. EN: Loads the customer's wallet for the specified currency.</summary>
    /// <param name="customerId">TR: Owner customer kimliği. EN: Owner-customer identifier.</param>
    /// <param name="currency">TR: Aranacak wallet currency değeri. EN: Wallet currency to find.</param>
    /// <param name="cancellationToken">TR: SQL sorgu iptal sinyali. EN: SQL-query cancellation signal.</param>
    /// <returns>TR: Currency wallet'ını; yoksa null döndürür. EN: Returns the currency wallet, or null when absent.</returns>
    Task<Wallet?> FindByCurrencyAsync(Guid customerId, CurrencyCode currency, CancellationToken cancellationToken);

    /// <summary>TR: Customer'a ait tüm wallet'ları currency sırasıyla döndürür. EN: Returns all wallets owned by the customer ordered by currency.</summary>
    /// <param name="customerId">TR: Owner customer kimliği. EN: Owner-customer identifier.</param>
    /// <param name="cancellationToken">TR: SQL sorgu iptal sinyali. EN: SQL-query cancellation signal.</param>
    /// <returns>TR: Customer wallet koleksiyonunu döndürür. EN: Returns customer wallet collection.</returns>
    Task<IReadOnlyCollection<Wallet>> ListOwnedAsync(Guid customerId, CancellationToken cancellationToken);

    /// <summary>
    /// TR: Yeni wallet kaydını eklemeyi dener; aynı customer/currency için concurrent duplicate yarışını false sonucu ile bildirir.
    /// EN: Attempts to insert a new wallet and reports a concurrent duplicate customer/currency race as false.
    /// </summary>
    /// <param name="wallet">TR: Eklenecek wallet aggregate'i. EN: Wallet aggregate to insert.</param>
    /// <param name="cancellationToken">TR: SQL insert iptal sinyali. EN: SQL-insert cancellation signal.</param>
    /// <returns>TR: Insert başarılıysa true; başka request aynı customer/currency wallet'ını önce oluşturduysa false döndürür. EN: Returns true when inserted; false when another request created the same customer/currency wallet first.</returns>
    Task<bool> TryInsertAsync(Wallet wallet, CancellationToken cancellationToken);
}
