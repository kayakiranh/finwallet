namespace FinWallet.Application.Communication;

/// <summary>
/// TR: FinWallet Application katmanını FakeCommunication API'nin HTTP sözleşmesi ve provider detaylarından ayıran iletişim gateway sınırını tanımlar.
/// EN: Defines the communication gateway boundary that decouples the FinWallet Application layer from FakeCommunication API HTTP contracts and provider details.
/// </summary>
public interface ICommunicationGateway
{
    /// <summary>
    /// TR: Müşteri kayıt doğrulaması için tek kullanımlık OTP kodunu SMS kanalı üzerinden gönderir; OTP hiçbir log alanına veya Outbox kaydına yazılmamalıdır.
    /// EN: Sends a one-time OTP code through the SMS channel for customer-registration verification; the OTP must never be written to logs or Outbox records.
    /// </summary>
    /// <param name="normalizedPhoneNumber">TR: SMS'in gönderileceği normalize uluslararası telefon numarası. EN: Normalized international phone number to which the SMS is sent.</param>
    /// <param name="otpCode">TR: Yalnızca provider çağrısında kullanılacak ham OTP kodu. EN: Raw OTP code used only for the provider call.</param>
    /// <param name="correlationId">TR: FinWallet ve FakeCommunication çağrısını uçtan uca ilişkilendiren correlation kimliği. EN: Correlation identifier linking FinWallet and FakeCommunication calls end-to-end.</param>
    /// <param name="cancellationToken">TR: Dış SMS çağrısının iptal sinyali. EN: Cancellation signal for the external SMS call.</param>
    Task SendRegistrationOtpAsync(string normalizedPhoneNumber, string otpCode, string correlationId, CancellationToken cancellationToken);

    /// <summary>
    /// TR: Finansal commit sonrasında Outbox worker tarafından oluşturulan hassas olmayan bildirim metnini SMS provider'a gönderir; bu çağrının başarısız olması finansal transaction'ı geri almaz.
    /// EN: Sends a non-sensitive post-commit notification created by the Outbox worker to the SMS provider; failure of this call never rolls back the financial transaction.
    /// </summary>
    /// <param name="normalizedPhoneNumber">TR: Müşterinin normalize telefon numarası. EN: Customer's normalized phone number.</param>
    /// <param name="messageType">TR: Provider'a gönderilecek stabil bildirim tipi. EN: Stable notification type sent to the provider.</param>
    /// <param name="body">TR: OTP, token veya ham PII içermeyen bildirim gövdesi. EN: Notification body containing no OTP, token or raw PII.</param>
    /// <param name="correlationId">TR: Outbox mesajıyla provider çağrısını ilişkilendiren correlation kimliği. EN: Correlation identifier linking the Outbox message with the provider call.</param>
    /// <param name="cancellationToken">TR: Dış SMS çağrısının iptal sinyali. EN: Cancellation signal for the external SMS call.</param>
    Task SendSmsAsync(string normalizedPhoneNumber, string messageType, string body, string correlationId, CancellationToken cancellationToken);
}
