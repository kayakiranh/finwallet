namespace FinWallet.Domain.Shared;

/// <summary>
/// TR: FinWallet içerisinde ayrı cüzdan ve banka hesabı açılabilen desteklenen para birimlerini tanımlar.
/// EN: Defines the supported currencies for which separate wallets and bank accounts can be opened in FinWallet.
/// </summary>
public enum CurrencyCode
{
    /// <summary>
    /// TR: Türk lirasını temsil eder.
    /// EN: Represents the Turkish lira.
    /// </summary>
    TRY = 1,

    /// <summary>
    /// TR: Amerikan dolarını temsil eder.
    /// EN: Represents the United States dollar.
    /// </summary>
    USD = 2,

    /// <summary>
    /// TR: Euro para birimini temsil eder.
    /// EN: Represents the euro currency.
    /// </summary>
    EUR = 3
}
