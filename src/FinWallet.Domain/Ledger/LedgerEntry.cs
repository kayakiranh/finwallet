namespace FinWallet.Domain.Ledger;

/// <summary>
/// TR: Bir double-entry journal içindeki tek debit/credit satırını temsil eder; oluşturulduktan sonra değiştirilemez ve her zaman pozitif tutar ile tek currency taşır.
/// EN: Represents one debit/credit line inside a double-entry journal; it is immutable after creation and always carries a positive amount with one currency.
/// </summary>
public sealed class LedgerEntry
{
    /// <summary>
    /// TR: Immutable ledger entry oluşturur.
    /// EN: Creates an immutable ledger entry.
    /// </summary>
    /// <param name="id">TR: Entry benzersiz kimliği. EN: Unique entry identifier.</param>
    /// <param name="accountId">TR: Entry'nin bağlı olduğu ledger hesap kimliği. EN: Ledger-account identifier associated with the entry.</param>
    /// <param name="side">TR: Debit veya Credit tarafı. EN: Debit or Credit side.</param>
    /// <param name="amount">TR: Sıfırdan büyük finansal tutar. EN: Financial amount greater than zero.</param>
    /// <param name="currency">TR: Üç harfli para birimi kodu. EN: Three-letter currency code.</param>
    public LedgerEntry(Guid id, Guid accountId, LedgerEntrySide side, decimal amount, string currency)
    {
        if (id == Guid.Empty) throw new ArgumentException("Ledger-entry identifier cannot be empty.", nameof(id));
        if (accountId == Guid.Empty) throw new ArgumentException("Ledger-account identifier cannot be empty.", nameof(accountId));
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);

        var normalizedCurrency = currency.Trim().ToUpperInvariant();
        if (normalizedCurrency.Length != 3) throw new ArgumentException("Ledger currency must contain exactly three characters.", nameof(currency));

        Id = id;
        AccountId = accountId;
        Side = side;
        Amount = amount;
        Currency = normalizedCurrency;
    }

    /// <summary>TR: Entry benzersiz kimliğini döndürür. EN: Gets unique entry identifier.</summary>
    public Guid Id { get; }

    /// <summary>TR: Entry'nin bağlı olduğu ledger hesap kimliğini döndürür. EN: Gets ledger-account identifier associated with the entry.</summary>
    public Guid AccountId { get; }

    /// <summary>TR: Entry'nin Debit/Credit tarafını döndürür. EN: Gets Debit/Credit side of the entry.</summary>
    public LedgerEntrySide Side { get; }

    /// <summary>TR: Pozitif entry tutarını döndürür. EN: Gets positive entry amount.</summary>
    public decimal Amount { get; }

    /// <summary>TR: Entry currency kodunu döndürür. EN: Gets entry currency code.</summary>
    public string Currency { get; }
}
