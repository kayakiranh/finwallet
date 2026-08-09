namespace FinWallet.Application.Registration;

/// <summary>
/// TR: Yeni kayıt talebinin mevcut müşteri verisiyle çakışması nedeniyle işlenemediğini ifade eden Application hatasını temsil eder.
/// EN: Represents an Application error indicating that a new registration request cannot be processed because it conflicts with existing customer data.
/// </summary>
public sealed class RegistrationConflictException : InvalidOperationException
{
    /// <summary>
    /// TR: Registration conflict hatasını güvenli istemci mesajıyla oluşturur.
    /// EN: Creates the registration-conflict error with a safe client-facing message.
    /// </summary>
    /// <param name="message">
    /// TR: Conflict nedenini hassas veri sızdırmadan açıklayan mesaj.
    /// EN: Message describing the conflict without leaking sensitive data.
    /// </param>
    public RegistrationConflictException(string message)
        : base(message)
    {
    }
}
