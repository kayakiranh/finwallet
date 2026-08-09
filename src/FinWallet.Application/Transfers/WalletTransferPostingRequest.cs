using FinWallet.Domain.Shared;

namespace FinWallet.Application.Transfers;

/// <summary>
/// TR: Authenticated customer'ın iki internal wallet arasında atomik ve durable idempotent transfer posting isteğini taşır.
/// EN: Carries an authenticated customer's request for atomic, durably idempotent posting between two internal wallets.
/// </summary>
public sealed class WalletTransferPostingRequest
{
    /// <summary>TR: Wallet transfer posting isteğini oluşturur. EN: Creates a wallet-transfer posting request.</summary>
    /// <param name="customerId">TR: Source wallet'ın authenticated owner customer kimliği. EN: Authenticated owner-customer identifier of the source wallet.</param>
    /// <param name="sourceWalletId">TR: Para çıkacak internal wallet kimliği. EN: Internal wallet identifier to debit.</param>
    /// <param name="destinationWalletId">TR: Para girecek internal wallet kimliği. EN: Internal wallet identifier to credit.</param>
    /// <param name="amount">TR: Pozitif ve `DECIMAL(19,4)` uyumlu transfer tutarı; currency source wallet'tan belirlenir. EN: Positive `DECIMAL(19,4)` compatible transfer amount; currency is determined from the source wallet.</param>
    /// <param name="idempotencyKey">TR: Customer/scope içinde benzersiz durable idempotency anahtarı. EN: Durable idempotency key unique within customer/scope.</param>
    public WalletTransferPostingRequest(Guid customerId, Guid sourceWalletId, Guid destinationWalletId, decimal amount, string idempotencyKey)
    {
        if (customerId == Guid.Empty) throw new ArgumentException("Customer identifier cannot be empty.", nameof(customerId));
        if (sourceWalletId == Guid.Empty) throw new ArgumentException("Source wallet identifier cannot be empty.", nameof(sourceWalletId));
        if (destinationWalletId == Guid.Empty) throw new ArgumentException("Destination wallet identifier cannot be empty.", nameof(destinationWalletId));
        if (sourceWalletId == destinationWalletId) throw new ArgumentException("Source and destination wallets must differ.");
        if (amount <= 0m) throw new ArgumentOutOfRangeException(nameof(amount));
        FinancialAmountRules.EnsureStorageCompatible(amount, nameof(amount));
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        var normalizedKey = idempotencyKey.Trim();
        if (normalizedKey.Length > 128) throw new ArgumentOutOfRangeException(nameof(idempotencyKey), "Idempotency key cannot exceed 128 characters.");

        CustomerId = customerId;
        SourceWalletId = sourceWalletId;
        DestinationWalletId = destinationWalletId;
        Amount = amount;
        IdempotencyKey = normalizedKey;
    }

    /// <summary>TR: Source wallet owner customer kimliğini döndürür. EN: Gets source-wallet owner customer identifier.</summary>
    public Guid CustomerId { get; }

    /// <summary>TR: Source wallet kimliğini döndürür. EN: Gets source-wallet identifier.</summary>
    public Guid SourceWalletId { get; }

    /// <summary>TR: Destination wallet kimliğini döndürür. EN: Gets destination-wallet identifier.</summary>
    public Guid DestinationWalletId { get; }

    /// <summary>TR: Pozitif transfer tutarını döndürür. EN: Gets positive transfer amount.</summary>
    public decimal Amount { get; }

    /// <summary>TR: Durable idempotency anahtarını döndürür. EN: Gets durable idempotency key.</summary>
    public string IdempotencyKey { get; }
}
