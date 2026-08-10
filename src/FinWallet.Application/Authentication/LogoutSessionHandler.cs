namespace FinWallet.Application.Authentication;

/// <summary>
/// TR: Authenticated müşterinin mevcut durable session'ını revoke ederek logout işlemini tamamlar.
/// EN: Completes logout by revoking the authenticated customer's current durable session.
/// </summary>
public sealed class LogoutSessionHandler
{
    private readonly IAuthenticationStore _authenticationStore;
    private readonly TimeProvider _timeProvider;

    /// <summary>TR: Authentication store ve UTC zaman kaynağıyla logout handler'ını oluşturur. EN: Creates the logout handler with the authentication store and UTC time source.</summary>
    /// <param name="authenticationStore">TR: Durable session revoke persistence sınırı. EN: Durable session-revocation persistence boundary.</param>
    /// <param name="timeProvider">TR: Revoke UTC zamanı için test edilebilir zaman kaynağı. EN: Testable time source for the revoke UTC timestamp.</param>
    public LogoutSessionHandler(IAuthenticationStore authenticationStore, TimeProvider timeProvider)
    {
        _authenticationStore = authenticationStore ?? throw new ArgumentNullException(nameof(authenticationStore));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <summary>TR: Belirtilen session'ı durable olarak revoke eder. EN: Durably revokes the specified session.</summary>
    /// <param name="sessionId">TR: JWT `sid` claim'inden gelen session kimliği. EN: Session identifier from the JWT `sid` claim.</param>
    /// <param name="cancellationToken">TR: MSSQL revoke işleminin iptal sinyali. EN: Cancellation signal for the MSSQL revocation operation.</param>
    public Task HandleAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        if (sessionId == Guid.Empty) throw new ArgumentException("Session identifier cannot be empty.", nameof(sessionId));
        return _authenticationStore.RevokeSessionAsync(sessionId, _timeProvider.GetUtcNow(), cancellationToken);
    }
}
