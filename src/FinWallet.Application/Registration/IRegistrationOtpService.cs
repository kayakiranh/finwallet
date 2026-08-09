namespace FinWallet.Application.Registration;

/// <summary>
/// TR: Registration handler'larını Redis ve kriptografik OTP saklama detaylarından ayıran OTP oluşturma, doğrulama ve tek kullanımlık tüketim sınırını tanımlar.
/// EN: Defines the OTP issuance, verification and single-use consumption boundary that decouples registration handlers from Redis and cryptographic OTP-storage details.
/// </summary>
public interface IRegistrationOtpService
{
    /// <summary>
    /// TR: Belirli pending customer için yeni OTP challenge oluşturur; eski aktif challenge varsa implementasyon tarafından geçersiz kılınmalıdır.
    /// EN: Creates a new OTP challenge for a specific pending customer; any older active challenge must be invalidated by the implementation.
    /// </summary>
    /// <param name="customerId">
    /// TR: OTP challenge'ın ait olduğu pending müşteri kimliği.
    /// EN: Pending customer identifier to which the OTP challenge belongs.
    /// </param>
    /// <param name="cancellationToken">
    /// TR: OTP üretim/saklama işleminin iptal sinyali.
    /// EN: Cancellation signal for OTP issuance/storage.
    /// </param>
    /// <returns>
    /// TR: Yalnızca SMS gönderiminde kullanılacak ham kod ve sona erme bilgisini döndürür.
    /// EN: Returns the raw code used only for SMS delivery and its expiration.
    /// </returns>
    Task<RegistrationOtpIssueResult> IssueAsync(Guid customerId, CancellationToken cancellationToken);

    /// <summary>
    /// TR: Kullanıcının gönderdiği OTP kodunu doğrular ve başarılıysa challenge'ı atomik olarak tüketerek tekrar kullanımını engeller.
    /// EN: Verifies the OTP code submitted by the user and atomically consumes the challenge on success to prevent replay.
    /// </summary>
    /// <param name="customerId">
    /// TR: OTP challenge'ın bağlı olduğu müşteri kimliği.
    /// EN: Customer identifier associated with the OTP challenge.
    /// </param>
    /// <param name="code">
    /// TR: Kullanıcının girdiği ham OTP kodu; loglanmamalıdır.
    /// EN: Raw OTP code entered by the user; it must not be logged.
    /// </param>
    /// <param name="cancellationToken">
    /// TR: OTP doğrulama/tüketme işleminin iptal sinyali.
    /// EN: Cancellation signal for OTP verification/consumption.
    /// </param>
    /// <returns>
    /// TR: OTP doğru, süresi geçmemiş ve daha önce kullanılmamışsa true döndürür.
    /// EN: Returns true when the OTP is correct, unexpired and has not previously been used.
    /// </returns>
    Task<bool> VerifyAndConsumeAsync(Guid customerId, string code, CancellationToken cancellationToken);
}
