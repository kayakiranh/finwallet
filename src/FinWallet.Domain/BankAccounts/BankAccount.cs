using FinWallet.Domain.Shared;

namespace FinWallet.Domain.BankAccounts;

/// <summary>
/// TR: FinWallet cüzdanını aynı currency'deki dış banka hesabına bağlayan, internal ve provider kimliklerini ayrı tutan banka hesabı aggregate'ini temsil eder.
/// EN: Represents the bank-account aggregate linking a FinWallet wallet to an external bank account in the same currency while keeping internal and provider identifiers separate.
/// </summary>
public sealed class BankAccount
{
    /// <summary>
    /// TR: Kalıcılık katmanının banka hesabı aggregate'ini yeniden oluşturması için ayrılmış kurucudur.
    /// EN: Constructor reserved for persistence materialization of the bank-account aggregate.
    /// </summary>
    private BankAccount()
    {
        ExternalIban = null;
    }

    /// <summary>
    /// TR: Dış banka hesap açılışı başlatılacak yeni internal BankAccount kaydını Opening durumunda oluşturur.
    /// EN: Creates a new internal BankAccount record in Opening state for an external-bank account-opening flow.
    /// </summary>
    /// <param name="id">TR: FinWallet internal banka hesabı kimliği. EN: FinWallet internal bank-account identifier.</param>
    /// <param name="customerId">TR: Banka hesabının sahibi customer kimliği. EN: Customer identifier owning the bank account.</param>
    /// <param name="walletId">TR: Aynı currency'de banka hesabıyla eşleştirilen internal wallet kimliği. EN: Internal wallet identifier linked to the bank account in the same currency.</param>
    /// <param name="currency">TR: Banka hesabı ve bağlı wallet'ın ortak currency değeri. EN: Shared currency of the bank account and linked wallet.</param>
    /// <param name="createdAt">TR: Internal account-opening kaydının oluşturulduğu UTC zaman. EN: UTC time at which the internal account-opening record was created.</param>
    /// <returns>TR: Provider hesabı henüz bağlanmamış Opening durumundaki aggregate'i döndürür. EN: Returns the Opening aggregate before a provider account has been linked.</returns>
    public static BankAccount Create(
        Guid id,
        Guid customerId,
        Guid walletId,
        CurrencyCode currency,
        DateTimeOffset createdAt)
    {
        ValidateIdentifiers(id, customerId, walletId);

        return new BankAccount
        {
            Id = id,
            CustomerId = customerId,
            WalletId = walletId,
            Currency = currency,
            Status = BankAccountStatus.Opening,
            CreatedAt = createdAt,
            UpdatedAt = createdAt
        };
    }

    /// <summary>
    /// TR: MSSQL kaydından banka hesabı aggregate'ini lifecycle invariant'larını doğrulayarak yeniden oluşturur.
    /// EN: Rehydrates the bank-account aggregate from an MSSQL record while validating lifecycle invariants.
    /// </summary>
    /// <param name="id">TR: Kalıcı internal banka hesabı kimliği. EN: Persisted internal bank-account identifier.</param>
    /// <param name="customerId">TR: Kalıcı customer kimliği. EN: Persisted customer identifier.</param>
    /// <param name="walletId">TR: Kalıcı wallet kimliği. EN: Persisted wallet identifier.</param>
    /// <param name="currency">TR: Kalıcı currency değeri. EN: Persisted currency value.</param>
    /// <param name="externalAccountId">TR: Dış provider hesap kimliği; provider hesabı henüz yoksa null. EN: External-provider account identifier, or null when not yet created.</param>
    /// <param name="externalIban">TR: Dış provider IBAN-benzeri değeri; provider hesabı henüz yoksa null. EN: External-provider IBAN-like value, or null when not yet created.</param>
    /// <param name="status">TR: Kalıcı banka hesabı lifecycle durumu. EN: Persisted bank-account lifecycle state.</param>
    /// <param name="createdAt">TR: Kalıcı oluşturulma UTC zamanı. EN: Persisted UTC creation time.</param>
    /// <param name="updatedAt">TR: Kalıcı son güncellenme UTC zamanı. EN: Persisted UTC last-update time.</param>
    /// <returns>TR: Kalıcı state'i taşıyan banka hesabı aggregate'ini döndürür. EN: Returns a bank-account aggregate carrying persisted state.</returns>
    public static BankAccount Restore(
        Guid id,
        Guid customerId,
        Guid walletId,
        CurrencyCode currency,
        Guid? externalAccountId,
        string? externalIban,
        BankAccountStatus status,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        ValidateIdentifiers(id, customerId, walletId);
        if (updatedAt < createdAt) throw new ArgumentException("Bank-account update time cannot precede creation time.", nameof(updatedAt));
        if (externalAccountId.HasValue != !string.IsNullOrWhiteSpace(externalIban)) throw new ArgumentException("External account identifier and IBAN must be present together.");
        if (status is BankAccountStatus.Active or BankAccountStatus.Blocked or BankAccountStatus.Closed && externalAccountId is null)
        {
            throw new ArgumentException("The persisted bank-account state requires an external account link.", nameof(externalAccountId));
        }

        return new BankAccount
        {
            Id = id,
            CustomerId = customerId,
            WalletId = walletId,
            Currency = currency,
            ExternalAccountId = externalAccountId,
            ExternalIban = string.IsNullOrWhiteSpace(externalIban) ? null : externalIban.Trim(),
            Status = status,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };
    }

