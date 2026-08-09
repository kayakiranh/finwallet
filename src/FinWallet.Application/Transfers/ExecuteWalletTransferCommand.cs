namespace FinWallet.Application.Transfers;

/// <summary>
/// TR: Authenticated session tarafından başlatılan wallet transfer için identity, wallet, amount, idempotency ve correlation bilgisini Application katmanına taşır.
/// EN: Carries identity, wallet, amount, idempotency and correlation information into the Application layer for a wallet transfer initiated by an authenticated session.
/// </summary>
public sealed class ExecuteWalletTransferCommand
{
    /// <summary>TR: Execute-wallet-transfer command'ını oluşturur. EN: Creates the execute-wallet-transfer command.</summary>
    /// <param name="customerId">TR: JWT subject customer kimliği. EN: JWT-subject customer identifier.</param>
    /// <param name="sessionId">TR: JWT sid server-side session kimliği. EN: JWT-sid server-side session identifier.</param>
    /// <param name="sourceWalletId">TR: Source wallet kimliği. EN: Source-wallet identifier.</param>
    /// <param name="destinationWalletId">TR: Destination wallet kimliği. EN: Destination-wallet identifier.</param>
    /// <param name="amount">TR: Pozitif transfer tutarı. EN: Positive transfer amount.</param>
    /// <param name="idempotencyKey">TR: Durable transfer idempotency anahtarı. EN: Durable transfer idempotency key.</param>
    /// <param name="correlationId">TR: External fraud çağrısına propagate edilecek correlation kimliği. EN: Correlation identifier propagated to the external fraud call.</param>
    public ExecuteWalletTransferCommand(
        Guid customerId,
        Guid sessionId,
        Guid sourceWalletId,
        Guid destinationWalletId,
        decimal amount,
        string idempotencyKey,
        string correlationId)
    {
        if (sessionId == Guid.Empty) throw new ArgumentException("Session identifier cannot be empty.", nameof(sessionId));
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        PostingRequest = new WalletTransferPostingRequest(customerId, sourceWalletId, destinationWalletId, amount, idempotencyKey);
        SessionId = sessionId;
        CorrelationId = correlationId.Trim();
    }

    /// <summary>TR: Atomic posting için normalize/validated transfer request'ini döndürür. EN: Gets normalized/validated transfer request used for atomic posting.</summary>
    public WalletTransferPostingRequest PostingRequest { get; }

    /// <summary>TR: Server-side session kimliğini döndürür. EN: Gets server-side session identifier.</summary>
    public Guid SessionId { get; }

    /// <summary>TR: External fraud çağrısına propagate edilecek correlation kimliğini döndürür. EN: Gets correlation identifier propagated to the external fraud call.</summary>
    public string CorrelationId { get; }
}
