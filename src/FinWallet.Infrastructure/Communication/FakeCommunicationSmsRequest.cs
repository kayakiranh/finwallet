namespace FinWallet.Infrastructure.Communication;

/// <summary>
/// TR: FakeCommunication HTTP sözleşmesine özel SMS request DTO'sunu Infrastructure içinde tutar ve dış provider modelinin Application/Domain katmanlarına sızmasını engeller.
/// EN: Keeps the SMS request DTO specific to the FakeCommunication HTTP contract inside Infrastructure and prevents the external provider model from leaking into Application/Domain layers.
/// </summary>
internal sealed class FakeCommunicationSmsRequest
{
    /// <summary>
    /// TR: Fake provider DTO'sunu oluşturur.
    /// EN: Creates the fake-provider DTO.
    /// </summary>
    /// <param name="recipient">
    /// TR: SMS hedefi normalize telefon numarası.
    /// EN: Normalized target phone number for the SMS.
    /// </param>
    /// <param name="messageType">
    /// TR: Provider sözleşmesindeki mesaj tipi.
    /// EN: Message type in the provider contract.
    /// </param>
    /// <param name="body">
    /// TR: SMS gövdesi; OTP içerebileceği için loglanmamalıdır.
    /// EN: SMS body; it must not be logged because it may contain an OTP.
    /// </param>
    /// <param name="correlationId">
    /// TR: Dış çağrı boyunca taşınacak correlation kimliği.
    /// EN: Correlation identifier propagated through the external call.
    /// </param>
    public FakeCommunicationSmsRequest(string recipient, string messageType, string body, string correlationId)
    {
        Recipient = recipient;
        MessageType = messageType;
        Body = body;
        CorrelationId = correlationId;
    }

    /// <summary>
    /// TR: SMS hedefi normalize telefon numarasını döndürür.
    /// EN: Gets the normalized target phone number.
    /// </summary>
    public string Recipient { get; }

    /// <summary>
    /// TR: Fake provider mesaj tipi değerini döndürür.
    /// EN: Gets the fake-provider message-type value.
    /// </summary>
    public string MessageType { get; }

    /// <summary>
    /// TR: Provider'a gönderilecek SMS gövdesini döndürür; loglanmamalıdır.
    /// EN: Gets the SMS body sent to the provider; it must not be logged.
    /// </summary>
    public string Body { get; }

    /// <summary>
    /// TR: Dış çağrıyla ilişkili correlation kimliğini döndürür.
    /// EN: Gets the correlation identifier associated with the external call.
    /// </summary>
    public string CorrelationId { get; }
}