    /// <summary>TR: FinWallet internal banka hesabı kimliğini döndürür. EN: Gets the FinWallet internal bank-account identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>TR: Banka hesabının sahibi customer kimliğini döndürür. EN: Gets the customer identifier owning the bank account.</summary>
    public Guid CustomerId { get; private set; }

    /// <summary>TR: Banka hesabıyla eşleştirilen internal wallet kimliğini döndürür. EN: Gets the internal wallet identifier linked to the bank account.</summary>
    public Guid WalletId { get; private set; }

    /// <summary>TR: Banka hesabının currency değerini döndürür. EN: Gets the bank-account currency.</summary>
    public CurrencyCode Currency { get; private set; }

    /// <summary>TR: Provider tarafından üretilen ve internal kimlikten ayrı tutulan harici hesap kimliğini döndürür. EN: Gets the provider-generated external account identifier kept separate from the internal identifier.</summary>
    public Guid? ExternalAccountId { get; private set; }

    /// <summary>TR: Provider tarafından üretilen IBAN-benzeri hesap değerini döndürür; loglarda maskelenmelidir. EN: Gets the provider-generated IBAN-like account value; it must be masked in logs.</summary>
    public string? ExternalIban { get; private set; }

    /// <summary>TR: Banka hesabı bağlantısının mevcut lifecycle durumunu döndürür. EN: Gets the current lifecycle state of the bank-account connection.</summary>
    public BankAccountStatus Status { get; private set; }

    /// <summary>TR: Internal banka hesabı kaydının oluşturulduğu UTC zamanı döndürür. EN: Gets the UTC time at which the internal bank-account record was created.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>TR: Banka hesabı lifecycle state'inin son değiştiği UTC zamanı döndürür. EN: Gets the UTC time at which the bank-account lifecycle state last changed.</summary>
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>
    /// TR: Dış bankanın oluşturduğu account kimliği ve IBAN-benzeri değeri Opening aggregate'e bağlar; internal kimliği değiştirmez.
    /// EN: Links the external-bank account identifier and IBAN-like value to the Opening aggregate without changing the internal identifier.
    /// </summary>
    /// <param name="externalAccountId">TR: Provider tarafından üretilen external account kimliği. EN: External-account identifier generated by the provider.</param>
    /// <param name="externalIban">TR: Provider tarafından üretilen IBAN-benzeri hesap değeri. EN: IBAN-like account value generated by the provider.</param>
    /// <param name="linkedAt">TR: Provider hesap linkinin kaydedildiği UTC zaman. EN: UTC time at which the provider account link was recorded.</param>
    public void LinkExternalAccount(Guid externalAccountId, string externalIban, DateTimeOffset linkedAt)
    {
        if (Status != BankAccountStatus.Opening) throw new InvalidOperationException("External account can only be linked while bank-account opening is in progress.");
        if (ExternalAccountId is not null) throw new InvalidOperationException("External account is already linked.");
        if (externalAccountId == Guid.Empty) throw new ArgumentException("External account identifier cannot be empty.", nameof(externalAccountId));
        ArgumentException.ThrowIfNullOrWhiteSpace(externalIban);
        EnsureMonotonicTime(linkedAt);

        ExternalAccountId = externalAccountId;
        ExternalIban = externalIban.Trim();
        UpdatedAt = linkedAt;
    }

    /// <summary>
    /// TR: Provider hesabı bağlanmış Opening kaydını Active duruma geçirir.
    /// EN: Transitions an Opening record with a linked provider account into Active state.
    /// </summary>
    /// <param name="activatedAt">TR: Hesabın aktifleştiği UTC zaman. EN: UTC time at which the account became active.</param>
    public void Activate(DateTimeOffset activatedAt)
    {
        if (Status != BankAccountStatus.Opening) throw new InvalidOperationException("Only an Opening bank account can be activated.");
        EnsureExternalAccountLinked();
        EnsureMonotonicTime(activatedAt);
        Status = BankAccountStatus.Active;
        UpdatedAt = activatedAt;
    }

