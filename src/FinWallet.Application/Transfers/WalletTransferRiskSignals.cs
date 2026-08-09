using FinWallet.Domain.Shared;

namespace FinWallet.Application.Transfers;

/// <summary>
/// TR: Wallet transfer fraud değerlendirmesi için tamamen server-side üretilen wallet, customer, device, velocity ve beneficiary sinyallerini taşır.
/// EN: Carries wallet, customer, device, velocity and beneficiary signals derived entirely server-side for wallet-transfer fraud evaluation.
/// </summary>
public sealed class WalletTransferRiskSignals
{
    /// <summary>TR: Server-derived transfer risk signal setini oluşturur. EN: Creates the server-derived transfer risk-signal set.</summary>
    /// <param name="currency">TR: Source/destination wallet ortak currency değeri. EN: Shared source/destination wallet currency.</param>
    /// <param name="countryCode">TR: Customer ülke kodu. EN: Customer country code.</param>
    /// <param name="deviceReference">TR: Raw DeviceId yerine kullanılan PII içermeyen stabil hash referansı. EN: Stable non-PII hash reference used instead of raw DeviceId.</param>
    /// <param name="isNewDevice">TR: Device ilk görülme zamanının new-device window içinde olup olmadığını belirtir. EN: Indicates whether first-seen device time falls inside the new-device window.</param>
    /// <param name="transactionCountLastFiveMinutes">TR: Son beş dakikadaki başarılı wallet transfer sayısı. EN: Number of successful wallet transfers in the previous five minutes.</param>
    /// <param name="amountLastTwentyFourHours">TR: Son yirmi dört saatte aynı currency'deki başarılı transfer toplamı. EN: Total successful transfer amount in the same currency over the previous twenty-four hours.</param>
    /// <param name="isKnownBeneficiary">TR: Destination wallet'a daha önce başarılı transfer olup olmadığını belirtir. EN: Indicates whether the destination wallet has received a previous successful transfer from the customer.</param>
    public WalletTransferRiskSignals(
        CurrencyCode currency,
        string countryCode,
        string deviceReference,
        bool isNewDevice,
        int transactionCountLastFiveMinutes,
        decimal amountLastTwentyFourHours,
        bool isKnownBeneficiary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(countryCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceReference);
        if (transactionCountLastFiveMinutes < 0) throw new ArgumentOutOfRangeException(nameof(transactionCountLastFiveMinutes));
        if (amountLastTwentyFourHours < 0m) throw new ArgumentOutOfRangeException(nameof(amountLastTwentyFourHours));
        FinancialAmountRules.EnsureStorageCompatible(amountLastTwentyFourHours, nameof(amountLastTwentyFourHours));

        Currency = currency;
        CountryCode = countryCode.Trim().ToUpperInvariant();
        DeviceReference = deviceReference.Trim();
        IsNewDevice = isNewDevice;
        TransactionCountLastFiveMinutes = transactionCountLastFiveMinutes;
        AmountLastTwentyFourHours = amountLastTwentyFourHours;
        IsKnownBeneficiary = isKnownBeneficiary;
    }

    /// <summary>TR: Transfer wallet currency değerini döndürür. EN: Gets transfer-wallet currency.</summary>
    public CurrencyCode Currency { get; }

    /// <summary>TR: Normalize customer ülke kodunu döndürür. EN: Gets normalized customer country code.</summary>
    public string CountryCode { get; }

    /// <summary>TR: PII içermeyen stabil device hash referansını döndürür. EN: Gets stable non-PII device hash reference.</summary>
    public string DeviceReference { get; }

    /// <summary>TR: Device yeni kabul ediliyorsa true döndürür. EN: Gets whether the device is considered new.</summary>
    public bool IsNewDevice { get; }

    /// <summary>TR: Son beş dakikadaki başarılı transfer sayısını döndürür. EN: Gets successful transfer count in the previous five minutes.</summary>
    public int TransactionCountLastFiveMinutes { get; }

    /// <summary>TR: Son yirmi dört saat aynı-currency transfer toplamını döndürür. EN: Gets same-currency transfer total over the previous twenty-four hours.</summary>
    public decimal AmountLastTwentyFourHours { get; }

    /// <summary>TR: Destination wallet geçmişte başarılı beneficiary olmuşsa true döndürür. EN: Gets whether the destination wallet has been a successful beneficiary before.</summary>
    public bool IsKnownBeneficiary { get; }
}
