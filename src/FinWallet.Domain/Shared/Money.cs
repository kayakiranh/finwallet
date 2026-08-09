namespace FinWallet.Domain.Shared;

/// <summary>
/// TR: Tutar ve para birimini tek bir finansal değer olarak taşır; farklı para birimlerinin yanlışlıkla birlikte işlenmesini engeller.
/// EN: Carries amount and currency as one financial value and prevents accidental operations across different currencies.
/// </summary>
public readonly record struct Money
{
    /// <summary>
    /// TR: Belirtilen tutar ve para birimi ile değiştirilemez bir para değeri oluşturur.
    /// EN: Creates an immutable monetary value with the specified amount and currency.
    /// </summary>
    /// <param name="amount">
    /// TR: Para değerinin ondalık tutarı; iş kuralına göre pozitiflik kontrolü ilgili operasyon tarafından yapılır.
    /// EN: Decimal monetary amount; positivity is validated by the relevant business operation according to its rules.
    /// </param>
    /// <param name="currency">
    /// TR: Tutarın ait olduğu desteklenen para birimi.
    /// EN: Supported currency to which the amount belongs.
    /// </param>
    public Money(decimal amount, CurrencyCode currency)
    {
        Amount = amount;
        Currency = currency;
    }

    /// <summary>
    /// TR: Para değerinin ondalık tutarını döndürür.
    /// EN: Gets the decimal amount of the monetary value.
    /// </summary>
    public decimal Amount { get; }

    /// <summary>
    /// TR: Tutarın ait olduğu para birimini döndürür; aritmetik işlemlerde para birimi eşleşmesi zorunludur.
    /// EN: Gets the currency of the amount; currency equality is mandatory for arithmetic operations.
    /// </summary>
    public CurrencyCode Currency { get; }

    /// <summary>
    /// TR: Tutarın sıfırdan büyük olup olmadığını belirtir ve para hareketi komutlarının pozitif tutar kontrolünü kolaylaştırır.
    /// EN: Indicates whether the amount is greater than zero and supports positive-amount validation for money movement commands.
    /// </summary>
    public bool IsPositive => Amount > 0m;

    /// <summary>
    /// TR: Aynı para birimindeki başka bir tutarı mevcut değere ekler.
    /// EN: Adds another monetary value with the same currency to the current value.
    /// </summary>
    /// <param name="other">
    /// TR: Eklenecek para değeri; para birimi mevcut değerle aynı olmalıdır.
    /// EN: Monetary value to add; its currency must match the current value.
    /// </param>
    /// <returns>
    /// TR: İki tutarın toplamını aynı para biriminde taşıyan yeni para değerini döndürür.
    /// EN: Returns a new monetary value containing the sum in the same currency.
    /// </returns>
    /// <exception cref="CurrencyMismatchException">
    /// TR: Para birimleri farklı olduğunda oluşur.
    /// EN: Thrown when the currencies differ.
    /// </exception>
    public Money Add(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount + other.Amount, Currency);
    }

    /// <summary>
    /// TR: Aynı para birimindeki başka bir tutarı mevcut değerden çıkarır; negatif sonuç üretme kararı bu değer nesnesinin değil ilgili domain kuralının sorumluluğundadır.
    /// EN: Subtracts another monetary value with the same currency; whether a negative result is allowed is owned by the relevant domain rule rather than this value object.
    /// </summary>
    /// <param name="other">
    /// TR: Çıkarılacak para değeri; para birimi mevcut değerle aynı olmalıdır.
    /// EN: Monetary value to subtract; its currency must match the current value.
    /// </param>
    /// <returns>
    /// TR: İki tutarın farkını aynı para biriminde taşıyan yeni para değerini döndürür.
    /// EN: Returns a new monetary value containing the difference in the same currency.
    /// </returns>
    /// <exception cref="CurrencyMismatchException">
    /// TR: Para birimleri farklı olduğunda oluşur.
    /// EN: Thrown when the currencies differ.
    /// </exception>
    public Money Subtract(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount - other.Amount, Currency);
    }

    /// <summary>
    /// TR: Verilen para değerinin mevcut değerle aynı para birimine sahip olduğunu doğrular.
    /// EN: Validates that the supplied monetary value uses the same currency as the current value.
    /// </summary>
    /// <param name="other">
    /// TR: Para birimi karşılaştırılacak para değeri.
    /// EN: Monetary value whose currency will be compared.
    /// </param>
    /// <exception cref="CurrencyMismatchException">
    /// TR: Para birimleri eşleşmediğinde oluşur.
    /// EN: Thrown when the currencies do not match.
    /// </exception>
    private void EnsureSameCurrency(Money other)
    {
        if (Currency != other.Currency)
        {
            throw new CurrencyMismatchException(Currency, other.Currency);
        }
    }
}
