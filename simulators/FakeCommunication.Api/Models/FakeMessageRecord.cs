namespace FakeCommunication.Api.Models;

/// <summary>
/// TR: FakeCommunication simulatorının test amacıyla bellekte tuttuğu teslim edilmiş mesaj kaydını temsil eder; production log modeli değildir.
/// EN: Represents a delivered message record kept in memory by the FakeCommunication simulator for testing; it is not a production logging model.
/// </summary>
public sealed class FakeMessageRecord
{
    /// <summary>
    /// TR: Fake mesaj kaydını oluşturur.
    /// EN: Creates a fake message record.
    /// </summary>
    /// <param name="messageId">
    /// TR: Fake provider mesaj kimliği.
    /// EN: Fake-provider message identifier.
    /// </param>
    /// <param name="recipient">
    /// TR: Mesajın hedef alıcısı.
    /// EN: Recipient targeted by the message.
    /// </param>
    /// <param name="messageType">
    /// TR: Mesajın registration veya finansal bildirim gibi kullanım amacı.
    /// EN: Purpose of the message such as registration or financial notification.
    /// </param>
    /// <param name="body">
    /// TR: Test sırasında gerçek SMS/e-posta yerine görüntülenebilen mesaj gövdesi; production loglarına yazılmamalıdır.
    /// EN: Message body visible during testing instead of a real SMS/email; it must not be written to production logs.
    /// </param>
    /// <param name="correlationId">
    /// TR: FinWallet çağrısıyla ilişkilendirilen correlation kimliği.
    /// EN: Correlation identifier associated with the FinWallet call.
    /// </param>
    /// <param name="acceptedAt">
    /// TR: Mesajın fake provider tarafından kabul edildiği UTC zaman bilgisi.
    /// EN: UTC timestamp at which the fake provider accepted the message.
    /// </param>
    public FakeMessageRecord(
        Guid messageId,
        string recipient,
        string messageType,
        string body,
        string correlationId,
        DateTimeOffset acceptedAt)
    {
        MessageId = messageId;
        Recipient = recipient;
        MessageType = messageType;
        Body = body;
        CorrelationId = correlationId;
        AcceptedAt = acceptedAt;
    }

    /// <summary>
    /// TR: Fake provider mesaj kimliğini döndürür.
    /// EN: Gets the fake-provider message identifier.
    /// </summary>
    public Guid MessageId { get; }

    /// <summary>
    /// TR: Mesajın hedef alıcısını döndürür.
    /// EN: Gets the target recipient of the message.
    /// </summary>
    public string Recipient { get; }

    /// <summary>
    /// TR: Mesajın kullanım amacını döndürür.
    /// EN: Gets the purpose of the message.
    /// </summary>
    public string MessageType { get; }

    /// <summary>
    /// TR: Yalnızca simulator test ekranı/API'si üzerinden incelenmesi gereken mesaj gövdesini döndürür.
    /// EN: Gets the message body that should only be inspected through simulator testing UI/API.
    /// </summary>
    public string Body { get; }

    /// <summary>
    /// TR: FinWallet request'iyle ilişkilendiren correlation kimliğini döndürür.
    /// EN: Gets the correlation identifier linking the record to the FinWallet request.
    /// </summary>
    public string CorrelationId { get; }

    /// <summary>
    /// TR: Mesajın kabul edildiği UTC zamanını döndürür.
    /// EN: Gets the UTC time at which the message was accepted.
    /// </summary>
    public DateTimeOffset AcceptedAt { get; }
}
