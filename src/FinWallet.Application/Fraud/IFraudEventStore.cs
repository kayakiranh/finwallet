namespace FinWallet.Application.Fraud;

/// <summary>
/// TR: Finansal posting öncesi fraud değerlendirme audit kayıtlarını durable storage'a append eden persistence sınırını tanımlar.
/// EN: Defines the persistence boundary that appends fraud-evaluation audit records to durable storage before financial posting.
/// </summary>
public interface IFraudEventStore
{
    /// <summary>TR: Fraud evaluation audit kaydını durable olarak append eder. EN: Durably appends a fraud-evaluation audit record.</summary>
    /// <param name="record">TR: Append edilecek PII-free fraud audit snapshot'ı. EN: PII-free fraud-audit snapshot to append.</param>
    /// <param name="cancellationToken">TR: MSSQL insert işlemine yayılan iptal sinyali. EN: Cancellation signal propagated to the MSSQL insert.</param>
    Task InsertAsync(FraudEvaluationAuditRecord record, CancellationToken cancellationToken);
}
