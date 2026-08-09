namespace FinWallet.Api.Contracts.Authentication;

/// <summary>
/// TR: Pending müşteri için yeni SMS OTP istenen resend endpoint request sözleşmesini tanımlar.
/// EN: Defines the resend-endpoint request used to request a new SMS OTP for a pending customer.
/// </summary>
public sealed class ResendRegistrationOtpRequest
{
    /// <summary>
    /// TR: SMS doğrulaması bekleyen müşteri kimliğini döndürür veya ayarlar.
    /// EN: Gets or sets the identifier of the customer awaiting SMS verification.
    /// </summary>
    public Guid CustomerId { get; set; }
}
