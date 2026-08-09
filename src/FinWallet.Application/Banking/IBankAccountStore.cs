using FinWallet.Domain.BankAccounts;

namespace FinWallet.Application.Banking;

/// <summary>
/// TR: BankAccount use-case'lerini MSSQL implementasyonundan ayıran durable persistence sınırını tanımlar.
/// EN: Defines the durable persistence boundary that decouples BankAccount use cases from the MSSQL implementation.
/// </summary>
public interface IBankAccountStore
{
    /// <summary>
    /// TR: Internal banka hesabını kimlik ve owner customer ile birlikte yükler.
    /// EN: Loads an internal bank account by identifier together with its owner-customer identifier.
    /// </summary>
    /// <param name="bankAccountId">TR: Internal BankAccount kimliği. EN: Internal BankAccount identifier.</param>
    /// <param name="customerId">TR: Beklenen owner customer kimliği. EN: Expected owner-customer identifier.</param>
    /// <param name="cancellationToken">TR: SQL sorgu iptal sinyali. EN: SQL-query cancellation signal.</param>
    /// <returns>TR: Eşleşen BankAccount aggregate'ini; yoksa null döndürür. EN: Returns matching BankAccount aggregate, or null when not found.</returns>
    Task<BankAccount?> FindOwnedAsync(Guid bankAccountId, Guid customerId, CancellationToken cancellationToken);

    /// <summary>
    /// TR: Customer'ın belirtilen wallet'ına bağlı mevcut BankAccount kaydını arar.
    /// EN: Finds an existing BankAccount linked to the specified wallet of a customer.
    /// </summary>
    /// <param name="walletId">TR: Internal wallet kimliği. EN: Internal wallet identifier.</param>
    /// <param name="customerId">TR: Owner customer kimliği. EN: Owner-customer identifier.</param>
    /// <param name="cancellationToken">TR: SQL sorgu iptal sinyali. EN: SQL-query cancellation signal.</param>
    /// <returns>TR: Eşleşen BankAccount aggregate'ini; yoksa null döndürür. EN: Returns matching BankAccount aggregate, or null when absent.</returns>
    Task<BankAccount?> FindByWalletAsync(Guid walletId, Guid customerId, CancellationToken cancellationToken);

    /// <summary>
    /// TR: Yeni Opening BankAccount kaydını durable store'a ekler.
    /// EN: Inserts a new Opening BankAccount record into the durable store.
    /// </summary>
    /// <param name="bankAccount">TR: Eklenecek BankAccount aggregate'i. EN: BankAccount aggregate to insert.</param>
    /// <param name="cancellationToken">TR: SQL insert iptal sinyali. EN: SQL-insert cancellation signal.</param>
    Task InsertAsync(BankAccount bankAccount, CancellationToken cancellationToken);

    /// <summary>
    /// TR: BankAccount lifecycle/provider state'ini beklenen mevcut status koşuluyla compare-and-set biçiminde günceller.
    /// EN: Updates BankAccount lifecycle/provider state using compare-and-set semantics against the expected current status.
    /// </summary>
    /// <param name="bankAccount">TR: Yeni state'i taşıyan BankAccount aggregate'i. EN: BankAccount aggregate carrying new state.</param>
    /// <param name="expectedStatus">TR: UPDATE öncesinde DB'de beklenen lifecycle durumu. EN: Lifecycle state expected in the database before UPDATE.</param>
    /// <param name="cancellationToken">TR: SQL update iptal sinyali. EN: SQL-update cancellation signal.</param>
    /// <returns>TR: CAS update tek satır değiştirdiyse true; concurrent state değişmişse false döndürür. EN: Returns true when CAS update changed exactly one row; false when state changed concurrently.</returns>
    Task<bool> TryUpdateAsync(BankAccount bankAccount, BankAccountStatus expectedStatus, CancellationToken cancellationToken);
}
