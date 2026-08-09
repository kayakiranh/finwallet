namespace FinWallet.Application.Wallets;

/// <summary>
/// TR: Concurrent wallet create yarışından sonra durable winner kaydı yeniden yüklenemediğinde oluşan güvenli conflict hatasını temsil eder.
/// EN: Represents a safe conflict failure when the durable winner cannot be reloaded after a concurrent wallet-create race.
/// </summary>
public sealed class WalletConcurrencyException : Exception
{
    /// <summary>TR: Wallet concurrency hatasını oluşturur. EN: Creates the wallet-concurrency failure.</summary>
    public WalletConcurrencyException()
        : base("The wallet state changed concurrently.")
    {
    }
}
