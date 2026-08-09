using System.Net.Http.Json;
using FinWallet.Application.Fraud;
using FinWallet.Domain.Fraud;

namespace FinWallet.Infrastructure.Fraud;

/// <summary>
/// TR: FinWallet dış fraud provider sınırını controller tabanlı FakeFraud HTTP API'ye uyarlayan ve provider envelope/enum detaylarını Infrastructure içinde tutan anti-corruption adapter'ıdır.
/// EN: Anti-corruption adapter that maps the FinWallet external-fraud boundary to the controller-based FakeFraud HTTP API while containing provider envelope/enum details inside Infrastructure.
/// </summary>
public sealed class FakeFraudProvider : IExternalFraudProvider
{
    private readonly HttpClient _httpClient;

    /// <summary>
    /// TR: FakeFraud için önceden BaseAddress ve timeout ayarlanmış HttpClient ile provider adapter'ını oluşturur.
    /// EN: Creates the provider adapter using an HttpClient preconfigured with FakeFraud BaseAddress and timeout.
    /// </summary>
    /// <param name="httpClient">TR: FakeFraud endpoint'lerine ayrılmış HttpClient örneği. EN: HttpClient instance dedicated to FakeFraud endpoints.</param>
    public FakeFraudProvider(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    /// <summary>
    /// TR: PII içermeyen Application fraud context'ini provider DTO'suna dönüştürür, correlation bilgisini taşır ve provider kararını FinWallet fraud enum'una map eder.
    /// EN: Converts the PII-free Application fraud context into the provider DTO, propagates correlation and maps the provider decision into the FinWallet fraud enum.
    /// </summary>
    /// <param name="context">TR: Dış fraud değerlendirmesine gönderilecek normalize Application context'i. EN: Normalized Application context sent to external fraud evaluation.</param>
    /// <param name="cancellationToken">TR: Dış HTTP çağrısına taşınacak iptal sinyali. EN: Cancellation signal propagated to the external HTTP call.</param>
    /// <returns>TR: Provider transport detaylarından arındırılmış fraud değerlendirme sonucunu döndürür. EN: Returns a fraud-evaluation result stripped of provider transport details.</returns>
    /// <exception cref="HttpRequestException">TR: Provider network hatası veya başarı dışı HTTP status oluşursa fırlatılır. EN: Thrown when a provider network error occurs or a non-success HTTP status is returned.</exception>
    /// <exception cref="InvalidOperationException">TR: Provider başarılı HTTP status ile geçersiz/eksik envelope veya bilinmeyen karar değeri döndürürse fırlatılır. EN: Thrown when the provider returns an invalid/incomplete envelope or unknown decision with a successful HTTP status.</exception>
    public async Task<ExternalFraudEvaluationResult> EvaluateAsync(
        ExternalFraudEvaluationContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var providerRequest = new FakeFraudRequest
        {
            TransactionReference = context.TransactionReference,
            CustomerReference = context.CustomerReference,
            TransactionType = context.TransactionType,
            Amount = context.Amount,
            Currency = context.Currency,
            CountryCode = context.CountryCode,
            DeviceReference = context.DeviceReference,
            IsNewDevice = context.IsNewDevice,
            TransactionCountLastFiveMinutes = context.TransactionCountLastFiveMinutes,
            AmountLastTwentyFourHours = context.AmountLastTwentyFourHours,
            MerchantId = context.MerchantId
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "api/v1/fraud/evaluate")
        {
            Content = JsonContent.Create(providerRequest)
        };
        request.Headers.TryAddWithoutValidation("X-Correlation-Id", context.CorrelationId);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var envelope = await response.Content.ReadFromJsonAsync<FakeFraudServiceResult>(cancellationToken);
        if (envelope is null || !envelope.IsSuccess || envelope.Data is null)
        {
            throw new InvalidOperationException("FakeFraud returned an invalid successful response envelope.");
        }

        var decision = MapDecision(envelope.Data.Decision);
        return new ExternalFraudEvaluationResult(
            envelope.Data.ProviderReference,
            decision,
            envelope.Data.RiskScore,
            envelope.Data.ReasonCodes ?? Array.Empty<string>());
    }

    /// <summary>
    /// TR: FakeFraud provider'ın numeric Allow/Review/Deny değerini provider bağımsız FinWallet fraud kararına dönüştürür.
    /// EN: Converts the FakeFraud provider numeric Allow/Review/Deny value into the provider-independent FinWallet fraud decision.
    /// </summary>
    /// <param name="providerDecision">TR: FakeFraud transport contract'ındaki numeric karar değeri. EN: Numeric decision value from the FakeFraud transport contract.</param>
    /// <returns>TR: Eşleşen FinWallet FraudDecision değerini döndürür. EN: Returns the matching FinWallet FraudDecision value.</returns>
    private static FraudDecision MapDecision(int providerDecision)
    {
        return providerDecision switch
        {
            1 => FraudDecision.Allow,
            2 => FraudDecision.Review,
            3 => FraudDecision.Deny,
            _ => throw new InvalidOperationException("FakeFraud returned an unknown decision value.")
        };
    }

    /// <summary>
    /// TR: FakeFraud HTTP endpoint'ine gönderilen provider-specific request DTO'sunu temsil eder.
    /// EN: Represents the provider-specific request DTO sent to the FakeFraud HTTP endpoint.
    /// </summary>
    private sealed class FakeFraudRequest
    {
        /// <summary>TR: Provider'a gönderilen transaction referansını döndürür veya ayarlar. EN: Gets or sets transaction reference sent to the provider.</summary>
        public Guid TransactionReference { get; init; }

        /// <summary>TR: Provider'a gönderilen PII içermeyen customer referansını döndürür veya ayarlar. EN: Gets or sets non-PII customer reference sent to the provider.</summary>
        public Guid CustomerReference { get; init; }

        /// <summary>TR: Provider transaction type değerini döndürür veya ayarlar. EN: Gets or sets provider transaction type.</summary>
        public string TransactionType { get; init; } = string.Empty;

        /// <summary>TR: Provider işlem tutarını döndürür veya ayarlar. EN: Gets or sets provider transaction amount.</summary>
        public decimal Amount { get; init; }

        /// <summary>TR: Provider currency kodunu döndürür veya ayarlar. EN: Gets or sets provider currency code.</summary>
        public string Currency { get; init; } = string.Empty;

        /// <summary>TR: Provider source-country kodunu döndürür veya ayarlar. EN: Gets or sets provider source-country code.</summary>
        public string CountryCode { get; init; } = string.Empty;

        /// <summary>TR: PII içermeyen provider cihaz referansını döndürür veya ayarlar. EN: Gets or sets non-PII provider device reference.</summary>
        public string DeviceReference { get; init; } = string.Empty;

        /// <summary>TR: Provider yeni-cihaz sinyalini döndürür veya ayarlar. EN: Gets or sets provider new-device signal.</summary>
        public bool IsNewDevice { get; init; }

        /// <summary>TR: Son beş dakika provider velocity sayacını döndürür veya ayarlar. EN: Gets or sets provider five-minute velocity counter.</summary>
        public int TransactionCountLastFiveMinutes { get; init; }

        /// <summary>TR: Son yirmi dört saat provider aggregate tutarını döndürür veya ayarlar. EN: Gets or sets provider twenty-four-hour aggregate amount.</summary>
        public decimal AmountLastTwentyFourHours { get; init; }

        /// <summary>TR: İsteğe bağlı provider merchant referansını döndürür veya ayarlar. EN: Gets or sets optional provider merchant reference.</summary>
        public string? MerchantId { get; init; }
    }

    /// <summary>
    /// TR: Shared ServiceResult'a Infrastructure bağımlılığı oluşturmadan FakeFraud HTTP envelope'unu deserialize eden provider-specific response DTO'sunu temsil eder.
    /// EN: Represents a provider-specific response DTO used to deserialize the FakeFraud HTTP envelope without creating an Infrastructure dependency on the shared ServiceResult type.
    /// </summary>
    private sealed class FakeFraudServiceResult
    {
        /// <summary>TR: Provider işleminin başarılı olup olmadığını döndürür veya ayarlar. EN: Gets or sets whether the provider operation succeeded.</summary>
        public bool IsSuccess { get; init; }

        /// <summary>TR: Provider result code değerini döndürür veya ayarlar. EN: Gets or sets provider result code.</summary>
        public string Code { get; init; } = string.Empty;

        /// <summary>TR: Provider güvenli result message değerini döndürür veya ayarlar. EN: Gets or sets provider-safe result message.</summary>
        public string Message { get; init; } = string.Empty;

        /// <summary>TR: Başarılı provider fraud data'sını döndürür veya ayarlar. EN: Gets or sets successful provider fraud data.</summary>
        public FakeFraudResponse? Data { get; init; }
    }

    /// <summary>
    /// TR: FakeFraud provider response içindeki karar, skor, referans ve reason code alanlarını taşıyan provider-specific DTO'dur.
    /// EN: Provider-specific DTO carrying decision, score, reference and reason-code fields from the FakeFraud provider response.
    /// </summary>
    private sealed class FakeFraudResponse
    {
        /// <summary>TR: Provider değerlendirme referansını döndürür veya ayarlar. EN: Gets or sets provider evaluation reference.</summary>
        public Guid ProviderReference { get; init; }

        /// <summary>TR: Provider numeric fraud kararını döndürür veya ayarlar. EN: Gets or sets provider numeric fraud decision.</summary>
        public int Decision { get; init; }

        /// <summary>TR: Provider risk skorunu döndürür veya ayarlar. EN: Gets or sets provider risk score.</summary>
        public int RiskScore { get; init; }

        /// <summary>TR: Provider reason code listesini döndürür veya ayarlar. EN: Gets or sets provider reason-code collection.</summary>
        public string[]? ReasonCodes { get; init; }
    }
}
