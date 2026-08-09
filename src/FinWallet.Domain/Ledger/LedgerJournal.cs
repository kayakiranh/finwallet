namespace FinWallet.Domain.Ledger;

/// <summary>
/// TR: Tek currency içinde debit/credit toplamı eşit olmak zorunda olan append-only double-entry journal aggregate'ini temsil eder; post edildikten sonra değiştirilemez ve düzeltmeler yeni reversal journal ile yapılır.
/// EN: Represents an append-only double-entry journal aggregate whose debit/credit totals must balance within one currency; it becomes immutable after posting and corrections are made through a new reversal journal.
/// </summary>
public sealed class LedgerJournal
{
    private readonly List<LedgerEntry> _entries = new();

    /// <summary>
    /// TR: Yeni Draft journal oluşturur.
    /// EN: Creates a new Draft journal.
    /// </summary>
    /// <param name="id">TR: Journal benzersiz kimliği. EN: Unique journal identifier.</param>
    /// <param name="transactionReference">TR: Journal'ı oluşturan FinWallet finansal transaction referansı. EN: FinWallet financial-transaction reference that created the journal.</param>
    /// <param name="currency">TR: Journal içindeki tüm entry'lerin ortak currency kodu. EN: Common currency code shared by all entries in the journal.</param>
    /// <param name="createdAt">TR: Journal oluşturulma UTC zamanı. EN: UTC journal creation time.</param>
    /// <param name="reversesJournalId">TR: Bu journal bir reversal ise ters çevirdiği original journal kimliği; normal journal'da null. EN: Original journal identifier reversed by this journal, or null for a normal journal.</param>
    public LedgerJournal(Guid id, Guid transactionReference, string currency, DateTimeOffset createdAt, Guid? reversesJournalId = null)
    {
        if (id == Guid.Empty) throw new ArgumentException("Ledger-journal identifier cannot be empty.", nameof(id));
        if (transactionReference == Guid.Empty) throw new ArgumentException("Transaction reference cannot be empty.", nameof(transactionReference));
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);

        var normalizedCurrency = currency.Trim().ToUpperInvariant();
        if (normalizedCurrency.Length != 3) throw new ArgumentException("Ledger currency must contain exactly three characters.", nameof(currency));
        if (reversesJournalId.HasValue && reversesJournalId.Value == Guid.Empty) throw new ArgumentException("Reversed journal identifier cannot be empty.", nameof(reversesJournalId));

