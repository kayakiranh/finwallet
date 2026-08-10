using FinWallet.Application.Corrections;

namespace FinWallet.Api.Contracts.Corrections;

/// <summary>TR: Completed Refund/Reversal sonucunu public API sözleşmesine taşır. EN: Carries completed Refund/Reversal result to public API contract.</summary>
public sealed class TransactionCorrectionResponse
{
    /// <summary>TR: Application correction sonucunu public response'a dönüştürür. EN: Converts Application correction result into public response.</summary>
    /// <param name="result">TR: Completed correction sonucu. EN: Completed correction result.</param>
    public TransactionCorrectionResponse(TransactionCorrectionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        TransactionId = result.TransactionId;
        OriginalTransactionId = result.OriginalTransactionId;
        Type = result.Type.ToString();
        Amount = result.Amount.Amount;
        Currency = result.Amount.Currency.ToString();
        CompletedAt = result.CompletedAt;
        WasReplay = result.WasReplay;
    }

    /// <summary>TR: Yeni correction transaction kimliğini döndürür. EN: Gets new correction transaction identifier.</summary>
    public Guid TransactionId { get; }
    /// <summary>TR: Original transaction kimliğini döndürür. EN: Gets original transaction identifier.</summary>
    public Guid OriginalTransactionId { get; }
    /// <summary>TR: Refund/Reversal tipini döndürür. EN: Gets Refund/Reversal type.</summary>
    public string Type { get; }
    /// <summary>TR: Correction tutarını döndürür. EN: Gets correction amount.</summary>
    public decimal Amount { get; }
    /// <summary>TR: Currency kodunu döndürür. EN: Gets currency code.</summary>
    public string Currency { get; }
    /// <summary>TR: Completion UTC zamanını döndürür. EN: Gets completion UTC timestamp.</summary>
    public DateTimeOffset CompletedAt { get; }
    /// <summary>TR: Durable replay bilgisini döndürür. EN: Gets durable-replay state.</summary>
    public bool WasReplay { get; }
}
