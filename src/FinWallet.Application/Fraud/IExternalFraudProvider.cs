namespace FinWallet.Application.Fraud;

/// <summary>
/// TR: FinWallet use-case'lerini belirli fraud sağlayıcısının HTTP/DTO detaylarından ayıran dış fraud değerlendirme sınırını tanımlar.
/// EN: Defines the external fraud-evaluation boundary that decouples FinWallet use cases from HTTP/DTO details of a specific fraud provider.
/// </summary>
public interface IExternalFraudProvider
{
    /// <summary>
    /// TR: PII içermeyen işlem risk context'ini dış provider'a değerlendirir ve normalize kararı döndürür.
    /// EN: Evaluates the PII-free transaction risk context through the external provider and returns a normalized decision.
    /// </summary>
    /// <param name="context">TR: Provider'a gönderilecek PII içermeyen fraud context'i. EN: PII-free fraud context sent to the provider.</param>
    /// <param name="cancellationToken">TR: Dış HTTP çağrısına taşınacak iptal sinyali. EN: Cancellation signal propagated to the external HTTP call.</param>
    /// <returns>TR: Provider transport detaylarından arındırılmış dış fraud sonucunu döndürür. EN: Returns the external fraud result stripped of provider transport details.</returns>
    Task<ExternalFraudEvaluationResult> EvaluateAsync(
        ExternalFraudEvaluationContext context,
        CancellationToken cancellationToken);
}
