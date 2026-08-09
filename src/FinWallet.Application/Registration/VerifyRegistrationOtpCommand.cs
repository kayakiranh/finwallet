namespace FinWallet.Application.Registration;

/// <summary>
/// TR: Pending müşteri kaydının SMS OTP ile aktive edilmesi için müşteri kimliği ve tek kullanımlık kodu taşır.
/// EN: Carries the customer identifier and one-time code used to activate a pending customer registration through SMS OTP verification.
/// </summary>
public sealed class VerifyRegistrationOtpCommand
{
    /// <summary>
    /// TR: OTP doğrulama komutunu oluşturur.
    /// EN: Creates the OTP-verification command.
    /// </summary>
    /// <param name="customerId">
    /// TR: SMS doğrulaması bekleyen müşteri kimliği.
    /// EN: Identifier of the customer awaiting SMS verification.
    /// </param>
    /// <param name="code">
    /// TR: Kullanıcının SMS'ten girerek gönderdiği ham OTP kodu.
    /// EN: Raw OTP code submitted by the user from the SMS message.
    /// </param>
    public VerifyRegistrationOtpCommand(Guid customerId, string code)
    {
        CustomerId = customerId;
        Code = code;
    }

    /// <summary>
    /// TR: SMS doğrulaması bekleyen müşteri kimliğini döndürür.
    /// EN: Gets the identifier of the customer awaiting SMS verification.
    /// </summary>
    public Guid CustomerId { get; }

    /// <summary>
    /// TR: Kullanıcının gönderdiği ham OTP kodunu döndürür; loglanmamalıdır.
    /// EN: Gets the raw OTP code submitted by the user; it must not be logged.
    /// </summary>
    public string Code { get; }
}
