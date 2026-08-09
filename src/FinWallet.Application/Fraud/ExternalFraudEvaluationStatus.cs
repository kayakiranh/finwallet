namespace FinWallet.Application.Fraud;

/// <summary>
/// TR: Fraud audit kaydında external provider değerlendirmesinin çağrılma/tamamlanma durumunu MSSQL şemasıyla aynı stabil numeric değerlerle tanımlar.
/// EN: Defines external-provider evaluation status in fraud audit records using stable numeric values aligned with the MSSQL schema.
/// </summary>
public enum ExternalFraudEvaluationStatus
{
    /// <summary>TR: Internal Deny nedeniyle external provider çağrısına gerek kalmadı. EN: External provider call was not required because the internal decision was Deny.</summary>
    NotRequired = 1,

    /// <summary>TR: External provider değerlendirmesi başarıyla tamamlandı. EN: External-provider evaluation completed successfully.</summary>
    Completed = 2,

    /// <summary>TR: Zorunlu external provider değerlendirmesi timeout/network/provider hatasıyla tamamlanamadı. EN: Required external-provider evaluation could not complete due to timeout, network or provider failure.</summary>
    Unavailable = 3
}
