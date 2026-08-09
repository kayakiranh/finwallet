using FinWallet.Domain.Shared;

namespace FinWallet.Domain.Fraud;

/// <summary>
/// TR: Internal fraud kurallarının değerlendirdiği currency-aware finansal tutar, velocity ve cihaz risk sinyallerini altyapı bağımlılığı olmadan taşır.
/// EN: Carries currency-aware financial amount, velocity and device risk signals evaluated by internal fraud rules without infrastructure dependencies.
/// </summary>
public sealed class FraudAssessmentContext
{
    /// <summary>
    /// TR: Internal fraud değerlendirme context'ini oluşturur ve tutar/currency/velocity tutarlılığını doğrular.
    /// EN: Creates the internal fraud-assessment context and validates amount/currency/velocity consistency.
    /// </summary>
    /// <param name="transactionReference">TR: Değerlendirilen finansal transaction kimliği. EN: Financial transaction identifier being evaluated.</param>
    /// <param name="customerReference">TR: Değerlendirilen müşteri kimliği. EN: Customer identifier being evaluated.</param>
    /// <param name="amount">TR: Currency-aware pozitif işlem tutarı. EN: Currency-aware positive transaction amount.</param>
    /// <param name="amountLastTwentyFourHours">TR: Aynı currency'de son yirmi dört saat aggregate tutarı. EN: Twenty-four-hour aggregate amount in the same currency.</param>
    /// <param name="transactionCountLastFiveMinutes">TR: Son beş dakikadaki işlem sayısı. EN: Transaction count during the previous five minutes.</param>
    /// <param name="isNewDevice">TR: İşlem cihazının müşteri için yeni olup olmadığını belirtir. EN: Indicates whether the transaction device is new for the customer.</param>
    /// <param name="isKnownBeneficiary">TR: Transfer alıcısının müşteri için daha önce bilinen beneficiary olup olmadığını belirtir. EN: Indicates whether the transfer beneficiary is previously known for the customer.</param>
    public FraudAssessmentContext(
        Guid transactionReference,
        Guid customerReference,
        Money amount,
        Money amountLastTwentyFourHours,
        int transactionCountLastFiveMinutes,
        bool isNewDevice,
        bool isKnownBeneficiary)
    {
        if (transactionReference == Guid.Empty) throw new ArgumentException("Transaction reference cannot be empty.", nameof(transactionReference));
        if (customerReference == Guid.Empty) throw new ArgumentException("Customer reference cannot be empty.", nameof(customerReference));
        if (!amount.IsPositive) throw new ArgumentOutOfRangeException(nameof(amount));
        if (amountLastTwentyFourHours.Amount < 0) throw new ArgumentOutOfRangeException(nameof(amountLastTwentyFourHours));
        if (amount.Currency != amountLastTwentyFourHours.Currency) throw new CurrencyMismatchException(amount.Currency, amountLastTwentyFourHours.Currency);
        if (transactionCountLastFiveMinutes < 0) throw new ArgumentOutOfRangeException(nameof(transactionCountLastFiveMinutes));

        TransactionReference = transactionReference;
        CustomerReference = customerReference;
        Amount = amount;
        AmountLastTwentyFourHours = amountLastTwentyFourHours;
        TransactionCountLastFiveMinutes = transactionCountLastFiveMinutes;
        IsNewDevice = isNewDevice;
        IsKnownBeneficiary = isKnownBeneficiary;
    }

    /// <summary>TR: Değerlendirilen transaction referansını döndürür. EN: Gets the evaluated transaction reference.</summary>
    public Guid TransactionReference { get; }

    /// <summary>TR: Değerlendirilen customer referansını döndürür. EN: Gets the evaluated customer reference.</summary>
    public Guid CustomerReference { get; }

    /// <summary>TR: Currency-aware işlem tutarını döndürür. EN: Gets the currency-aware transaction amount.</summary>
    public Money Amount { get; }

    /// <summary>TR: Aynı currency'deki son yirmi dört saat aggregate tutarını döndürür. EN: Gets the twenty-four-hour aggregate amount in the same currency.</summary>
    public Money AmountLastTwentyFourHours { get; }

    /// <summary>TR: Son beş dakikadaki işlem sayısını döndürür. EN: Gets the transaction count in the previous five minutes.</summary>
    public int TransactionCountLastFiveMinutes { get; }

    /// <summary>TR: İşlem cihazının yeni olup olmadığını döndürür. EN: Gets whether the transaction device is new.</summary>
    public bool IsNewDevice { get; }

    /// <summary>TR: Transfer beneficiary'sinin daha önce bilinen alıcı olup olmadığını döndürür. EN: Gets whether the transfer beneficiary is previously known.</summary>
    public bool IsKnownBeneficiary { get; }
}
