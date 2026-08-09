namespace FakeBank.Api.Models;

/// <summary>
/// TR: FakeBank simulatorında dış banka hesabına yönelik para hareketi tipini temsil eder.
/// EN: Represents the money-movement type applied to an external bank account in the FakeBank simulator.
/// </summary>
public enum FakeBankTransactionType
{
    /// <summary>TR: Harici banka hesabına para girişi sağlayan işlemi temsil eder. EN: Represents a transaction that credits funds into the external bank account.</summary>
    Deposit = 1,

    /// <summary>TR: Harici banka hesabından para çıkışı sağlayan işlemi temsil eder. EN: Represents a transaction that debits funds from the external bank account.</summary>
    Withdrawal = 2
}
