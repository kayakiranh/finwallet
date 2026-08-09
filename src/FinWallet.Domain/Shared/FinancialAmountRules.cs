namespace FinWallet.Domain.Shared;

/// <summary>
/// TR: FinWallet finansal tutarlarının MSSQL `DECIMAL(19,4)` saklama modeline uyumlu ortak precision/scale sınırlarını tanımlar.
/// EN: Defines shared precision/scale limits that keep FinWallet monetary amounts compatible with the MSSQL `DECIMAL(19,4)` storage model.
/// </summary>
public static class FinancialAmountRules
{
    /// <summary>TR: Finansal tutarlarda izin verilen maksimum ondalık basamak sayısıdır. EN: Maximum number of decimal places allowed for financial amounts.</summary>
    public const int Scale = 4;

    /// <summary>TR: `DECIMAL(19,4)` içinde saklanabilen maksimum mutlak finansal tutardır. EN: Maximum absolute financial amount storable in `DECIMAL(19,4)`.</summary>
    public const decimal MaximumAbsoluteAmount = 999_999_999_999_999.9999m;

    /// <summary>
    /// TR: Tutarın `DECIMAL(19,4)` kapasitesi ve scale sınırı içinde olduğunu doğrular; pozitiflik/negatiflik kararını ilgili business operation'a bırakır.
    /// EN: Validates that an amount fits `DECIMAL(19,4)` capacity and scale while leaving positivity/negativity decisions to the relevant business operation.
    /// </summary>
    /// <param name="amount">TR: Doğrulanacak decimal tutar. EN: Decimal amount to validate.</param>
    /// <param name="parameterName">TR: Validation exception'ında kullanılacak parametre adı. EN: Parameter name used in validation exceptions.</param>
    public static void EnsureStorageCompatible(decimal amount, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(parameterName);
        if (decimal.Abs(amount) > MaximumAbsoluteAmount)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Financial amount exceeds DECIMAL(19,4) storage capacity.");
        }

        if (decimal.Round(amount, Scale, MidpointRounding.ToEven) != amount)
        {
            throw new ArgumentOutOfRangeException(parameterName, $"Financial amount cannot contain more than {Scale} decimal places.");
        }
    }
}
