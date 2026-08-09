namespace FakeFraud.Api.Models;

/// <summary>
/// TR: Harici FakeFraud sağlayıcısının finansal işlem için verdiği bağımsız risk kararını temsil eder; FinWallet internal fraud kararı değildir.
/// EN: Represents the independent risk decision returned by the external FakeFraud provider; it is not the FinWallet internal fraud decision.
/// </summary>
public enum ExternalFraudDecision
{
    /// <summary>TR: Harici provider işlemi kendi kuralları açısından kabul edilebilir bulmuştur. EN: External provider considers the transaction acceptable under its rules.</summary>
    Allow = 1,

    /// <summary>TR: Harici provider işlemin manuel/ek kontrol gerektirdiğini belirtmiştir. EN: External provider indicates that the transaction requires manual or additional review.</summary>
    Review = 2,

    /// <summary>TR: Harici provider işlemin gerçekleştirilmemesi gerektiğini belirtmiştir. EN: External provider indicates that the transaction should not proceed.</summary>
    Deny = 3
}
