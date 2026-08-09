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
    /// <param name="id">
    /// TR: Cüzdanın sistem içindeki benzersiz kimliği.
    /// EN: Unique identifier of the wallet inside the system.
    /// </param>
    /// <param name="customerId">
    /// TR: Cüzdanın sahibi olan müşterinin benzersiz kimliği.
    /// EN: Unique identifier of the customer who owns the wallet.
    /// </param>
    /// <param name="currency">
    /// TR: Cüzdanın kabul ettiği ve tüm bakiyelerin tutulduğu para birimi.
    /// EN: Currency accepted by the wallet and used for all of its balances.
    /// </param>
    /// <param name="createdAt">
    /// TR: Cüzdanın oluşturulduğu UTC zaman bilgisi.
    /// EN: UTC timestamp at which the wallet was created.
    /// </param>
    /// <returns>
    /// TR: Sıfır kullanılabilir ve bloke bakiye ile oluşturulmuş aktif cüzdanı döndürür.
    /// EN: Returns an active wallet created with zero available and blocked balances.
    /// </returns>
    public static Wallet Create(Guid id, Guid customerId, CurrencyCode currency, DateTimeOffset createdAt)
    {
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
    /// TR: Cüzdanın sistem içindeki benzersiz kimliğini döndürür.
    /// EN: Gets the wallet's unique identifier inside the system.
    /// </summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// TR: Cüzdanın sahibi olan müşterinin benzersiz kimliğini döndürür.
    /// EN: Gets the unique identifier of the customer who owns the wallet.
    /// </summary>
    public Guid CustomerId { get; private set; }

    /// <summary>
    /// TR: Cüzdanın işlem yaptığı para birimini döndürür; farklı para birimindeki tutarlar doğrudan uygulanamaz.
    /// EN: Gets the currency used by the wallet; monetary values in another currency cannot be applied directly.
    /// </summary>
    public CurrencyCode Currency { get; private set; }

    /// <summary>
    /// TR: Yeni harcama veya transfer işlemlerinde kullanılabilecek mevcut bakiyeyi döndürür.
    /// EN: Gets the current balance available for new purchases or transfers.
    /// </summary>
    public decimal AvailableBalance { get; private set; }

    /// <summary>
    /// TR: Başlatılmış ancak henüz kesinleşmemiş dış finansal işlemler için ayrılmış bakiyeyi döndürür.
    /// EN: Gets the balance reserved for external financial operations that have started but are not yet finalized.
    /// </summary>
    public decimal BlockedBalance { get; private set; }

    /// <summary>
    /// TR: Cüzdanın yeni finansal işlem kabul edip edemeyeceğini belirleyen mevcut durumunu döndürür.
    /// EN: Gets the current wallet state that determines whether new financial operations may be initiated.
    /// </summary>
    public WalletStatus Status { get; private set; }

    /// <summary>
    /// TR: Cüzdanın oluşturulduğu UTC zaman bilgisini döndürür.
    /// EN: Gets the UTC timestamp at which the wallet was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// TR: Gelen finansal tutarı kullanılabilir bakiyeye ekler; kapalı cüzdan para kabul edemez.
    /// EN: Adds an incoming monetary amount to the available balance; a closed wallet cannot receive funds.
    /// </summary>
    /// <param name="amount">
    /// TR: Cüzdana eklenecek pozitif ve aynı para birimindeki para değeri.
    /// EN: Positive monetary value in the wallet currency to add.
    /// </param>
    public void Credit(Money amount)
    {
        EnsureCanReceive();
        EnsurePositive(amount);
        EnsureCurrency(amount);
        AvailableBalance += amount.Amount;
    }

    /// <summary>
    /// TR: Kesinleşen dahili para çıkışını kullanılabilir bakiyeden düşer ve yetersiz bakiye durumunda değişiklik yapmaz.
    /// EN: Debits a finalized internal outgoing amount from the available balance and makes no change when funds are insufficient.
    /// </summary>
    /// <param name="amount">
    /// TR: Cüzdandan düşülecek pozitif ve aynı para birimindeki para değeri.
    /// EN: Positive monetary value in the wallet currency to debit.
    /// </param>
    /// <exception cref="InsufficientBalanceException">
    /// TR: Kullanılabilir bakiye istenen tutardan düşük olduğunda oluşur.
    /// EN: Thrown when the available balance is lower than the requested amount.
    /// </exception>
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
    /// TR: Banka çekimi gibi bekleyen dış işlemler için tutarı kullanılabilir bakiyeden bloke bakiyeye taşır.
    /// EN: Moves an amount from available balance to blocked balance for pending external operations such as bank withdrawals.
    /// </summary>
    /// <param name="amount">
    /// TR: Bloke edilecek pozitif ve aynı para birimindeki para değeri.
    /// EN: Positive monetary value in the wallet currency to block.
    /// </param>
    /// <exception cref="InsufficientBalanceException">
    /// TR: Kullanılabilir bakiye bloke edilecek tutardan düşük olduğunda oluşur.
    /// EN: Thrown when the available balance is lower than the amount to block.
    /// </exception>
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
    /// <param name="amount">
    /// TR: Bloke bakiyeden serbest bırakılacak pozitif ve aynı para birimindeki para değeri.
    /// EN: Positive monetary value in the wallet currency to release from blocked balance.
    /// </param>
    /// <exception cref="InsufficientBalanceException">
    /// TR: Bloke bakiye serbest bırakılacak tutardan düşük olduğunda oluşur.
    /// EN: Thrown when the blocked balance is lower than the amount to release.
    /// </exception>
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
    /// EN: Permanently removes the related amount from blocked balance when an external financial operation is finalized successfully.
    /// </summary>
    /// <param name="amount">
    /// TR: Kesinleştirilerek bloke bakiyeden düşülecek pozitif ve aynı para birimindeki para değeri.
    /// EN: Positive monetary value in the wallet currency to settle from blocked balance.
    /// </param>
    /// <exception cref="InsufficientBalanceException">
    /// TR: Bloke bakiye kesinleştirilecek tutardan düşük olduğunda oluşur.
    /// EN: Thrown when the blocked balance is lower than the amount to settle.
    /// </exception>
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

    /// <summary>
    /// TR: Aktif cüzdandan yeni para çıkışı başlatılmasını engellemek için cüzdanı bloke eder.
    /// EN: Blocks an active wallet to prevent initiation of new outgoing money movements.
    /// </summary>
    public void Block()
    {
        EnsureActive();
        Status = WalletStatus.Blocked;
    }

    /// <summary>
    /// TR: Cüzdanın yeni para çıkışı başlatabilecek aktif durumda olduğunu doğrular.
    /// EN: Validates that the wallet is active and may initiate new outgoing money movements.
    /// </summary>
    private void EnsureActive()
    {
        if (Status != WalletStatus.Active)
        {
            throw new InvalidOperationException($"Wallet in '{Status}' state cannot initiate this operation.");
        }
    }

    /// <summary>
    /// TR: Cüzdanın gelen para hareketini kabul edebilecek durumda olduğunu doğrular; kapalı cüzdanlara kredi uygulanamaz.
    /// EN: Validates that the wallet may receive incoming funds; closed wallets cannot be credited.
    /// </summary>
    private void EnsureCanReceive()
    {
        if (Status == WalletStatus.Closed)
        {
            throw new InvalidOperationException("Closed wallet cannot receive funds.");
        }
    }

    /// <summary>
    /// TR: Finansal hareket tutarının sıfırdan büyük olduğunu doğrular.
    /// EN: Validates that the monetary amount used for a financial movement is greater than zero.
    /// </summary>
    /// <param name="amount">
    /// TR: Pozitifliği doğrulanacak para değeri.
    /// EN: Monetary value whose positivity will be validated.
    /// </param>
    private static void EnsurePositive(Money amount)
    {
        if (!amount.IsPositive)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Financial movement amount must be greater than zero.");
        }
    }

    /// <summary>
    /// TR: Finansal hareketin para biriminin cüzdan para birimiyle aynı olduğunu doğrular.
    /// EN: Validates that the financial movement currency matches the wallet currency.
    /// </summary>
    /// <param name="amount">
    /// TR: Para birimi doğrulanacak para değeri.
    /// EN: Monetary value whose currency will be validated.
    /// </param>
    private void EnsureCurrency(Money amount)
    {
        if (amount.Currency != Currency)
        {
            throw new CurrencyMismatchException(Currency, amount.Currency);
        }
    }
}
