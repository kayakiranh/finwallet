namespace FinWallet.Application.Banking;

/// <summary>
/// TR: BankAccount lifecycle state'i eşzamanlı başka bir işlem tarafından değiştirildiğinde use-case'in stale sonucu commit etmediğini belirtir.
/// EN: Indicates that BankAccount lifecycle state changed concurrently and the use case did not commit a stale result.
/// </summary>
public sealed class BankAccountConcurrencyException : Exception
{
    /// <summary>TR: Güvenli BankAccount concurrency hatasını oluşturur. EN: Creates the safe BankAccount concurrency failure.</summary>
    public BankAccountConcurrencyException()
        : base("The bank account state changed concurrently.")
    {
    }
}
