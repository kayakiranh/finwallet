namespace FinWallet.Domain.Ledger;

/// <summary>
/// TR: Double-entry ledger hesabının muhasebe sınıfını temsil eder ve ileride normal bakiye yönü/raporlama kurallarının belirlenmesini sağlar.
/// EN: Represents the accounting class of a double-entry ledger account and enables future normal-balance/reporting rules to be determined.
/// </summary>
public enum LedgerAccountType
{
    /// <summary>TR: Banka settlement/cash gibi sistem varlık hesaplarını temsil eder. EN: Represents system asset accounts such as bank settlement/cash.</summary>
    Asset = 1,

    /// <summary>TR: Customer wallet veya merchant payable gibi sistem yükümlülük hesaplarını temsil eder. EN: Represents system liability accounts such as customer wallet or merchant payable.</summary>
    Liability = 2,

    /// <summary>TR: Platform komisyon geliri gibi gelir hesaplarını temsil eder. EN: Represents revenue accounts such as platform fee revenue.</summary>
    Revenue = 3,

    /// <summary>TR: Platform sponsorlu kampanya gideri gibi gider hesaplarını temsil eder. EN: Represents expense accounts such as platform-funded campaign expense.</summary>
    Expense = 4,

    /// <summary>TR: Sistem özkaynak/denge hesaplarını temsil eder. EN: Represents system equity/balancing accounts.</summary>
    Equity = 5
}
