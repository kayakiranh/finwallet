namespace FinWallet.Domain.Customers;

/// <summary>
/// TR: Müşterinin kayıt ve kullanım yaşam döngüsündeki mevcut durumunu tanımlar.
/// EN: Defines the customer's current state in the registration and usage lifecycle.
/// </summary>
public enum CustomerStatus
{
    /// <summary>
    /// TR: Müşterinin SMS doğrulaması veya diğer kayıt adımları tamamlanmadan önceki durumunu belirtir.
    /// EN: Indicates that the customer has not yet completed SMS verification or other registration steps.
    /// </summary>
    PendingVerification = 1,

    /// <summary>
    /// TR: Müşterinin sisteme giriş yapabildiği ve uygun finansal işlemleri başlatabildiği aktif durumu belirtir.
    /// EN: Indicates the active state in which the customer may sign in and initiate eligible financial operations.
    /// </summary>
    Active = 2,

    /// <summary>
    /// TR: Güvenlik veya operasyonel nedenlerle müşteri erişiminin geçici olarak engellendiğini belirtir.
    /// EN: Indicates that customer access is temporarily blocked for security or operational reasons.
    /// </summary>
    Blocked = 3,

    /// <summary>
    /// TR: Müşteri ilişkisinin kapatıldığını ve yeni işlem başlatılamayacağını belirtir.
    /// EN: Indicates that the customer relationship is closed and new operations cannot be initiated.
    /// </summary>
    Closed = 4
}
