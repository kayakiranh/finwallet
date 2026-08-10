using FinWallet.Application.Banking;

namespace FinWallet.Api.Contracts.Banking;

/// <summary>TR: Public bank deposit/withdrawal request'inde internal BankAccount ve pozitif tutarı taşır. EN: Carries internal BankAccount and positive amount in a public bank deposit/withdrawal request.</summary>
public sealed class BankMoneyMovementRequest
{
    /// <summary>TR: Authenticated customer'a ait internal BankAccount kimliğini döndürür veya ayarlar. EN: Gets or sets internal BankAccount identifier owned by the authenticated customer.</summary>
    public Guid BankAccountId { get; init; }
    /// <summary>TR: İşlem tutarını döndürür veya ayarlar. EN: Gets or sets operation amount.</summary>
    public decimal Amount { get; init; }
}

/// <summary>TR: Public bank deposit/withdrawal durable lifecycle sonucunu taşır. EN: Carries durable lifecycle result of a public bank deposit/withdrawal.</summary>
public sealed class BankMoneyMovementResponse
{
    /// <summary>TR: Application sonucunu public API response'una dönüştürür. EN: Converts an Application result into the public API response.</summary>
    /// <param name="result">TR: Durable bank movement sonucu. EN: Durable bank-movement result.</param>
    /// <param name="operation">TR: `BankDeposit` veya `BankWithdrawal` public işlem adı. EN: Public operation name `BankDeposit` or `BankWithdrawal`.</param>
    public BankMoneyMovementResponse(BankMoneyMovementResult result, string operation)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);
        TransactionId = result.TransactionId;
        BankAccountId = result.BankAccountId;
        ExternalTransactionId = result.ExternalTransactionId;
        Operation = operation;
        Amount = result.Amount.Amount;
        Currency = result.Amount.Currency.ToString();
        State = result.State.ToString();
        ProcessingDate = result.ProcessingDate;
        SettlementDate = result.SettlementDate;
        WasReplay = result.WasReplay;
    }

    /// <summary>TR: Internal FinancialTransaction kimliğini döndürür. EN: Gets internal FinancialTransaction identifier.</summary>
    public Guid TransactionId { get; }
    /// <summary>TR: Internal BankAccount kimliğini döndürür. EN: Gets internal BankAccount identifier.</summary>
    public Guid BankAccountId { get; }
    /// <summary>TR: Provider transaction kimliğini veya null değerini döndürür. EN: Gets provider transaction identifier or null.</summary>
    public Guid? ExternalTransactionId { get; }
    /// <summary>TR: Public operation adını döndürür. EN: Gets public operation name.</summary>
    public string Operation { get; }
    /// <summary>TR: İşlem tutarını döndürür. EN: Gets operation amount.</summary>
    public decimal Amount { get; }
    /// <summary>TR: Currency kodunu döndürür. EN: Gets currency code.</summary>
    public string Currency { get; }
    /// <summary>TR: Scheduled/Pending/Completed/Failed lifecycle state'ini döndürür. EN: Gets Scheduled/Pending/Completed/Failed lifecycle state.</summary>
    public string State { get; }
    /// <summary>TR: Processing business tarihini döndürür. EN: Gets processing business date.</summary>
    public DateOnly ProcessingDate { get; }
    /// <summary>TR: Settlement business tarihini döndürür. EN: Gets settlement business date.</summary>
    public DateOnly SettlementDate { get; }
    /// <summary>TR: Sonucun durable replay olup olmadığını döndürür. EN: Gets whether result is a durable replay.</summary>
    public bool WasReplay { get; }
}
