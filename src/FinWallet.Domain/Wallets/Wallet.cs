using FinWallet.Domain.Shared;

namespace FinWallet.Domain.Wallets;

/// <summary>
/// TR: Bir müşterinin tek bir para birimindeki kullanılabilir ve bloke bakiyesini yöneten finansal cüzdan aggregate'ini temsil eder.
/// EN: Represents the financial wallet aggregate that manages a customer's available and blocked balances for one currency.
/// </summary>
public sealed class Wallet
{
    /// <summary>
    /// TR: Kalıcılık araçlarının cüzdanı yeniden oluşturabilmesi için ayrılmış kurucudur ve iş akışlarında doğrudan kullanılmamalıdır.
    /// EN: Constructor reserved for persistence materialization and not intended for direct use by business workflows.
    /// </summary>
    private Wallet()
    {
    }

    /// <summary>
    /// TR: Müşteri ve para birimi için sıfır bakiyeli aktif yeni bir cüzdan oluşturur.
    /// EN: Creates a new active wallet with zero balances for the specified customer and currency.
    /// </summary>
    /// <param name="id">TR: Cüzdanın sistem içindeki benzersiz kimliği. EN: Unique identifier of the wallet inside the system.</param>
    /// <param name="customerId">TR: Cüzdanın sahibi olan müşterinin benzersiz kimliği. EN: Unique identifier of the customer who owns the wallet.</param>
    /// <param name="currency">TR: Cüzdanın kabul ettiği para birimi. EN: Currency accepted by the wallet.</param>
    /// <param name="createdAt">TR: Cüzdanın oluşturulduğu UTC zaman bilgisi. EN: UTC timestamp at which the wallet was created.</param>
    /// <returns>TR: Sıfır bakiyeli aktif cüzdanı döndürür. EN: Returns an active wallet with zero balances.</returns>
    public static Wallet Create(Guid id, Guid customerId, CurrencyCode currency, DateTimeOffset createdAt)
    {
        ValidateIdentifiers(id, customerId);
        return new Wallet
        {
            Id = id,
            CustomerId = customerId,
            Currency = currency,
            AvailableBalance = 0m,
            BlockedBalance = 0m,
            Status = WalletStatus.Active,
            CreatedAt = createdAt
        };
    }

    /// <summary>
    /// TR: MSSQL kaydındaki cüzdan state'ini reflection kullanmadan kontrollü biçimde yeniden oluşturur.
    /// EN: Rehydrates persisted wallet state from MSSQL in a controlled way without using reflection.
    /// </summary>
    /// <param name="id">TR: Kalıcı wallet kimliği. EN: Persisted wallet identifier.</param>
    /// <param name="customerId">TR: Kalıcı customer kimliği. EN: Persisted customer identifier.</param>
    /// <param name="currency">TR: Kalıcı wallet currency değeri. EN: Persisted wallet currency.</param>
    /// <param name="availableBalance">TR: Kalıcı kullanılabilir bakiye. EN: Persisted available balance.</param>
    /// <param name="blockedBalance">TR: Kalıcı bloke bakiye. EN: Persisted blocked balance.</param>
    /// <param name="status">TR: Kalıcı wallet lifecycle durumu. EN: Persisted wallet lifecycle state.</param>
    /// <param name="createdAt">TR: Kalıcı oluşturulma UTC zamanı. EN: Persisted UTC creation time.</param>
    /// <returns>TR: Kalıcı state'i taşıyan wallet aggregate'ini döndürür. EN: Returns a wallet aggregate carrying persisted state.</returns>
    public static Wallet Restore(
        Guid id,
        Guid customerId,
        CurrencyCode currency,
        decimal availableBalance,
        decimal blockedBalance,
        WalletStatus status,
        DateTimeOffset createdAt)
    {
        ValidateIdentifiers(id, customerId);
        if (availableBalance < 0m) throw new ArgumentOutOfRangeException(nameof(availableBalance));
        if (blockedBalance < 0m) throw new ArgumentOutOfRangeException(nameof(blockedBalance));
        if (!Enum.IsDefined(status)) throw new ArgumentOutOfRangeException(nameof(status));

        return new Wallet
        {
            Id = id,
            CustomerId = customerId,
            Currency = currency,
            AvailableBalance = availableBalance,
            BlockedBalance = blockedBalance,
            Status = status,
            CreatedAt = createdAt
        };
    }

