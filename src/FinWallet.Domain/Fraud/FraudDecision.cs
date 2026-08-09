namespace FinWallet.Domain.Fraud;

/// <summary>
/// TR: FinWallet fraud karar süreçlerinde provider bağımsız olarak kullanılan Allow, Review ve Deny kararlarını temsil eder.
/// EN: Represents provider-independent Allow, Review and Deny decisions used by FinWallet fraud decision flows.
/// </summary>
public enum FraudDecision
{
    /// <summary>TR: İşlemin fraud açısından devam edebileceğini belirtir. EN: Indicates that the transaction may proceed from a fraud perspective.</summary>
    Allow = 1,

    /// <summary>TR: İşlemin ek veya manuel değerlendirme gerektirdiğini belirtir. EN: Indicates that the transaction requires additional or manual review.</summary>
    Review = 2,

    /// <summary>TR: İşlemin fraud kararı nedeniyle devam etmemesi gerektiğini belirtir. EN: Indicates that the transaction must not proceed because of the fraud decision.</summary>
    Deny = 3
}
