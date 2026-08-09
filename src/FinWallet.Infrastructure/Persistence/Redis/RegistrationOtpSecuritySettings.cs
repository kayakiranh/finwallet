using System.Text;

namespace FinWallet.Infrastructure.Persistence.Redis;

/// <summary>
/// TR: Registration OTP digest'lerini Redis sızıntısında offline brute-force'a karşı güçlendirmek için deployment secret store'dan sağlanan HMAC pepper değerini taşır; OTP algoritması/TTL/deneme limitlerini yapılandırmaz.
/// EN: Carries the HMAC pepper supplied by the deployment secret store to strengthen registration OTP digests against offline brute force after Redis exposure; it does not configure OTP algorithm, TTL or attempt limits.
/// </summary>
public sealed class RegistrationOtpSecuritySettings
{
    /// <summary>
    /// TR: En az 32 UTF-8 byte uzunluğundaki deployment pepper secret ile OTP güvenlik ayarını oluşturur.
    /// EN: Creates OTP security settings using a deployment pepper secret containing at least 32 UTF-8 bytes.
    /// </summary>
    /// <param name="pepper">
    /// TR: Secret store veya environment üzerinden sağlanan HMAC pepper; source code veya loglarda bulunmamalıdır.
    /// EN: HMAC pepper supplied through a secret store or environment; it must not appear in source code or logs.
    /// </param>
    /// <exception cref="ArgumentException">
    /// TR: Pepper boşsa veya 32 UTF-8 byte'tan kısa ise oluşur.
    /// EN: Thrown when the pepper is empty or shorter than 32 UTF-8 bytes.
    /// </exception>
    public RegistrationOtpSecuritySettings(string pepper)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pepper);

        if (Encoding.UTF8.GetByteCount(pepper) < 32)
        {
            throw new ArgumentException("Registration OTP pepper must contain at least 32 UTF-8 bytes.", nameof(pepper));
        }

        Pepper = pepper;
    }

    /// <summary>
    /// TR: OTP HMAC digest üretiminde kullanılan deployment pepper secret değerini döndürür; loglanmamalıdır.
    /// EN: Gets the deployment pepper secret used for OTP HMAC-digest generation; it must not be logged.
    /// </summary>
    public string Pepper { get; }
}
