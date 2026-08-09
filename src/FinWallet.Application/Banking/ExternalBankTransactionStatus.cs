namespace FinWallet.Application.Banking;

/// <summary>
/// TR: Application katmanında dış banka transaction durumunu provider enumundan bağımsız biçimde temsil eder.
/// EN: Represents an external-bank transaction state in the Application layer independently from provider enums.
/// </summary>
public enum ExternalBankTransactionStatus
{
    /// <summary>TR: Provider transaction henüz sonuçlanmamıştır. EN: Provider transaction has not completed yet.</summary>
    Pending = 1,

    /// <summary>TR: Provider transaction başarıyla tamamlanmıştır. EN: Provider transaction completed successfully.</summary>
    Completed = 2,

    /// <summary>TR: Provider transaction başarısız sonuçlanmıştır. EN: Provider transaction failed.</summary>
    Failed = 3
}
