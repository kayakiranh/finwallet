namespace FinWallet.Application.Outbox;

/// <summary>TR: SQL tarafından claim edilmiş ve dış provider'a gönderilmeyi bekleyen tek Outbox mesajını taşır. EN: Carries one SQL-claimed Outbox message awaiting external-provider delivery.</summary>
public sealed class OutboxMessage
{
    /// <summary>TR: Claimed Outbox mesajını oluşturur. EN: Creates a claimed Outbox message.</summary>
    /// <param name="id">TR: Outbox message kimliği. EN: Outbox-message identifier.</param>
    /// <param name="messageType">TR: Stabil event/message tipi. EN: Stable event/message type.</param>
    /// <param name="aggregateId">TR: İlişkili financial transaction kimliği veya null. EN: Related financial-transaction identifier or null.</param>
    /// <param name="payloadJson">TR: Durable JSON payload; yalnız worker tarafından güvenli alanları çıkarmak için kullanılır. EN: Durable JSON payload used only by the worker to extract safe fields.</param>
    /// <param name="correlationId">TR: Persisted correlation kimliği veya null. EN: Persisted correlation identifier or null.</param>
    /// <param name="attemptCount">TR: Claim dahil toplam gönderim deneme sayısı. EN: Total delivery-attempt count including this claim.</param>
    public OutboxMessage(Guid id, string messageType, Guid? aggregateId, string payloadJson, string? correlationId, int attemptCount)
    {
        if (id == Guid.Empty) throw new ArgumentException("Outbox message identifier cannot be empty.", nameof(id));
        ArgumentException.ThrowIfNullOrWhiteSpace(messageType);
        ArgumentException.ThrowIfNullOrWhiteSpace(payloadJson);
        if (attemptCount < 1) throw new ArgumentOutOfRangeException(nameof(attemptCount));
        Id = id;
        MessageType = messageType.Trim();
        AggregateId = aggregateId;
        PayloadJson = payloadJson;
        CorrelationId = string.IsNullOrWhiteSpace(correlationId) ? null : correlationId.Trim();
        AttemptCount = attemptCount;
    }

    /// <summary>TR: Outbox kimliğini döndürür. EN: Gets Outbox identifier.</summary>
    public Guid Id { get; }
    /// <summary>TR: Message type değerini döndürür. EN: Gets message type.</summary>
    public string MessageType { get; }
    /// <summary>TR: Aggregate kimliğini döndürür. EN: Gets aggregate identifier.</summary>
    public Guid? AggregateId { get; }
    /// <summary>TR: Durable JSON payload'u döndürür. EN: Gets durable JSON payload.</summary>
    public string PayloadJson { get; }
    /// <summary>TR: Correlation kimliğini döndürür. EN: Gets correlation identifier.</summary>
    public string? CorrelationId { get; }
    /// <summary>TR: Deneme sayısını döndürür. EN: Gets attempt count.</summary>
    public int AttemptCount { get; }
}

/// <summary>TR: Transactional Outbox kayıtlarını çoklu instance yarışına dayanıklı biçimde claim/finalize eden MSSQL sınırıdır. EN: MSSQL boundary that claims/finalizes Transactional Outbox records safely under multiple-instance races.</summary>
public interface IOutboxStore
{
    /// <summary>TR: Pending mesajları `UPDLOCK/READPAST` ile atomik claim eder ve AvailableAt değerini lease sonuna taşır; HTTP çağrısı bu SQL transaction kapandıktan sonra yapılır. EN: Atomically claims pending messages using `UPDLOCK/READPAST` and advances AvailableAt to lease expiry; HTTP is executed after this SQL transaction closes.</summary>
    /// <param name="now">TR: Claim UTC zamanı. EN: Claim UTC timestamp.</param>
    /// <param name="leaseUntil">TR: Yeniden claim edilebileceği UTC zaman. EN: UTC time after which the message may be reclaimed.</param>
    /// <param name="take">TR: Batch üst sınırı. EN: Batch upper bound.</param>
    /// <param name="cancellationToken">TR: SQL iptal sinyali. EN: SQL cancellation signal.</param>
    /// <returns>TR: Claim edilmiş mesajları döndürür. EN: Returns claimed messages.</returns>
    Task<IReadOnlyCollection<OutboxMessage>> ClaimPendingAsync(DateTimeOffset now, DateTimeOffset leaseUntil, int take, CancellationToken cancellationToken);

    /// <summary>TR: Başarılı provider tesliminden sonra Outbox mesajını processed olarak finalize eder. EN: Finalizes an Outbox message as processed after successful provider delivery.</summary>
    /// <param name="messageId">TR: Outbox message kimliği. EN: Outbox-message identifier.</param>
    /// <param name="processedAt">TR: Provider başarı UTC zamanı. EN: Provider-success UTC timestamp.</param>
    /// <param name="cancellationToken">TR: SQL iptal sinyali. EN: SQL cancellation signal.</param>
    Task MarkProcessedAsync(Guid messageId, DateTimeOffset processedAt, CancellationToken cancellationToken);

    /// <summary>TR: Provider hatasında mesajı exponential-backoff benzeri yeni AvailableAt ile tekrar kuyruğa alır ve yalnız güvenli hata kodunu saklar. EN: Requeues a provider failure with a backoff AvailableAt and stores only a safe error code.</summary>
    /// <param name="messageId">TR: Outbox message kimliği. EN: Outbox-message identifier.</param>
    /// <param name="availableAt">TR: Sonraki güvenli deneme UTC zamanı. EN: UTC time for the next safe attempt.</param>
    /// <param name="errorCode">TR: Hassas içerik içermeyen kısa hata kodu. EN: Short error code containing no sensitive content.</param>
    /// <param name="cancellationToken">TR: SQL iptal sinyali. EN: SQL cancellation signal.</param>
    Task RescheduleAsync(Guid messageId, DateTimeOffset availableAt, string errorCode, CancellationToken cancellationToken);

    /// <summary>TR: Outbox payload içindeki CustomerId için normalize telefon numarasını server-side customer tablosundan çözer. EN: Resolves the normalized phone number from the server-side customer table for CustomerId carried in the Outbox payload.</summary>
    /// <param name="customerId">TR: Customer kimliği. EN: Customer identifier.</param>
    /// <param name="cancellationToken">TR: SQL sorgu iptal sinyali. EN: SQL-query cancellation signal.</param>
    /// <returns>TR: Normalize telefon numarası veya null döndürür. EN: Returns normalized phone number or null.</returns>
    Task<string?> FindCustomerPhoneAsync(Guid customerId, CancellationToken cancellationToken);
}
