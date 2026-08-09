namespace FinWallet.Application.Banking;

/// <summary>
/// TR: Banka hesabı açılışında belirtilen wallet'ın authenticated customer'a ait olmadığını veya bulunamadığını belirtir; ownership bilgisini sızdırmamak için aynı hata kullanılır.
/// EN: Indicates that the wallet supplied for bank-account opening was not found or does not belong to the authenticated customer; the same error is used to avoid leaking ownership information.
/// </summary>
public sealed class BankAccountWalletNotFoundException : Exception
{
    /// <summary>TR: Güvenli wallet-not-found hatasını oluşturur. EN: Creates the safe wallet-not-found failure.</summary>
    public BankAccountWalletNotFoundException()
        : base("The wallet was not found.")
    {
    }
}
