using FinWallet.Domain.Ledger;
using FinWallet.Domain.Shared;

namespace FinWallet.Application.Ledger;

/// <summary>
/// TR: Ledger hesaplarının durable get-or-create davranışını MSSQL implementasyonundan ayırır; journal posting sorumluluğunu bu store'a vermez.
/// EN: Decouples durable ledger-account get-or-create behavior from MSSQL implementation and deliberately does not give this store journal-posting responsibility.
/// </summary>
public interface ILedgerAccountStore
{
    /// <summary>
    /// TR: Stabil account code için mevcut ledger hesabını döndürür veya yoksa concurrency-safe biçimde oluşturur; mevcut account currency/type farklıysa hata verir.
    /// EN: Returns the existing ledger account for a stable account code or creates it concurrency-safely; fails when an existing account has a different currency/type.
    /// </summary>
    /// <param name="code">TR: Benzersiz ledger account kodu. EN: Unique ledger-account code.</param>
    /// <param name="currency">TR: Ledger account currency değeri. EN: Ledger-account currency.</param>
    /// <param name="type">TR: Muhasebe hesap sınıfı. EN: Accounting account class.</param>
    /// <param name="cancellationToken">TR: SQL işlemlerine yayılan iptal sinyali. EN: Cancellation signal propagated to SQL operations.</param>
    /// <returns>TR: Durable LedgerAccount nesnesini döndürür. EN: Returns the durable LedgerAccount.</returns>
    Task<LedgerAccount> GetOrCreateAsync(string code, CurrencyCode currency, LedgerAccountType type, CancellationToken cancellationToken);
}