    /// <summary>
    /// TR: Dış bankanın hesap açılışını reddetmesi durumunda Opening kaydını Rejected state'e geçirir.
    /// EN: Transitions an Opening record into Rejected state when the external bank rejects account opening.
    /// </summary>
    /// <param name="rejectedAt">TR: Provider reddinin işlendiği UTC zaman. EN: UTC time at which provider rejection was recorded.</param>
    public void Reject(DateTimeOffset rejectedAt)
    {
        if (Status != BankAccountStatus.Opening) throw new InvalidOperationException("Only an Opening bank account can be rejected.");
        EnsureMonotonicTime(rejectedAt);
        Status = BankAccountStatus.Rejected;
        UpdatedAt = rejectedAt;
    }

    /// <summary>
    /// TR: Aktif banka hesabını yeni finansal hareketlere kapatmak için Blocked state'e geçirir.
    /// EN: Transitions an Active bank account into Blocked state to stop new financial movements.
    /// </summary>
    /// <param name="blockedAt">TR: Blokajın başladığı UTC zaman. EN: UTC time at which blocking began.</param>
    public void Block(DateTimeOffset blockedAt)
    {
        if (Status != BankAccountStatus.Active) throw new InvalidOperationException("Only an Active bank account can be blocked.");
        EnsureMonotonicTime(blockedAt);
        Status = BankAccountStatus.Blocked;
        UpdatedAt = blockedAt;
    }

    /// <summary>
    /// TR: Bloke banka hesabını tekrar Active state'e getirir.
    /// EN: Restores a Blocked bank account to Active state.
    /// </summary>
    /// <param name="unblockedAt">TR: Blokajın kaldırıldığı UTC zaman. EN: UTC time at which blocking ended.</param>
    public void Unblock(DateTimeOffset unblockedAt)
    {
        if (Status != BankAccountStatus.Blocked) throw new InvalidOperationException("Only a Blocked bank account can be unblocked.");
        EnsureMonotonicTime(unblockedAt);
        Status = BankAccountStatus.Active;
        UpdatedAt = unblockedAt;
    }

    /// <summary>
    /// TR: Active veya Blocked banka hesabı bağlantısını kalıcı Closed state'e geçirir.
    /// EN: Permanently transitions an Active or Blocked bank-account connection into Closed state.
    /// </summary>
    /// <param name="closedAt">TR: Hesabın kapatıldığı UTC zaman. EN: UTC time at which the account was closed.</param>
    public void Close(DateTimeOffset closedAt)
    {
        if (Status is not (BankAccountStatus.Active or BankAccountStatus.Blocked)) throw new InvalidOperationException("Only an Active or Blocked bank account can be closed.");
        EnsureMonotonicTime(closedAt);
        Status = BankAccountStatus.Closed;
        UpdatedAt = closedAt;
    }

    /// <summary>TR: Create/Restore sırasında internal aggregate kimliklerinin boş olmamasını doğrular. EN: Validates that internal aggregate identifiers are not empty during Create/Restore.</summary>
    /// <param name="id">TR: Internal banka hesabı kimliği. EN: Internal bank-account identifier.</param>
    /// <param name="customerId">TR: Customer kimliği. EN: Customer identifier.</param>
    /// <param name="walletId">TR: Wallet kimliği. EN: Wallet identifier.</param>
    private static void ValidateIdentifiers(Guid id, Guid customerId, Guid walletId)
    {
        if (id == Guid.Empty) throw new ArgumentException("Bank-account identifier cannot be empty.", nameof(id));
        if (customerId == Guid.Empty) throw new ArgumentException("Customer identifier cannot be empty.", nameof(customerId));
        if (walletId == Guid.Empty) throw new ArgumentException("Wallet identifier cannot be empty.", nameof(walletId));
    }

    /// <summary>TR: Provider hesabı gerektiren state geçişlerinden önce external account kimliği ve IBAN değerinin bağlı olduğunu doğrular. EN: Validates that external account identifier and IBAN are linked before state transitions that require a provider account.</summary>
    private void EnsureExternalAccountLinked()
    {
        if (ExternalAccountId is null || string.IsNullOrWhiteSpace(ExternalIban)) throw new InvalidOperationException("External bank account is not linked.");
    }

    /// <summary>TR: Lifecycle zamanlarının geriye gitmesini engeller. EN: Prevents lifecycle timestamps from moving backward.</summary>
    /// <param name="time">TR: Uygulanacak yeni lifecycle UTC zamanı. EN: New lifecycle UTC time to apply.</param>
    private void EnsureMonotonicTime(DateTimeOffset time)
    {
        if (time < UpdatedAt) throw new ArgumentException("Bank-account lifecycle time cannot move backward.", nameof(time));
    }
}
