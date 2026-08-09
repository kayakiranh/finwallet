namespace FinWallet.Domain.Wallets;

/// <summary>
/// TR: Bir cüzdanın finansal işlem kabul edip edemeyeceğini belirleyen yaşam döngüsü durumlarını tanımlar.
/// EN: Defines lifecycle states that determine whether a wallet may accept financial operations.
/// </summary>
public enum WalletStatus
{
    /// <summary>
    /// TR: Cüzdanın izin verilen finansal işlemleri gerçekleştirebildiği aktif durumu belirtir.
    /// EN: Indicates that the wallet is active and may perform permitted financial operations.
    /// </summary>
    Active = 1,

    /// <summary>
    /// TR: Cüzdandan yeni para çıkışı başlatılmasının engellendiği bloke durumu belirtir.
    /// EN: Indicates that initiation of new outgoing money movements from the wallet is blocked.
    /// </summary>
    Blocked = 2,

    /// <summary>
    /// TR: Cüzdanın kalıcı olarak kapatıldığı ve yeni finansal işlem kabul etmediği durumu belirtir.
    /// EN: Indicates that the wallet is permanently closed and no longer accepts new financial operations.
    /// </summary>
    Closed = 3
}
