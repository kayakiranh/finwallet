namespace FinWallet.Application.Transfers;

/// <summary>
/// TR: Aynı customer/scope/idempotency key'in farklı transfer payload'ıyla tekrar kullanıldığını belirtir.
/// EN: Indicates that the same customer/scope/idempotency key was reused with a different transfer payload.
/// </summary>
public sealed class WalletTransferIdempotencyConflictException : Exception
{
    /// <summary>TR: Güvenli idempotency conflict hatasını oluşturur. EN: Creates the safe idempotency-conflict failure.</summary>
    public WalletTransferIdempotencyConflictException()
        : base("The idempotency key was already used with a different request.")
    {
    }
}
