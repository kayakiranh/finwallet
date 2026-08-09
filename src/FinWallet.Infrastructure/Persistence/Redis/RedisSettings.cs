namespace FinWallet.Infrastructure.Persistence.Redis;

/// <summary>
/// TR: Redis bağlantısı ve registration OTP HMAC pepper secret'ı için deployment seviyesinde sağlanması gereken altyapı değerlerini taşır; OTP güvenlik algoritması ve süreleri bu ayarlardan seçilemez.
/// EN: Carries deployment-level infrastructure values required for the Redis connection and registration-OTP HMAC pepper secret; OTP security algorithms and lifetimes cannot be selected through these settings.
/// </summary>
public sealed class RedisSettings
{
    /// <summary>
    /// TR: Redis sunucusuna bağlanmak için kullanılan connection string'i döndürür veya ayarlar; secret/config provider üzerinden sağlanmalıdır.
    /// EN: Gets or sets the connection string used to connect to Redis; it must be supplied through a secret/configuration provider.
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// TR: Düşük entropili OTP kodlarının Redis dump'ından offline brute-force edilmesini zorlaştırmak için HMAC-SHA256 anahtarı olarak kullanılan Base64 pepper secret'ı döndürür veya ayarlar; en az 32 byte decode edilmelidir.
    /// EN: Gets or sets the Base64 pepper secret used as the HMAC-SHA256 key to make offline brute-forcing low-entropy OTP codes from a Redis dump harder; it must decode to at least 32 bytes.
    /// </summary>
    public string RegistrationOtpPepperBase64 { get; set; } = string.Empty;
}
