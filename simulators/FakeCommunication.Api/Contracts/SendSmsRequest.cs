namespace FakeCommunication.Api.Contracts;

/// <summary>
/// TR: FakeCommunication servisinin registration veya finansal bildirim amacıyla kabul ettiği SMS gönderim isteğini temsil eder.
/// EN: Represents an SMS delivery request accepted by FakeCommunication for registration or financial-notification purposes.
/// </summary>
public sealed class SendSmsRequest
{
    /// <summary>
    /// TR: SMS gönderilecek normalize uluslararası telefon numarasını döndürür veya ayarlar.
    /// EN: Gets or sets the normalized international phone number to which the SMS is sent.
    /// </summary>
    public string Recipient { get; init; } = string.Empty;

    /// <summary>
    /// TR: Mesajın `RegistrationOtp` veya `FinancialNotification` gibi kullanım amacını döndürür veya ayarlar.
    /// EN: Gets or sets the purpose of the message such as `RegistrationOtp` or `FinancialNotification`.
    /// </summary>
    public string MessageType { get; init; } = string.Empty;

    /// <summary>
    /// TR: Fake provider tarafından teslim edilmiş gibi davranılacak SMS gövdesini döndürür veya ayarlar; OTP içerebileceği için loglanmamalıdır.
    /// EN: Gets or sets the SMS body simulated as delivered by the fake provider; it must not be logged because it may contain an OTP.
    /// </summary>
    public string Body { get; init; } = string.Empty;

    /// <summary>
    /// TR: FinWallet request'i ile fake provider kaydını ilişkilendiren correlation kimliğini döndürür veya ayarlar.
    /// EN: Gets or sets the correlation identifier linking the FinWallet request with the fake-provider record.
    /// </summary>
    public string CorrelationId { get; init; } = string.Empty;
}
