namespace FinWallet.Domain.Fraud;

/// <summary>
/// TR: Internal ve external fraud kararlarını Deny > Review > Allow önceliğiyle tek nihai finansal risk kararına birleştiren provider bağımsız domain policy'sidir.
/// EN: Provider-independent domain policy that combines internal and external fraud decisions into one final financial-risk decision using Deny > Review > Allow precedence.
/// </summary>
public sealed class FraudDecisionPolicy
{
    /// <summary>
    /// TR: Internal ve external kararları güvenli öncelik sırasına göre birleştirir; hiçbir Allow kararı karşı taraftaki Deny kararını override edemez.
    /// EN: Combines internal and external decisions using safe precedence so an Allow decision can never override a Deny from the other source.
    /// </summary>
    /// <param name="internalDecision">TR: FinWallet internal fraud engine kararı. EN: Decision produced by the FinWallet internal fraud engine.</param>
    /// <param name="externalDecision">TR: Normalize edilmiş dış fraud provider kararı. EN: Normalized decision produced by the external fraud provider.</param>
    /// <returns>TR: Nihai Allow, Review veya Deny fraud kararını döndürür. EN: Returns the final Allow, Review or Deny fraud decision.</returns>
    public FraudDecision Combine(FraudDecision internalDecision, FraudDecision externalDecision)
    {
        if (internalDecision == FraudDecision.Deny || externalDecision == FraudDecision.Deny)
        {
            return FraudDecision.Deny;
        }

        if (internalDecision == FraudDecision.Review || externalDecision == FraudDecision.Review)
        {
            return FraudDecision.Review;
        }

        return FraudDecision.Allow;
    }
}
