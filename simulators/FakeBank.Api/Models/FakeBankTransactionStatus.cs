namespace FakeBank.Api.Models;

/// <summary>
/// TR: FakeBank tarafındaki harici para hareketi isteğinin provider yaşam döngüsü durumunu temsil eder.
/// EN: Represents the provider lifecycle state of an external money-movement request inside FakeBank.
/// </summary>
public enum FakeBankTransactionStatus
{
    /// <summary>TR: İşlem provider tarafından kabul edilmiş ancak finansal sonuç henüz kesinleşmemiştir. EN: Transaction was accepted by the provider but its financial outcome is not final yet.</summary>
    Pending = 1,

    /// <summary>TR: İşlem provider tarafından başarıyla tamamlanmıştır. EN: Transaction was completed successfully by the provider.</summary>
    Completed = 2,

    /// <summary>TR: İşlem provider tarafından başarısız olarak sonuçlandırılmıştır. EN: Transaction was finalized as failed by the provider.</summary>
    Failed = 3
}
