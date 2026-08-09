using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using FinWallet.Application.Registration;
using StackExchange.Redis;

namespace FinWallet.Infrastructure.Persistence.Redis;

/// <summary>
/// TR: Registration OTP challenge'larını Redis'te yalnızca HMAC digest olarak saklayan; resend cooldown, TTL, deneme limiti ve atomik verify-and-consume davranışını uygulayan servistir.
/// EN: Service that stores registration OTP challenges in Redis only as HMAC digests and enforces resend cooldown, TTL, attempt limits and atomic verify-and-consume behavior.
/// </summary>
public sealed class RedisRegistrationOtpService : IRegistrationOtpService
{
    /// <summary>
    /// TR: Kullanıcıya gönderilen numeric OTP'nin sabit rakam sayısını tanımlar.
    /// EN: Defines the fixed number of digits in the numeric OTP sent to the user.
    /// </summary>
    private const int OtpDigits = 6;

    /// <summary>
    /// TR: Tek OTP challenge için sabit maksimum yanlış doğrulama deneme sayısını tanımlar.
    /// EN: Defines the fixed maximum number of failed verification attempts for one OTP challenge.
    /// </summary>
    private const int MaximumAttempts = 5;

    /// <summary>
    /// TR: OTP challenge'ın sabit yaşam süresini tanımlar; runtime config ile uzatılamaz.
    /// EN: Defines the fixed OTP challenge lifetime; it cannot be extended through runtime configuration.
    /// </summary>
    private static readonly TimeSpan OtpLifetime = TimeSpan.FromMinutes(5);

    /// <summary>
    /// TR: Aynı registration için yeni OTP üretmeden önce beklenmesi gereken sabit resend cooldown süresini tanımlar.
    /// EN: Defines the fixed resend-cooldown period required before another OTP may be issued for the same registration.
    /// </summary>
    private static readonly TimeSpan ResendCooldown = TimeSpan.FromSeconds(30);

    private const string IssueScript = """
        if redis.call('EXISTS', KEYS[2]) == 1 then
            return 0
        end

        redis.call('SET', KEYS[2], '1', 'PX', ARGV[3])
        redis.call('HSET', KEYS[1], 'digest', ARGV[1], 'attempts', '0')
        redis.call('PEXPIRE', KEYS[1], ARGV[2])
        return 1
        """;

    private const string VerifyScript = """
        local digest = redis.call('HGET', KEYS[1], 'digest')
        if not digest then
            return -1
        end

        local attempts = tonumber(redis.call('HGET', KEYS[1], 'attempts') or '0')
        local maximumAttempts = tonumber(ARGV[2])

        if attempts >= maximumAttempts then
            redis.call('DEL', KEYS[1])
            return -2
        end

        if digest == ARGV[1] then
            redis.call('DEL', KEYS[1])
            return 1
        end

        attempts = redis.call('HINCRBY', KEYS[1], 'attempts', 1)
        if attempts >= maximumAttempts then
            redis.call('DEL', KEYS[1])
        end

        return 0
        """;

