using FinWallet.Application.Transactions;
using FinWallet.Domain.Transactions;
using FinWallet.Shared.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinWallet.Api.Controllers;

/// <summary>TR: Authenticated customer'ın yalnız kendi financial transaction read-history'sini public Web API olarak sunar. EN: Exposes only the authenticated customer's own financial-transaction read history as public Web API.</summary>
[ApiController]
[Authorize]
[Route("api/v1/transactions")]
public sealed class TransactionHistoryController : ControllerBase
{
    private readonly ListTransactionHistoryHandler _handler;

    /// <summary>TR: History query handler bağımlılığıyla controller'ı oluşturur. EN: Creates controller with history-query-handler dependency.</summary>
    /// <param name="handler">TR: Customer-owned transaction history handler. EN: Customer-owned transaction-history handler.</param>
    public TransactionHistoryController(ListTransactionHistoryHandler handler) => _handler = handler ?? throw new ArgumentNullException(nameof(handler));

    /// <summary>TR: Transaction history'yi newest-first keyset pagination ile listeler; `beforeTransactionId` önceki sayfanın son kaydıdır. EN: Lists transaction history newest-first using keyset pagination; `beforeTransactionId` is the last row from the previous page.</summary>
    /// <param name="take">TR: 1–100 arası page size. EN: Page size between 1 and 100.</param>
    /// <param name="beforeTransactionId">TR: Opsiyonel keyset cursor transaction kimliği. EN: Optional keyset-cursor transaction identifier.</param>
    /// <param name="cancellationToken">TR: MSSQL query iptal sinyali. EN: MSSQL-query cancellation signal.</param>
    /// <returns>TR: Customer transaction read-model sayfasını döndürür. EN: Returns customer transaction read-model page.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(ServiceResult<IReadOnlyCollection<TransactionHistoryResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ServiceResult<object>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ServiceResult<IReadOnlyCollection<TransactionHistoryResponse>>>> ListAsync([FromQuery] int take = 50, [FromQuery] Guid? beforeTransactionId = null, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParseExact(User.FindFirst("sub")?.Value, "N", out var customerId))
        {
            return Unauthorized(ServiceResult<IReadOnlyCollection<TransactionHistoryResponse>>.Failure("INVALID_ACCESS_TOKEN", "The access token customer identity is invalid."));
        }

        var items = await _handler.HandleAsync(customerId, beforeTransactionId, take, cancellationToken);
        IReadOnlyCollection<TransactionHistoryResponse> response = items.Select(static item => new TransactionHistoryResponse(item)).ToArray();
        return Ok(ServiceResult<IReadOnlyCollection<TransactionHistoryResponse>>.Success(response, "TRANSACTION_HISTORY_LISTED", "Transaction history listed successfully."));
    }
}

/// <summary>TR: Customer-facing transaction history response modelidir; raw ledger entries veya secret/PII alanları içermez. EN: Customer-facing transaction-history response model; it contains no raw ledger entries, secrets or PII.</summary>
public sealed class TransactionHistoryResponse
{
    /// <summary>TR: Application history item'ını API read-model'e dönüştürür. EN: Converts Application history item into API read model.</summary>
    /// <param name="item">TR: Customer-owned history row. EN: Customer-owned history row.</param>
    public TransactionHistoryResponse(TransactionHistoryItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        TransactionId = item.TransactionId;
        Type = item.Type.ToString();
        Status = Enum.IsDefined(typeof(FinancialTransactionStatus), (int)item.Status) ? ((FinancialTransactionStatus)item.Status).ToString() : $"Unknown:{item.Status}";
        SourceWalletId = item.SourceWalletId;
        DestinationWalletId = item.DestinationWalletId;
        Amount = item.Amount.Amount;
        Currency = item.Amount.Currency.ToString();
        CreatedAt = item.CreatedAt;
        FinalizedAt = item.FinalizedAt;
        ReversedAt = item.ReversedAt;
        FailureCode = item.FailureCode;
        ParentTransactionId = item.ParentTransactionId;
        BankAccountId = item.BankAccountId;
        ExternalTransactionId = item.ExternalTransactionId;
        MerchantId = item.MerchantId;
        OriginalAmount = item.OriginalAmount;
        DiscountAmount = item.DiscountAmount;
        ProcessingDate = item.ProcessingDate;
        SettlementDate = item.SettlementDate;
    }

    /// <summary>TR: Transaction kimliğini döndürür. EN: Gets transaction identifier.</summary>
    public Guid TransactionId { get; }
    /// <summary>TR: Transaction type adını döndürür. EN: Gets transaction-type name.</summary>
    public string Type { get; }
    /// <summary>TR: Transaction lifecycle state adını döndürür. EN: Gets transaction lifecycle-state name.</summary>
    public string Status { get; }
    /// <summary>TR: Source wallet kimliğini döndürür. EN: Gets source-wallet identifier.</summary>
    public Guid? SourceWalletId { get; }
    /// <summary>TR: Destination wallet kimliğini döndürür. EN: Gets destination-wallet identifier.</summary>
    public Guid? DestinationWalletId { get; }
    /// <summary>TR: Transaction tutarını döndürür. EN: Gets transaction amount.</summary>
    public decimal Amount { get; }
    /// <summary>TR: Currency kodunu döndürür. EN: Gets currency code.</summary>
    public string Currency { get; }
    /// <summary>TR: Created UTC zamanını döndürür. EN: Gets creation UTC timestamp.</summary>
    public DateTimeOffset CreatedAt { get; }
    /// <summary>TR: Finalized UTC zamanını döndürür. EN: Gets finalization UTC timestamp.</summary>
    public DateTimeOffset? FinalizedAt { get; }
    /// <summary>TR: Reversed UTC zamanını döndürür. EN: Gets reversal UTC timestamp.</summary>
    public DateTimeOffset? ReversedAt { get; }
    /// <summary>TR: Güvenli failure code değerini döndürür. EN: Gets safe failure code.</summary>
    public string? FailureCode { get; }
    /// <summary>TR: Parent transaction kimliğini döndürür. EN: Gets parent-transaction identifier.</summary>
    public Guid? ParentTransactionId { get; }
    /// <summary>TR: BankAccount kimliğini döndürür. EN: Gets BankAccount identifier.</summary>
    public Guid? BankAccountId { get; }
    /// <summary>TR: External transaction kimliğini döndürür. EN: Gets external-transaction identifier.</summary>
    public Guid? ExternalTransactionId { get; }
    /// <summary>TR: Merchant kimliğini döndürür. EN: Gets merchant identifier.</summary>
    public string? MerchantId { get; }
    /// <summary>TR: Campaign öncesi original amount değerini döndürür. EN: Gets original amount before campaign.</summary>
    public decimal? OriginalAmount { get; }
    /// <summary>TR: Campaign discount tutarını döndürür. EN: Gets campaign-discount amount.</summary>
    public decimal? DiscountAmount { get; }
    /// <summary>TR: Bank processing tarihini döndürür. EN: Gets bank-processing date.</summary>
    public DateOnly? ProcessingDate { get; }
    /// <summary>TR: Bank settlement tarihini döndürür. EN: Gets bank-settlement date.</summary>
    public DateOnly? SettlementDate { get; }
}
