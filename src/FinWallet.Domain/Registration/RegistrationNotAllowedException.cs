namespace FinWallet.Domain.Registration;

/// <summary>
/// TR: Müşteri kaydının ülke allow-list'i veya ülke/telefon eşleşmesi nedeniyle kabul edilemediğini ifade eden domain hatasını temsil eder.
/// EN: Represents the domain error indicating that customer registration cannot be accepted because of the country allow-list or country/phone mismatch.
/// </summary>
public sealed class RegistrationNotAllowedException : InvalidOperationException
{
    /// <summary>
    /// TR: Kayıt reddinin iş kuralı nedenini taşıyan hatayı oluşturur.
    /// EN: Creates the error carrying the business-rule reason for registration rejection.
    /// </summary>
    /// <param name="message">
    /// TR: Kayıt reddinin teknik olmayan iş kuralı açıklaması.
    /// EN: Business-rule description of the registration rejection.
    /// </param>
    public RegistrationNotAllowedException(string message)
        : base(message)
    {
    }
}