    private readonly IDatabase _database;
    private readonly byte[] _pepperBytes;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// TR: Redis multiplexer, deployment OTP pepper secret ve test edilebilir zaman kaynağıyla registration OTP servisini oluşturur.
    /// EN: Creates the registration OTP service using a Redis multiplexer, deployment OTP pepper secret and a testable time source.
    /// </summary>
    /// <param name="connectionMultiplexer">TR: Paylaşımlı ve thread-safe Redis connection multiplexer. EN: Shared thread-safe Redis connection multiplexer.</param>
    /// <param name="securitySettings">TR: HMAC pepper secret değerini taşıyan OTP security ayarları. EN: OTP security settings carrying the HMAC pepper secret.</param>
    /// <param name="timeProvider">TR: OTP expiration sonucunu üretmek için kullanılan test edilebilir UTC zaman kaynağı. EN: Testable UTC time source used to produce the OTP expiration result.</param>
    public RedisRegistrationOtpService(
        IConnectionMultiplexer connectionMultiplexer,
        RegistrationOtpSecuritySettings securitySettings,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(connectionMultiplexer);
        ArgumentNullException.ThrowIfNull(securitySettings);

        _database = connectionMultiplexer.GetDatabase();
        _pepperBytes = Encoding.UTF8.GetBytes(securitySettings.Pepper);
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <summary>
    /// TR: Kriptografik rastgele altı haneli OTP üretir, yalnızca customer-bound HMAC digest'ini Redis'e yazar, eski challenge'ı değiştirir ve 30 saniyelik resend cooldown uygular.
    /// EN: Generates a cryptographically random six-digit OTP, writes only its customer-bound HMAC digest to Redis, replaces the previous challenge and applies a thirty-second resend cooldown.
    /// </summary>
    /// <param name="customerId">TR: OTP challenge'ın bağlı olduğu pending müşteri kimliği. EN: Pending customer identifier associated with the OTP challenge.</param>
    /// <param name="cancellationToken">TR: Redis işlemi beklenirken kullanılacak iptal sinyali. EN: Cancellation signal used while awaiting the Redis operation.</param>
    /// <returns>TR: Yalnızca SMS gönderiminde kullanılacak ham OTP kodu ve sona erme zamanını döndürür. EN: Returns the raw OTP code used only for SMS delivery and its expiration time.</returns>
    /// <exception cref="RegistrationOtpRateLimitException">TR: Resend cooldown henüz dolmadıysa oluşur. EN: Thrown when the resend cooldown has not elapsed.</exception>
    public async Task<RegistrationOtpIssueResult> IssueAsync(
        Guid customerId,
        CancellationToken cancellationToken)
    {
        if (customerId == Guid.Empty)
        {
            throw new ArgumentException("Customer identifier cannot be empty.", nameof(customerId));
        }

        var code = RandomNumberGenerator
            .GetInt32(0, (int)Math.Pow(10, OtpDigits))
            .ToString($"D{OtpDigits}", CultureInfo.InvariantCulture);
        var digest = CreateDigest(customerId, code);
        var keys = new RedisKey[] { CreateOtpKey(customerId), CreateCooldownKey(customerId) };
        var values = new RedisValue[]
        {
            digest,
            (long)OtpLifetime.TotalMilliseconds,
            (long)ResendCooldown.TotalMilliseconds
        };

        var result = await _database
            .ScriptEvaluateAsync(IssueScript, keys, values)
            .WaitAsync(cancellationToken);

        if (ParseIntegerResult(result) != 1)
        {
            throw new RegistrationOtpRateLimitException();
        }

        return new RegistrationOtpIssueResult(code, _timeProvider.GetUtcNow().Add(OtpLifetime));
    }

    /// <summary>
    /// TR: Sunulan OTP için aynı customer-bound HMAC digest'i üretir ve Redis Lua script'i içinde karşılaştırma, deneme artırma ve başarılı challenge silme işlemlerini atomik uygular.
    /// EN: Produces the same customer-bound HMAC digest for the submitted OTP and atomically performs comparison, attempt increment and successful challenge deletion inside a Redis Lua script.
    /// </summary>
    /// <param name="customerId">TR: OTP challenge'ın bağlı olduğu müşteri kimliği. EN: Customer identifier associated with the OTP challenge.</param>
    /// <param name="code">TR: Kullanıcının gönderdiği ham OTP; loglanmamalıdır. EN: Raw OTP submitted by the user; it must not be logged.</param>
    /// <param name="cancellationToken">TR: Redis işlemi beklenirken kullanılacak iptal sinyali. EN: Cancellation signal used while awaiting the Redis operation.</param>
    /// <returns>TR: Challenge mevcut, digest eşleşmiş ve atomik olarak tüketilmişse true; diğer güvenli başarısızlık durumlarında false döndürür. EN: Returns true when the challenge exists, the digest matches and it was atomically consumed; returns false for other safe failure states.</returns>
    public async Task<bool> VerifyAndConsumeAsync(
        Guid customerId,
        string code,
        CancellationToken cancellationToken)
    {
        if (customerId == Guid.Empty)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        var digest = CreateDigest(customerId, code.Trim());
        var result = await _database
            .ScriptEvaluateAsync(
                VerifyScript,
                new RedisKey[] { CreateOtpKey(customerId) },
                new RedisValue[] { digest, MaximumAttempts })
            .WaitAsync(cancellationToken);

        return ParseIntegerResult(result) == 1;
    }

    /// <summary>
    /// TR: OTP'yi customer kimliğine bağlayarak HMAC-SHA256 digest üretir; düşük entropili altı haneli kod Redis sızıntısında pepper olmadan offline doğrulanamaz.
    /// EN: Produces an HMAC-SHA256 digest binding the OTP to the customer identifier so the low-entropy six-digit code cannot be verified offline after Redis exposure without the pepper.
    /// </summary>
    /// <param name="customerId">TR: Digest'e bağlanacak müşteri kimliği. EN: Customer identifier bound into the digest.</param>
    /// <param name="code">TR: Digest üretilecek ham OTP kodu. EN: Raw OTP code from which the digest is produced.</param>
    /// <returns>TR: 64 karakter büyük harf hexadecimal HMAC-SHA256 digest döndürür. EN: Returns a 64-character uppercase hexadecimal HMAC-SHA256 digest.</returns>
    private string CreateDigest(Guid customerId, string code)
    {
        var payload = Encoding.UTF8.GetBytes($"{customerId:N}:{code}");
        var digest = HMACSHA256.HashData(_pepperBytes, payload);
        return Convert.ToHexString(digest);
    }

    /// <summary>
    /// TR: Customer'a özel registration OTP Redis key'ini üretir; key PII veya telefon numarası içermez.
    /// EN: Creates the customer-specific registration OTP Redis key without including PII or a phone number.
    /// </summary>
    /// <param name="customerId">TR: Redis key'e bağlanacak müşteri kimliği. EN: Customer identifier embedded into the Redis key.</param>
    /// <returns>TR: OTP challenge Redis key'ini döndürür. EN: Returns the OTP challenge Redis key.</returns>
    private static RedisKey CreateOtpKey(Guid customerId)
    {
        return $"finwallet:registration:otp:{customerId:N}";
    }

    /// <summary>
    /// TR: OTP resend cooldown için customer'a özel Redis key'ini üretir.
    /// EN: Creates the customer-specific Redis key used for OTP resend cooldown.
    /// </summary>
    /// <param name="customerId">TR: Cooldown key'e bağlanacak müşteri kimliği. EN: Customer identifier embedded into the cooldown key.</param>
    /// <returns>TR: OTP resend cooldown Redis key'ini döndürür. EN: Returns the OTP resend-cooldown Redis key.</returns>
    private static RedisKey CreateCooldownKey(Guid customerId)
    {
        return $"finwallet:registration:otp-cooldown:{customerId:N}";
    }

    /// <summary>
    /// TR: Redis Lua script sonucunu invariant culture ile integer status koduna dönüştürür.
    /// EN: Converts a Redis Lua-script result into an integer status code using invariant culture.
    /// </summary>
    /// <param name="result">TR: Redis script sonucu. EN: Redis script result.</param>
    /// <returns>TR: Lua script'in integer status kodunu döndürür. EN: Returns the integer status code produced by the Lua script.</returns>
    private static int ParseIntegerResult(RedisResult result)
    {
        return int.Parse(result.ToString(), CultureInfo.InvariantCulture);
    }
}
