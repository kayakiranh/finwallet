namespace FinWallet.Application.Communication;

/// <summary>
/// TR: FinWallet Application katmanını FakeCommunication API'nin HTTP sözleşmesi ve provider detaylarından ayıran iletişim gateway sınırını tanımlar.
/// EN: Defines the communication gateway boundary that decouples the FinWallet Application layer from FakeCommunication API HTTP contracts and provider details.
/// </summary>
public interface ICommunicationGateway
{
    /// <summary>
    /// TR: Müşteri kayıt doğrulaması için tek kullanımlık OTP kodunu SMS kanalı üzerinden gönderir; OTP hiçbir log alanına yazılmamalıdır.
    /// EN: Sends a one-time OTP code through the SMS channel for customer-registration verification; the OTP must never be written to log fields.
    /// </summary>
    /// <param name="normalizedPhoneNumber">
    /// TR: SMS'in gönderileceği normalize uluslararası telefon numarası.
    /// EN: Normalized international phone number to which the SMS is sent.
    /// </param>
    /// <param name="otpCode">
    /// TR: Yalnızca provider çağrısında kullanılacak ham OTP kodu.
    /// EN: Raw OTP code used only for the provider call.
    /// </param>
    /// <param name="correlationId">
    /// TR: FinWallet ve FakeCommunication çağrısını uçtan uca ilişkilendiren correlation kimliği.
    /// EN: Correlation identifier linking FinWallet and FakeCommunication calls end-to-end.
    /// </param>
    /// <param name="cancellationToken">
    /// TR: Dış SMS çağrısının iptal sinyali.
    /// EN: Cancellation signal for the external SMS call.
    /// </param>
    Task SendRegistrationOtpAsync(
        string normalizedPhoneNumber,
        string otpCode,
        string correlationId,
        CancellationToken cancellationToken);
}
