namespace FinWallet.Application.Registration;

/// <summary>
/// TR: Pending müşteri için yeni registration OTP üretip SMS provider'a yeniden göndermek üzere müşteri ve correlation kimliklerini taşır.
/// EN: Carries customer and correlation identifiers used to issue and resend a new registration OTP for a pending customer.
/// </summary>
public sealed class ResendRegistrationOtpCommand
{
    /// <summary>
    /// TR: Registration OTP resend komutunu oluşturur.
    /// EN: Creates the registration-OTP resend command.
    /// </summary>
    /// <param name="customerId">TR: SMS doğrulaması bekleyen müşteri kimliği. EN: Identifier of the customer awaiting SMS verification.</param>
    /// <param name="correlationId">TR: FakeCommunication çağrısına taşınacak request correlation kimliği. EN: Request-correlation identifier propagated to the FakeCommunication call.</param>
    public ResendRegistrationOtpCommand(Guid customerId, string correlationId)
    {
        CustomerId = customerId;
        CorrelationId = correlationId;
    }

    /// <summary>TR: SMS doğrulaması bekleyen müşteri kimliğini döndürür. EN: Gets the identifier of the customer awaiting SMS verification.</summary>
    public Guid CustomerId { get; }

    /// <summary>TR: Provider çağrısına taşınacak correlation kimliğini döndürür. EN: Gets the correlation identifier propagated to the provider call.</summary>
    public string CorrelationId { get; }
}
