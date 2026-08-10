namespace FinWallet.Domain.Transactions;

/// <summary>
/// TR: FinWallet içinde durable olarak izlenen finansal transaction türlerini MSSQL şemasıyla aynı stabil numeric değerlerle tanımlar.
/// EN: Defines durable FinWallet financial-transaction types using stable numeric values aligned with the MSSQL schema.
/// </summary>
public enum FinancialTransactionType
{
    /// <summary>TR: İki internal wallet arasındaki transferi temsil eder. EN: Represents a transfer between two internal wallets.</summary>
    WalletTransfer = 1,

    /// <summary>TR: Dış bankadan FinWallet tarafına para girişini temsil eder. EN: Represents money entering FinWallet from an external bank.</summary>
    BankDeposit = 2,

    /// <summary>TR: FinWallet'tan dış bankaya para çıkışını temsil eder. EN: Represents money leaving FinWallet to an external bank.</summary>
    BankWithdrawal = 3,

    /// <summary>TR: Önceki finansal işlemin müşteri lehine iadesini temsil eder. EN: Represents a customer refund of a previous financial operation.</summary>
    Refund = 4,

    /// <summary>TR: Önceki ledger etkisini ters kayıtla düzelten reversal işlemini temsil eder. EN: Represents a reversal transaction correcting a previous ledger effect with opposite entries.</summary>
    Reversal = 5,

    /// <summary>TR: Merchant'a yapılan ve opsiyonel kampanya accounting'i içeren alışverişi temsil eder. EN: Represents a merchant purchase with optional campaign accounting.</summary>
    Purchase = 6
}