    /// <summary>TR: Cüzdanın sistem içindeki benzersiz kimliğini döndürür. EN: Gets the wallet's unique identifier inside the system.</summary>
    public Guid Id { get; private set; }

    /// <summary>TR: Cüzdan sahibinin müşteri kimliğini döndürür. EN: Gets the identifier of the customer who owns the wallet.</summary>
    public Guid CustomerId { get; private set; }

    /// <summary>TR: Cüzdanın işlem yaptığı para birimini döndürür. EN: Gets the currency used by the wallet.</summary>
    public CurrencyCode Currency { get; private set; }

    /// <summary>TR: Yeni finansal işlemlerde kullanılabilecek bakiyeyi döndürür. EN: Gets the balance available for new financial operations.</summary>
    public decimal AvailableBalance { get; private set; }

    /// <summary>TR: Bekleyen dış finansal işlemler için ayrılmış bakiyeyi döndürür. EN: Gets the balance reserved for pending external financial operations.</summary>
    public decimal BlockedBalance { get; private set; }

    /// <summary>TR: Cüzdanın mevcut yaşam döngüsü durumunu döndürür. EN: Gets the current wallet lifecycle state.</summary>
    public WalletStatus Status { get; private set; }

    /// <summary>TR: Cüzdanın oluşturulduğu UTC zamanı döndürür. EN: Gets the UTC timestamp at which the wallet was created.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// TR: Gelen finansal tutarı kullanılabilir bakiyeye ekler; kapalı cüzdan para kabul edemez.
    /// EN: Adds an incoming monetary amount to the available balance; a closed wallet cannot receive funds.
    /// </summary>
    /// <param name="amount">TR: Cüzdana eklenecek pozitif ve aynı para birimindeki para değeri. EN: Positive monetary value in the wallet currency to add.</param>
    public void Credit(Money amount)
    {
        EnsureCanReceive();
        EnsurePositive(amount);
        EnsureCurrency(amount);
        AvailableBalance += amount.Amount;
    }

    /// <summary>
    /// TR: Kesinleşen dahili para çıkışını kullanılabilir bakiyeden düşer.
    /// EN: Debits a finalized internal outgoing amount from available balance.
    /// </summary>
    /// <param name="amount">TR: Cüzdandan düşülecek pozitif tutar. EN: Positive amount to debit from the wallet.</param>
    public void Debit(Money amount)
    {
        EnsureActive();
        EnsurePositive(amount);
        EnsureCurrency(amount);
        if (AvailableBalance < amount.Amount)
        {
            throw new InsufficientBalanceException(new Money(AvailableBalance, Currency), amount);
        }
        AvailableBalance -= amount.Amount;
    }

    /// <summary>
    /// TR: Bekleyen dış işlem için tutarı kullanılabilir bakiyeden bloke bakiyeye taşır.
    /// EN: Moves an amount from available balance to blocked balance for a pending external operation.
    /// </summary>
    /// <param name="amount">TR: Bloke edilecek pozitif tutar. EN: Positive amount to block.</param>
    public void BlockFunds(Money amount)
    {
        EnsureActive();
        EnsurePositive(amount);
        EnsureCurrency(amount);
        if (AvailableBalance < amount.Amount)
        {
            throw new InsufficientBalanceException(new Money(AvailableBalance, Currency), amount);
        }
        AvailableBalance -= amount.Amount;
        BlockedBalance += amount.Amount;
    }

