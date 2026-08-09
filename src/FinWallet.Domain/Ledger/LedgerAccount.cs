namespace FinWallet.Domain.Ledger;

/// <summary>
/// TR: Double-entry ledger içerisinde currency bazlı muhasebe hesabını temsil eder; customer wallet liability, bank settlement, merchant payable, revenue veya expense gibi ekonomik hesapları birbirinden ayırır.
/// EN: Represents a currency-specific accounting account in the double-entry ledger and separates economic accounts such as customer-wallet liability, bank settlement, merchant payable, revenue or expense.
/// </summary>
public sealed class LedgerAccount
{
    /// <summary>
    /// TR: Yeni aktif ledger hesabı oluşturur.
    /// EN: Creates a new active ledger account.
    /// </summary>
    /// <param name="id">TR: Ledger hesabının benzersiz kimliği. EN: Unique ledger-account identifier.</param>
    /// <param name="code">TR: İnsan ve sistem tarafından izlenebilir benzersiz hesap kodu. EN: Human/system traceable unique account code.</param>
    /// <param name="currency">TR: Hesabın kabul ettiği üç harfli para birimi kodu. EN: Three-letter currency code accepted by the account.</param>
    /// <param name="type">TR: Muhasebe hesap sınıfı. EN: Accounting account class.</param>
    public LedgerAccount(Guid id, string code, string currency, LedgerAccountType type)
    {
        if (id == Guid.Empty) throw new ArgumentException("Ledger-account identifier cannot be empty.", nameof(id));
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);

        var normalizedCurrency = currency.Trim().ToUpperInvariant();
        if (normalizedCurrency.Length != 3) throw new ArgumentException("Ledger currency must contain exactly three characters.", nameof(currency));

        Id = id;
        Code = code.Trim().ToUpperInvariant();
        Currency = normalizedCurrency;
        Type = type;
        Status = LedgerAccountStatus.Active;
    }

    /// <summary>TR: Ledger hesabının benzersiz kimliğini döndürür. EN: Gets unique ledger-account identifier.</summary>
    public Guid Id { get; }

    /// <summary>TR: Ledger hesap kodunu döndürür. EN: Gets ledger-account code.</summary>
    public string Code { get; }

    /// <summary>TR: Hesabın currency kodunu döndürür. EN: Gets account currency code.</summary>
    public string Currency { get; }

    /// <summary>TR: Hesabın muhasebe sınıfını döndürür. EN: Gets accounting account class.</summary>
    public LedgerAccountType Type { get; }

    /// <summary>TR: Ledger hesabının mevcut lifecycle durumunu döndürür. EN: Gets current ledger-account lifecycle state.</summary>
    public LedgerAccountStatus Status { get; private set; }

    /// <summary>
    /// TR: Ledger hesabını yeni journal entry kabul etmeyecek şekilde kapatır; geçmiş ledger kayıtlarını değiştirmez.
    /// EN: Closes the ledger account to new journal entries without changing historical ledger records.
    /// </summary>
    public void Close()
    {
        Status = LedgerAccountStatus.Closed;
    }
}
