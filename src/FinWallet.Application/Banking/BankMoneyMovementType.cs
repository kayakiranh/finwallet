namespace FinWallet.Application.Banking;

/// <summary>
/// TR: FinWallet ile dış banka arasındaki para hareketinin yönünü provider sözleşmesinden bağımsız olarak tanımlar.
/// EN: Defines the direction of a money movement between FinWallet and an external bank independently from provider contracts.
/// </summary>
public enum BankMoneyMovementType
{
    /// <summary>TR: Dış banka hesabına para girişini temsil eder. EN: Represents a credit/deposit into the external bank account.</summary>
    Deposit = 1,

    /// <summary>TR: Dış banka hesabından para çıkışını temsil eder. EN: Represents a debit/withdrawal from the external bank account.</summary>
    Withdrawal = 2
}
