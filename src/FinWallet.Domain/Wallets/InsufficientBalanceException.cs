using FinWallet.Domain.Shared;

namespace FinWallet.Domain.Wallets;

/// <summary>
/// TR: Cüzdandaki kullanılabilir veya bloke bakiyenin istenen finansal hareket için yetersiz olduğunu belirtir.
/// EN: Indicates that the wallet's available or blocked balance is insufficient for the requested financial movement.
/// </summary>
public sealed class InsufficientBalanceException : InvalidOperationException
{
    /// <summary>
    /// TR: Kullanılabilir tutar ve ihtiyaç duyulan tutar bilgileriyle yeni bir yetersiz bakiye hatası oluşturur.
    /// EN: Creates a new insufficient balance error with the available and required monetary values.
    /// </summary>
    /// <param name="available">
    /// TR: İşlem sırasında kullanılabilir olan para değeri.
    /// EN: Monetary value available at the time of the operation.
    /// </param>
    /// <param name="required">
    /// TR: İşlemin tamamlanması için gereken para değeri.
    /// EN: Monetary value required to complete the operation.
    /// </param>
    public InsufficientBalanceException(Money available, Money required)
        : base($"Insufficient balance. Available '{available.Amount} {available.Currency}', required '{required.Amount} {required.Currency}'.")
    {
        Available = available;
        Required = required;
    }

    /// <summary>
    /// TR: İşlem sırasında kullanılabilir olan para değerini döndürür.
    /// EN: Gets the monetary value available at the time of the operation.
    /// </summary>
    public Money Available { get; }

    /// <summary>
    /// TR: İşlemin tamamlanması için gereken para değerini döndürür.
    /// EN: Gets the monetary value required to complete the operation.
    /// </summary>
    public Money Required { get; }
}