    /// <summary>
    /// TR: Başarısız veya iptal edilen bekleyen işlem sonrasında bloke tutarı tekrar kullanılabilir bakiyeye taşır.
    /// EN: Moves blocked funds back to available balance after a pending operation fails or is cancelled.
    /// </summary>
    /// <param name="amount">TR: Serbest bırakılacak pozitif tutar. EN: Positive amount to release.</param>
    public void ReleaseBlockedFunds(Money amount)
    {
        EnsurePositive(amount);
        EnsureCurrency(amount);
        if (BlockedBalance < amount.Amount)
        {
            throw new InsufficientBalanceException(new Money(BlockedBalance, Currency), amount);
        }
        BlockedBalance -= amount.Amount;
        AvailableBalance += amount.Amount;
    }

    /// <summary>
    /// TR: Dış finansal işlem başarıyla kesinleştiğinde ilgili tutarı bloke bakiyeden kalıcı olarak düşer.
    /// EN: Permanently removes the related amount from blocked balance when an external operation settles successfully.
    /// </summary>
    /// <param name="amount">TR: Kesinleştirilecek pozitif tutar. EN: Positive amount to settle.</param>
    public void SettleBlockedFunds(Money amount)
    {
        EnsurePositive(amount);
        EnsureCurrency(amount);
        if (BlockedBalance < amount.Amount)
        {
            throw new InsufficientBalanceException(new Money(BlockedBalance, Currency), amount);
        }
        BlockedBalance -= amount.Amount;
    }

    /// <summary>TR: Aktif cüzdandan yeni para çıkışı başlatılmasını engellemek için cüzdanı bloke eder. EN: Blocks an active wallet to prevent initiation of new outgoing money movements.</summary>
    public void Block()
    {
        EnsureActive();
        Status = WalletStatus.Blocked;
    }

    /// <summary>TR: Cüzdanın aktif olduğunu doğrular. EN: Validates that the wallet is active.</summary>
    private void EnsureActive()
    {
        if (Status != WalletStatus.Active)
        {
            throw new InvalidOperationException($"Wallet in '{Status}' state cannot initiate this operation.");
        }
    }

    /// <summary>TR: Cüzdanın gelen para hareketini kabul edebileceğini doğrular. EN: Validates that the wallet may receive incoming funds.</summary>
    private void EnsureCanReceive()
    {
        if (Status == WalletStatus.Closed)
        {
            throw new InvalidOperationException("Closed wallet cannot receive funds.");
        }
    }

    /// <summary>TR: Finansal hareket tutarının sıfırdan büyük olduğunu doğrular. EN: Validates that the monetary amount is greater than zero.</summary>
    /// <param name="amount">TR: Pozitifliği doğrulanacak para değeri. EN: Monetary value whose positivity will be validated.</param>
    private static void EnsurePositive(Money amount)
    {
        if (!amount.IsPositive)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Financial movement amount must be greater than zero.");
        }
    }

    /// <summary>TR: Finansal hareket currency'sinin wallet currency'siyle aynı olduğunu doğrular. EN: Validates that the financial-movement currency matches the wallet currency.</summary>
    /// <param name="amount">TR: Currency değeri doğrulanacak para değeri. EN: Monetary value whose currency will be validated.</param>
    private void EnsureCurrency(Money amount)
    {
        if (amount.Currency != Currency)
        {
            throw new CurrencyMismatchException(Currency, amount.Currency);
        }
    }

    /// <summary>TR: Wallet ve customer kimliklerinin boş olmadığını doğrular. EN: Validates that wallet and customer identifiers are not empty.</summary>
    /// <param name="id">TR: Wallet kimliği. EN: Wallet identifier.</param>
    /// <param name="customerId">TR: Customer kimliği. EN: Customer identifier.</param>
    private static void ValidateIdentifiers(Guid id, Guid customerId)
    {
        if (id == Guid.Empty) throw new ArgumentException("Wallet identifier cannot be empty.", nameof(id));
        if (customerId == Guid.Empty) throw new ArgumentException("Customer identifier cannot be empty.", nameof(customerId));
    }
}
