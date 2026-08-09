namespace FinWallet.Api.Contracts.Authentication;

/// <summary>
/// TR: Pending müşteri kaydının SMS OTP ile doğrulanması için gereken müşteri kimliği ve kod alanlarını tanımlar.
/// EN: Defines the customer identifier and code fields required to verify a pending customer registration using SMS OTP.
/// </summary>
public sealed class VerifyRegistrationOtpRequest
{
    /// <summary>
    /// TR: SMS doğrulaması bekleyen müşteri kimliğini döndürür veya ayarlar.
    /// EN: Gets or sets the identifier of the customer awaiting SMS verification.
    /// </summary>
    public Guid CustomerId { get; set; }

    /// <summary>
    /// TR: Kullanıcının SMS'ten aldığı ham OTP kodunu döndürür veya ayarlar; loglanmamalıdır.
    /// EN: Gets or sets the raw OTP code received by the user through SMS; it must not be logged.
    /// </summary>
    public string Code { get; set; } = string.Empty;
}
