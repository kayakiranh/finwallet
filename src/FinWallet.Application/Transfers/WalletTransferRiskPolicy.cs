namespace FinWallet.Application.Transfers;

/// <summary>
/// TR: Wallet transfer fraud sinyallerinde kullanılan sabit server-side zaman pencerelerini merkezi olarak tanımlar.
/// EN: Centrally defines fixed server-side time windows used by wallet-transfer fraud signals.
/// </summary>
public static class WalletTransferRiskPolicy
{
    /// <summary>TR: Aynı device kimliği ilk kez bu pencere içinde görüldüyse device yeni kabul edilir. EN: A device is considered new when its first-seen time falls within this window.</summary>
    public static readonly TimeSpan NewDeviceWindow = TimeSpan.FromHours(24);

    /// <summary>TR: Kısa dönem transaction velocity sayım penceresidir. EN: Short-term transaction-velocity counting window.</summary>
    public static readonly TimeSpan VelocityWindow = TimeSpan.FromMinutes(5);

    /// <summary>TR: Aggregate transfer tutarı risk penceresidir. EN: Aggregate transfer-amount risk window.</summary>
    public static readonly TimeSpan AggregateAmountWindow = TimeSpan.FromHours(24);
}
