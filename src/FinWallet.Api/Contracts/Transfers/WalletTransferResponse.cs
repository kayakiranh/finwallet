using FinWallet.Application.Transfers;

namespace FinWallet.Api.Contracts.Transfers;

/// <summary>
/// TR: Completed wallet transfer'ın immutable transaction bilgilerini dış Web API sözleşmesinde temsil eder.
/// EN: Represents immutable transaction information of a Completed wallet transfer in the external Web API contract.
/// </summary>
public sealed class WalletTransferResponse
{
    /// <summary>TR: Application posting sonucunu API response modeline dönüştürür. EN: Converts an Application posting result into the API response model.</summary>
    /// <param name="result">TR: Completed wallet-transfer posting sonucu. EN: Completed wallet-transfer posting result.</param>
    public WalletTransferResponse(WalletTransferPostingResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        TransactionId = result.TransactionId;
        SourceWalletId = result.SourceWalletId;
        DestinationWalletId = result.DestinationWalletId;
        Amount = result.Amount.Amount;
        Currency = result.Amount.Currency.ToString();
        CompletedAt = result.CompletedAt;
        WasReplay = result.WasReplay;
    }

    /// <summary>TR: Durable FinancialTransaction kimliğini döndürür. EN: Gets durable FinancialTransaction identifier.</summary>
    public Guid TransactionId { get; }

    /// <summary>TR: Source wallet kimliğini döndürür. EN: Gets source-wallet identifier.</summary>
    public Guid SourceWalletId { get; }

    /// <summary>TR: Destination wallet kimliğini döndürür. EN: Gets destination-wallet identifier.</summary>
    public Guid DestinationWalletId { get; }

    /// <summary>TR: Transfer tutarını döndürür. EN: Gets transfer amount.</summary>
    public decimal Amount { get; }

    /// <summary>TR: Transfer currency kodunu döndürür. EN: Gets transfer currency code.</summary>
    public string Currency { get; }

    /// <summary>TR: Original completion UTC zamanını döndürür. EN: Gets original UTC completion time.</summary>
    public DateTimeOffset CompletedAt { get; }

    /// <summary>TR: Response daha önce tamamlanmış idempotent request replay'i ise true döndürür. EN: Gets whether the response is a replay of a previously completed idempotent request.</summary>
    public bool WasReplay { get; }
}