        Id = id;
        TransactionReference = transactionReference;
        Currency = normalizedCurrency;
        CreatedAt = createdAt;
        ReversesJournalId = reversesJournalId;
        Status = LedgerJournalStatus.Draft;
    }

    /// <summary>TR: Journal benzersiz kimliğini döndürür. EN: Gets unique journal identifier.</summary>
    public Guid Id { get; }

    /// <summary>TR: Journal'ın bağlı olduğu finansal transaction referansını döndürür. EN: Gets financial-transaction reference associated with journal.</summary>
    public Guid TransactionReference { get; }

    /// <summary>TR: Journal'ın tek currency kodunu döndürür. EN: Gets single currency code of journal.</summary>
    public string Currency { get; }

    /// <summary>TR: Journal oluşturulma UTC zamanını döndürür. EN: Gets journal UTC creation time.</summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>TR: Journal'ın post edildiği UTC zamanı; Draft ise null döndürür. EN: Gets UTC posting time, or null while Draft.</summary>
    public DateTimeOffset? PostedAt { get; private set; }

    /// <summary>TR: Mevcut journal lifecycle durumunu döndürür. EN: Gets current journal lifecycle state.</summary>
    public LedgerJournalStatus Status { get; private set; }

    /// <summary>TR: Bu journal reversal ise original journal kimliğini; değilse null döndürür. EN: Gets original journal identifier when this journal is a reversal, otherwise null.</summary>
    public Guid? ReversesJournalId { get; }

    /// <summary>TR: Journal entry'lerini dışarıya yalnızca read-only koleksiyon olarak döndürür. EN: Exposes journal entries only as a read-only collection.</summary>
    public IReadOnlyCollection<LedgerEntry> Entries => _entries.AsReadOnly();

    /// <summary>
    /// TR: Aktif ledger hesabına journal currency'siyle eşleşen pozitif Debit entry ekler; Posted journal'a entry eklenemez.
    /// EN: Adds a positive Debit entry for an active ledger account whose currency matches the journal; entries cannot be added to a Posted journal.
    /// </summary>
    /// <param name="account">TR: Debit uygulanacak aktif ledger hesabı. EN: Active ledger account receiving the debit entry.</param>
    /// <param name="amount">TR: Pozitif debit tutarı. EN: Positive debit amount.</param>
    public void AddDebit(LedgerAccount account, decimal amount) => AddEntry(account, LedgerEntrySide.Debit, amount);

    /// <summary>
    /// TR: Aktif ledger hesabına journal currency'siyle eşleşen pozitif Credit entry ekler; Posted journal'a entry eklenemez.
    /// EN: Adds a positive Credit entry for an active ledger account whose currency matches the journal; entries cannot be added to a Posted journal.
    /// </summary>
    /// <param name="account">TR: Credit uygulanacak aktif ledger hesabı. EN: Active ledger account receiving the credit entry.</param>
    /// <param name="amount">TR: Pozitif credit tutarı. EN: Positive credit amount.</param>
    public void AddCredit(LedgerAccount account, decimal amount) => AddEntry(account, LedgerEntrySide.Credit, amount);

    /// <summary>
    /// TR: Journal'ın en az bir debit ve bir credit içerdiğini ve toplam debit=credit invariant'ını doğrulayıp finansal geçmişe immutable Posted state olarak kesinleştirir.
    /// EN: Validates that the journal contains at least one debit and one credit and that total debit equals total credit, then finalizes it into immutable Posted financial history.
    /// </summary>
    /// <param name="postedAt">TR: Journal'ın finansal geçmişe kesinleştiği UTC zaman. EN: UTC time at which journal is finalized into financial history.</param>
    /// <exception cref="UnbalancedLedgerJournalException">TR: Debit ve credit toplamları eşit değilse oluşur. EN: Thrown when debit and credit totals are unequal.</exception>
    public void Post(DateTimeOffset postedAt)
    {
        EnsureDraft();
        if (postedAt < CreatedAt) throw new ArgumentException("Posting time cannot precede creation.", nameof(postedAt));

        var totalDebit = _entries.Where(static entry => entry.Side == LedgerEntrySide.Debit).Sum(static entry => entry.Amount);
        var totalCredit = _entries.Where(static entry => entry.Side == LedgerEntrySide.Credit).Sum(static entry => entry.Amount);

        if (totalDebit <= 0 || totalCredit <= 0 || totalDebit != totalCredit)
        {
            throw new UnbalancedLedgerJournalException(totalDebit, totalCredit);
        }

        Status = LedgerJournalStatus.Posted;
        PostedAt = postedAt;
    }

    /// <summary>
    /// TR: Posted original journal'ın her debit satırını credit, her credit satırını debit yaparak yeni ve dengeli bir reversal journal oluşturup post eder; original journal'a dokunmaz.
    /// EN: Creates and posts a new balanced reversal journal by turning every debit of the Posted original journal into credit and every credit into debit, leaving the original journal unchanged.
    /// </summary>
    /// <param name="reversalJournalId">TR: Yeni reversal journal kimliği. EN: New reversal-journal identifier.</param>
    /// <param name="reversalTransactionReference">TR: Reversal işleminin finansal transaction referansı. EN: Financial transaction reference of the reversal operation.</param>
    /// <param name="accounts">TR: Original entry account kimliklerini çözen ledger hesap koleksiyonu. EN: Ledger-account collection resolving original entry account identifiers.</param>
    /// <param name="createdAt">TR: Reversal journal oluşturulma/post UTC zamanı. EN: UTC creation/posting time of reversal journal.</param>
    /// <returns>TR: Original journal'ı referanslayan yeni Posted reversal journal döndürür. EN: Returns a new Posted reversal journal referencing the original journal.</returns>
    public LedgerJournal CreateReversal(Guid reversalJournalId, Guid reversalTransactionReference, IReadOnlyDictionary<Guid, LedgerAccount> accounts, DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(accounts);
        if (Status != LedgerJournalStatus.Posted) throw new InvalidOperationException("Only a Posted journal can be reversed.");

        var reversal = new LedgerJournal(reversalJournalId, reversalTransactionReference, Currency, createdAt, Id);
        foreach (var entry in _entries)
        {
            if (!accounts.TryGetValue(entry.AccountId, out var account)) throw new InvalidOperationException($"Ledger account '{entry.AccountId}' required for reversal was not supplied.");

            if (entry.Side == LedgerEntrySide.Debit)
            {
                reversal.AddCredit(account, entry.Amount);
            }
            else
            {
                reversal.AddDebit(account, entry.Amount);
            }
        }

        reversal.Post(createdAt);
        return reversal;
    }

    /// <summary>
    /// TR: Ortak entry ekleme invariant'larını uygular: journal Draft, hesap Active, currency eşit ve amount pozitif olmalıdır.
    /// EN: Applies shared entry-addition invariants: journal must be Draft, account Active, currency equal and amount positive.
    /// </summary>
    /// <param name="account">TR: Entry'nin bağlanacağı ledger hesap nesnesi. EN: Ledger-account object associated with the entry.</param>
    /// <param name="side">TR: Debit veya Credit entry tarafı. EN: Debit or Credit entry side.</param>
    /// <param name="amount">TR: Pozitif finansal tutar. EN: Positive financial amount.</param>
    private void AddEntry(LedgerAccount account, LedgerEntrySide side, decimal amount)
    {
        ArgumentNullException.ThrowIfNull(account);
        EnsureDraft();
        if (account.Status != LedgerAccountStatus.Active) throw new InvalidOperationException("Closed ledger account cannot receive new entries.");
        if (!string.Equals(account.Currency, Currency, StringComparison.Ordinal)) throw new InvalidOperationException("Ledger account currency must match journal currency.");
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
        _entries.Add(new LedgerEntry(Guid.NewGuid(), account.Id, side, amount, Currency));
    }

    /// <summary>TR: Journal'ın halen Draft olduğunu doğrular ve Posted finansal geçmişin mutasyona uğramasını engeller. EN: Ensures journal is still Draft and prevents mutation of Posted financial history.</summary>
    private void EnsureDraft()
    {
        if (Status != LedgerJournalStatus.Draft) throw new InvalidOperationException("Posted ledger journal is immutable.");
    }
}
