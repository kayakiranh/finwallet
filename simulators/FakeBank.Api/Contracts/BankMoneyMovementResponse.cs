using FakeBank.Api.Models;

namespace FakeBank.Api.Contracts;

/// <summary>
/// TR: FakeBank deposit/withdrawal başlatma sonucunda provider transaction kimliği, mevcut durum ve provider bakiyesini döndürür.
/// EN: Returns provider transaction identifier, current state and provider balance after initiating a FakeBank deposit/withdrawal.
/// </summary>
public sealed class BankMoneyMovementResponse
{
    /// <summary>TR: Para hareketi yanıtını oluşturur. EN: Creates money-movement response.</summary>
    /// <param name="transactionId">TR: Provider transaction kimliği. EN: Provider transaction identifier.</param>
    /// <param name="status">TR: Provider transaction durumu. EN: Provider transaction state.</param>
    /// <param name="accountBalance">TR: Provider hesabın yanıt anındaki bakiyesi. EN: Provider account balance at response time.</param>
    public BankMoneyMovementResponse(Guid transactionId, FakeBankTransactionStatus status, decimal accountBalance)
    {
        TransactionId = transactionId;
        Status = status;
        AccountBalance = accountBalance;
    }

    /// <summary>TR: Provider transaction kimliğini döndürür. EN: Gets provider transaction identifier.</summary>
    public Guid TransactionId { get; }

    /// <summary>TR: Provider transaction durumunu döndürür. EN: Gets provider transaction state.</summary>
    public FakeBankTransactionStatus Status { get; }

    /// <summary>TR: Provider hesap bakiyesini döndürür. EN: Gets provider account balance.</summary>
    public decimal AccountBalance { get; }
}
