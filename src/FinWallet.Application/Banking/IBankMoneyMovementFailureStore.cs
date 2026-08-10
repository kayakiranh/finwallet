namespace FinWallet.Application.Banking;

/// <summary>
/// TR: Provider çağrısı kalıcı olarak reddedildiğinde pending banka hareketini güvenli terminal Failed state'e geçirip varsa bloke fonu serbest bırakan persistence sınırıdır.
/// EN: Persistence boundary that moves a pending bank movement into a safe terminal Failed state and releases any blocked funds when the provider permanently rejects the operation.
/// </summary>
public interface IBankMoneyMovementFailureStore
{
    /// <summary>
    /// TR: Retry edilmeyecek provider hatasını atomik olarak kaydeder, withdrawal blokajını açar, idempotency state'ini finalize eder ve Outbox kaydı oluşturur.
    /// EN: Atomically records a non-retryable provider failure, releases withdrawal reservation, finalizes idempotency state and creates an Outbox record.
    /// </summary>
    /// <param name="transactionId">TR: Failed yapılacak internal FinancialTransaction kimliği. EN: Internal FinancialTransaction identifier to fail.</param>
    /// <param name="failureCode">TR: Hassas detay içermeyen stabil provider/adapter hata kodu. EN: Stable provider/adapter failure code without sensitive details.</param>
    /// <param name="cancellationToken">TR: MSSQL transaction iptal sinyali. EN: MSSQL-transaction cancellation signal.</param>
    /// <returns>TR: Güncel durable failed bank movement sonucunu döndürür. EN: Returns current durable failed bank-movement result.</returns>
    Task<BankMoneyMovementResult> FailAsync(Guid transactionId, string failureCode, CancellationToken cancellationToken);
}
