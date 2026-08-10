using FinWallet.Domain.Shared;

namespace FinWallet.Application.Cutoff;

/// <summary>TR: FinWallet'ın dış business-calendar/cutoff provider bağımsız sonucunu taşır. EN: Carries FinWallet's provider-independent external business-calendar/cutoff result.</summary>
public sealed class CutoffEvaluationResult
{
    /// <summary>TR: Cutoff sonucunu oluşturur. EN: Creates the cutoff result.</summary>
    /// <param name="referenceId">TR: Provider değerlendirme referansı. EN: Provider evaluation reference.</param>
    /// <param name="canProcessNow">TR: İşlemin mevcut business day içinde başlayabilmesini belirtir. EN: Indicates whether processing may start on the current business day.</param>
    /// <param name="processingDate">TR: Provider processing business tarihi. EN: Provider processing business date.</param>
    /// <param name="settlementDate">TR: Provider settlement business tarihi. EN: Provider settlement business date.</param>
    /// <param name="reason">TR: Machine-readable cutoff nedeni. EN: Machine-readable cutoff reason.</param>
    public CutoffEvaluationResult(Guid referenceId, bool canProcessNow, DateOnly processingDate, DateOnly settlementDate, string reason)
    {
        if (referenceId == Guid.Empty) throw new ArgumentException("Cutoff reference cannot be empty.", nameof(referenceId));
        if (settlementDate < processingDate) throw new ArgumentException("Settlement date cannot precede processing date.", nameof(settlementDate));
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        ReferenceId = referenceId;
        CanProcessNow = canProcessNow;
        ProcessingDate = processingDate;
        SettlementDate = settlementDate;
        Reason = reason.Trim();
    }

    /// <summary>TR: Provider değerlendirme referansını döndürür. EN: Gets provider evaluation reference.</summary>
    public Guid ReferenceId { get; }
    /// <summary>TR: İşlemin şimdi başlayabilmesini döndürür. EN: Gets whether processing may start now.</summary>
    public bool CanProcessNow { get; }
    /// <summary>TR: Processing business tarihini döndürür. EN: Gets processing business date.</summary>
    public DateOnly ProcessingDate { get; }
    /// <summary>TR: Settlement business tarihini döndürür. EN: Gets settlement business date.</summary>
    public DateOnly SettlementDate { get; }
    /// <summary>TR: Cutoff karar nedenini döndürür. EN: Gets cutoff decision reason.</summary>
    public string Reason { get; }
}

/// <summary>TR: FakeCutoff veya gerçek provider detaylarını Application katmanından ayıran cutoff sınırıdır. EN: Cutoff boundary decoupling the Application layer from FakeCutoff or real-provider details.</summary>
public interface ICutoffProvider
{
    /// <summary>TR: Banka işleminin processing/settlement tarihlerini değerlendirir. EN: Evaluates processing/settlement dates for a bank operation.</summary>
    /// <param name="countryCode">TR: İki harfli business-calendar ülke kodu. EN: Two-letter business-calendar country code.</param>
    /// <param name="currency">TR: İşlem currency'si. EN: Operation currency.</param>
    /// <param name="transactionType">TR: Provider cutoff kuralını seçen transaction türü. EN: Transaction type selecting the provider cutoff rule.</param>
    /// <param name="requestedAt">TR: FinWallet request UTC zamanı. EN: FinWallet request UTC timestamp.</param>
    /// <param name="correlationId">TR: Dağıtık izleme kimliği. EN: Distributed tracing identifier.</param>
    /// <param name="cancellationToken">TR: HTTP çağrısı iptal sinyali. EN: HTTP-call cancellation signal.</param>
    /// <returns>TR: Provider bağımsız cutoff sonucunu döndürür. EN: Returns provider-independent cutoff result.</returns>
    Task<CutoffEvaluationResult> EvaluateAsync(string countryCode, CurrencyCode currency, string transactionType, DateTimeOffset requestedAt, string correlationId, CancellationToken cancellationToken);
}

/// <summary>TR: Cutoff provider erişim veya sözleşme hatasını güvenli application exception olarak temsil eder. EN: Represents cutoff-provider access or contract failure as a safe application exception.</summary>
public sealed class CutoffProviderException : Exception
{
    /// <summary>TR: Güvenli provider hata kodu ile exception oluşturur. EN: Creates the exception with a safe provider error code.</summary>
    /// <param name="code">TR: Machine-readable hata kodu. EN: Machine-readable error code.</param>
    /// <param name="message">TR: Güvenli hata açıklaması. EN: Safe error description.</param>
    /// <param name="innerException">TR: İsteğe bağlı teknik kök neden. EN: Optional technical root cause.</param>
    public CutoffProviderException(string code, string message, Exception? innerException = null) : base(message, innerException)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        Code = code.Trim();
    }

    /// <summary>TR: Machine-readable hata kodunu döndürür. EN: Gets machine-readable error code.</summary>
    public string Code { get; }
}
