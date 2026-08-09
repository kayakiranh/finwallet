namespace FinWallet.Api.Contracts.Transfers;

/// <summary>
/// TR: Authenticated customer'ın internal wallet transferında kaynak wallet, hedef wallet ve tutarı taşıyan Web API request modelidir.
/// EN: Web API request model carrying source wallet, destination wallet and amount for an authenticated customer's internal wallet transfer.
/// </summary>
public sealed class WalletTransferRequest
{
    /// <summary>TR: Para çıkacak internal wallet kimliğini döndürür veya ayarlar. EN: Gets or sets internal source-wallet identifier.</summary>
    public Guid SourceWalletId { get; init; }

    /// <summary>TR: Para girecek internal wallet kimliğini döndürür veya ayarlar. EN: Gets or sets internal destination-wallet identifier.</summary>
    public Guid DestinationWalletId { get; init; }

    /// <summary>TR: Pozitif ve en fazla dört ondalık basamaklı transfer tutarını döndürür veya ayarlar. EN: Gets or sets positive transfer amount with at most four decimal places.</summary>
    public decimal Amount { get; init; }
}
