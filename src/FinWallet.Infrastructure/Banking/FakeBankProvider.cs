using System.Net;
using System.Net.Http.Json;
using FinWallet.Application.Banking;
using FinWallet.Domain.Shared;

namespace FinWallet.Infrastructure.Banking;

/// <summary>
/// TR: Application IBankProvider sınırını FakeBank controller Web API'sine uyarlayan anti-corruption adapter'ıdır; provider DTO, ServiceResult envelope ve numeric enum detaylarını Infrastructure içinde tutar.
/// EN: Anti-corruption adapter mapping the Application IBankProvider boundary to the FakeBank controller Web API while containing provider DTO, ServiceResult envelope and numeric-enum details inside Infrastructure.
/// </summary>
public sealed class FakeBankProvider : IBankProvider
{
    private readonly HttpClient _httpClient;

    /// <summary>
    /// TR: FakeBank için BaseAddress ve timeout değeri önceden ayarlanmış HttpClient ile adapter'ı oluşturur.
    /// EN: Creates the adapter using an HttpClient preconfigured with FakeBank BaseAddress and timeout.
    /// </summary>
    /// <param name="httpClient">TR: FakeBank çağrılarına ayrılmış HttpClient. EN: HttpClient dedicated to FakeBank calls.</param>
    public FakeBankProvider(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    /// <inheritdoc />
    public async Task<ExternalBankAccountResult> OpenAccountAsync(
        Guid externalCustomerReference,
        CurrencyCode currency,
        string requestKey,
        string correlationId,
        CancellationToken cancellationToken)
    {
        if (externalCustomerReference == Guid.Empty) throw new ArgumentException("External customer reference cannot be empty.", nameof(externalCustomerReference));
        ArgumentException.ThrowIfNullOrWhiteSpace(requestKey);
        ValidateCorrelationId(correlationId);

        var request = new OpenAccountProviderRequest(externalCustomerReference, currency.ToString(), requestKey.Trim());
        var data = await SendAsync<OpenAccountProviderResponse>(
            HttpMethod.Post,
            "api/v1/bank/accounts",
            JsonContent.Create(request),
            correlationId,
            cancellationToken);

        return MapAccount(data);
    }

    /// <inheritdoc />
    public async Task<ExternalBankAccountResult> GetAccountAsync(
        Guid externalAccountId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        if (externalAccountId == Guid.Empty) throw new ArgumentException("External account identifier cannot be empty.", nameof(externalAccountId));
        ValidateCorrelationId(correlationId);

        var data = await SendAsync<OpenAccountProviderResponse>(
            HttpMethod.Get,
            $"api/v1/bank/accounts/{externalAccountId:D}",
            content: null,
            correlationId,
            cancellationToken);

        return MapAccount(data);
    }

    /// <inheritdoc />
    public async Task<ExternalBankTransactionResult> StartMoneyMovementAsync(
        Guid externalAccountId,
        Money amount,
        BankMoneyMovementType transactionType,
        string requestKey,
        string correlationId,
        CancellationToken cancellationToken)
    {
        if (externalAccountId == Guid.Empty) throw new ArgumentException("External account identifier cannot be empty.", nameof(externalAccountId));
        if (!amount.IsPositive) throw new ArgumentOutOfRangeException(nameof(amount));
        ArgumentException.ThrowIfNullOrWhiteSpace(requestKey);
        ValidateCorrelationId(correlationId);

        var request = new MoneyMovementProviderRequest(
            externalAccountId,
            amount.Amount,
            amount.Currency.ToString(),
            (int)transactionType,
            requestKey.Trim());

        var data = await SendAsync<MoneyMovementProviderResponse>(
            HttpMethod.Post,
            "api/v1/bank/transactions",
            JsonContent.Create(request),
            correlationId,
            cancellationToken);

        return MapTransaction(data);
    }

    /// <inheritdoc />
    public async Task<ExternalBankTransactionResult> GetTransactionAsync(
        Guid externalTransactionId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        if (externalTransactionId == Guid.Empty) throw new ArgumentException("External transaction identifier cannot be empty.", nameof(externalTransactionId));
        ValidateCorrelationId(correlationId);

        var data = await SendAsync<MoneyMovementProviderResponse>(
            HttpMethod.Get,
            $"api/v1/bank/transactions/{externalTransactionId:D}",
            content: null,
            correlationId,
            cancellationToken);

        return MapTransaction(data);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<ExternalBankStatementItem>> GetStatementAsync(
        Guid externalAccountId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        if (externalAccountId == Guid.Empty) throw new ArgumentException("External account identifier cannot be empty.", nameof(externalAccountId));
        ValidateCorrelationId(correlationId);

        var data = await SendAsync<List<StatementProviderItem>>(
            HttpMethod.Get,
            $"api/v1/bank/accounts/{externalAccountId:D}/statement",
            content: null,
            correlationId,
            cancellationToken);

        return data.Select(MapStatementItem).ToArray();
    }

    /// <summary>
    /// TR: Provider çağrısını gönderir, ServiceResult envelope'u açar ve HTTP/provider hatalarını güvenli Application exception tipine çevirir.
    /// EN: Sends a provider call, unwraps the ServiceResult envelope and maps HTTP/provider failures into the safe Application exception type.
    /// </summary>
    /// <typeparam name="T">TR: Provider başarılı data DTO tipi. EN: Provider successful data DTO type.</typeparam>
    /// <param name="method">TR: HTTP method. EN: HTTP method.</param>
    /// <param name="path">TR: FakeBank relative route. EN: FakeBank relative route.</param>
    /// <param name="content">TR: İsteğe bağlı HTTP request body. EN: Optional HTTP request body.</param>
    /// <param name="correlationId">TR: Propagate edilecek correlation kimliği. EN: Correlation identifier to propagate.</param>
    /// <param name="cancellationToken">TR: HTTP çağrısı iptal sinyali. EN: HTTP-call cancellation signal.</param>
    /// <returns>TR: Başarılı provider data nesnesini döndürür. EN: Returns successful provider data.</returns>
    private async Task<T> SendAsync<T>(
        HttpMethod method,
        string path,
        HttpContent? content,
        string correlationId,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, path) { Content = content };
        request.Headers.TryAddWithoutValidation("X-Correlation-Id", correlationId);

        try
        {
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            var envelope = await response.Content.ReadFromJsonAsync<ProviderEnvelope<T>>(cancellationToken: cancellationToken);

            if (response.IsSuccessStatusCode && envelope is { IsSuccess: true, Data: not null })
            {
                return envelope.Data;
            }

            var code = string.IsNullOrWhiteSpace(envelope?.Code) ? "BANK_PROVIDER_ERROR" : envelope.Code.Trim();
            var message = string.IsNullOrWhiteSpace(envelope?.Message) ? "External bank provider rejected the request." : envelope.Message.Trim();
            throw new ExternalBankProviderException(code, message, IsRetryable(response.StatusCode));
        }
        catch (ExternalBankProviderException)
        {
            throw;
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ExternalBankProviderException(
                "BANK_PROVIDER_TIMEOUT",
                "External bank provider did not respond within the allowed time.",
                isRetryable: true,
                exception);
        }
        catch (HttpRequestException exception)
        {
            throw new ExternalBankProviderException(
                "BANK_PROVIDER_NETWORK_ERROR",
                "External bank provider is temporarily unreachable.",
                isRetryable: true,
                exception);
        }
        catch (NotSupportedException exception)
        {
            throw new ExternalBankProviderException(
                "BANK_PROVIDER_INVALID_RESPONSE",
                "External bank provider returned an unsupported response.",
                isRetryable: false,
                exception);
        }
        catch (System.Text.Json.JsonException exception)
        {
            throw new ExternalBankProviderException(
                "BANK_PROVIDER_INVALID_RESPONSE",
                "External bank provider returned an invalid response.",
                isRetryable: false,
                exception);
        }
    }

    /// <summary>TR: Provider account DTO'sunu Application sonucuna dönüştürür. EN: Maps provider account DTO into Application result.</summary>
    /// <param name="data">TR: Provider account DTO. EN: Provider account DTO.</param>
    /// <returns>TR: Provider bağımsız account sonucu döndürür. EN: Returns provider-independent account result.</returns>
    private static ExternalBankAccountResult MapAccount(OpenAccountProviderResponse data)
    {
        var currency = ParseCurrency(data.Currency);
        var status = data.Status switch
        {
            1 => ExternalBankAccountStatus.Pending,
            2 => ExternalBankAccountStatus.Active,
            3 => ExternalBankAccountStatus.Rejected,
            4 => ExternalBankAccountStatus.Blocked,
            5 => ExternalBankAccountStatus.Closed,
            _ => throw InvalidProviderState("account status")
        };

        return new ExternalBankAccountResult(data.AccountId, data.Iban, currency, status);
    }

    /// <summary>TR: Provider transaction DTO'sunu Application sonucuna dönüştürür. EN: Maps provider transaction DTO into Application result.</summary>
    /// <param name="data">TR: Provider transaction DTO. EN: Provider transaction DTO.</param>
    /// <returns>TR: Provider bağımsız transaction sonucu döndürür. EN: Returns provider-independent transaction result.</returns>
    private static ExternalBankTransactionResult MapTransaction(MoneyMovementProviderResponse data)
    {
        var status = data.Status switch
        {
            1 => ExternalBankTransactionStatus.Pending,
            2 => ExternalBankTransactionStatus.Completed,
            3 => ExternalBankTransactionStatus.Failed,
            _ => throw InvalidProviderState("transaction status")
        };

        return new ExternalBankTransactionResult(data.TransactionId, status, data.AccountBalance);
    }

    /// <summary>TR: Provider statement DTO'sunu Application statement modeline dönüştürür. EN: Maps provider statement DTO into Application statement model.</summary>
    /// <param name="data">TR: Provider statement DTO. EN: Provider statement DTO.</param>
    /// <returns>TR: Provider bağımsız statement satırı döndürür. EN: Returns provider-independent statement item.</returns>
    private static ExternalBankStatementItem MapStatementItem(StatementProviderItem data)
    {
        var type = data.Type switch
        {
            1 => BankMoneyMovementType.Deposit,
            2 => BankMoneyMovementType.Withdrawal,
            _ => throw InvalidProviderState("transaction type")
        };

        return new ExternalBankStatementItem(data.TransactionId, type, data.Amount, ParseCurrency(data.Currency), data.CompletedAt);
    }

    /// <summary>TR: Provider currency metnini desteklenen CurrencyCode enumuna dönüştürür. EN: Converts provider currency text into a supported CurrencyCode enum.</summary>
    /// <param name="currency">TR: Provider currency metni. EN: Provider currency text.</param>
    /// <returns>TR: Desteklenen currency enumunu döndürür. EN: Returns supported currency enum.</returns>
    private static CurrencyCode ParseCurrency(string currency)
    {
        if (!Enum.TryParse<CurrencyCode>(currency, ignoreCase: true, out var parsed))
        {
            throw InvalidProviderState("currency");
        }

        return parsed;
    }

    /// <summary>TR: HTTP status koduna göre provider hatasının retryable olup olmadığını belirler. EN: Determines whether a provider failure is retryable from its HTTP status code.</summary>
    /// <param name="statusCode">TR: Provider HTTP status kodu. EN: Provider HTTP status code.</param>
    /// <returns>TR: Güvenli retry mümkünse true döndürür. EN: Returns true when a safe retry may be attempted.</returns>
    private static bool IsRetryable(HttpStatusCode statusCode)
    {
        return (int)statusCode >= 500 || statusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests;
    }

    /// <summary>TR: Correlation kimliğinin boş olmamasını doğrular. EN: Validates that the correlation identifier is not empty.</summary>
    /// <param name="correlationId">TR: Doğrulanacak correlation kimliği. EN: Correlation identifier to validate.</param>
    private static void ValidateCorrelationId(string correlationId) => ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

    /// <summary>TR: Bilinmeyen provider enum/currency değerleri için güvenli invalid-response exception üretir. EN: Creates a safe invalid-response exception for unknown provider enum/currency values.</summary>
    /// <param name="field">TR: Geçersiz provider alan adı. EN: Invalid provider field name.</param>
    /// <returns>TR: Provider response contract hatasını döndürür. EN: Returns provider-response contract failure.</returns>
    private static ExternalBankProviderException InvalidProviderState(string field)
    {
        return new ExternalBankProviderException(
            "BANK_PROVIDER_INVALID_RESPONSE",
            $"External bank provider returned an unsupported {field}.",
            isRetryable: false);
    }

    /// <summary>TR: FakeBank ServiceResult benzeri transport envelope'unu Infrastructure içinde temsil eder. EN: Represents the FakeBank ServiceResult-like transport envelope inside Infrastructure.</summary>
    private sealed class ProviderEnvelope<T>
    {
        /// <summary>TR: Provider operasyon başarı durumunu döndürür veya ayarlar. EN: Gets or sets provider operation success state.</summary>
        public bool IsSuccess { get; init; }

        /// <summary>TR: Provider result kodunu döndürür veya ayarlar. EN: Gets or sets provider result code.</summary>
        public string Code { get; init; } = string.Empty;

        /// <summary>TR: Güvenli provider mesajını döndürür veya ayarlar. EN: Gets or sets safe provider message.</summary>
        public string Message { get; init; } = string.Empty;

        /// <summary>TR: Başarılı provider data değerini döndürür veya ayarlar. EN: Gets or sets successful provider data.</summary>
        public T? Data { get; init; }
    }

    /// <summary>TR: FakeBank account-opening request transport DTO'sunu temsil eder. EN: Represents FakeBank account-opening request transport DTO.</summary>
    /// <param name="ExternalCustomerReference">TR: Provider customer referansı. EN: Provider customer reference.</param>
    /// <param name="Currency">TR: Currency kodu. EN: Currency code.</param>
    /// <param name="RequestKey">TR: Provider idempotency anahtarı. EN: Provider idempotency key.</param>
    private sealed record OpenAccountProviderRequest(Guid ExternalCustomerReference, string Currency, string RequestKey);

    /// <summary>TR: FakeBank account response transport DTO'sunu temsil eder. EN: Represents FakeBank account response transport DTO.</summary>
    private sealed class OpenAccountProviderResponse
    {
        /// <summary>TR: Provider hesap kimliği. EN: Provider account identifier.</summary>
        public Guid AccountId { get; init; }

        /// <summary>TR: Provider IBAN-benzeri değer. EN: Provider IBAN-like value.</summary>
        public string Iban { get; init; } = string.Empty;

        /// <summary>TR: Provider currency kodu. EN: Provider currency code.</summary>
        public string Currency { get; init; } = string.Empty;

        /// <summary>TR: Numeric provider hesap durumu. EN: Numeric provider account state.</summary>
        public int Status { get; init; }
    }

    /// <summary>TR: FakeBank money-movement request transport DTO'sunu temsil eder. EN: Represents FakeBank money-movement request transport DTO.</summary>
    /// <param name="AccountId">TR: Provider hesap kimliği. EN: Provider account identifier.</param>
    /// <param name="Amount">TR: İşlem tutarı. EN: Transaction amount.</param>
    /// <param name="Currency">TR: Currency kodu. EN: Currency code.</param>
    /// <param name="TransactionType">TR: Numeric provider transaction tipi. EN: Numeric provider transaction type.</param>
    /// <param name="RequestKey">TR: Provider idempotency anahtarı. EN: Provider idempotency key.</param>
    private sealed record MoneyMovementProviderRequest(Guid AccountId, decimal Amount, string Currency, int TransactionType, string RequestKey);

    /// <summary>TR: FakeBank money-movement response transport DTO'sunu temsil eder. EN: Represents FakeBank money-movement response transport DTO.</summary>
    private sealed class MoneyMovementProviderResponse
    {
        /// <summary>TR: Provider transaction kimliği. EN: Provider transaction identifier.</summary>
        public Guid TransactionId { get; init; }

        /// <summary>TR: Numeric provider transaction durumu. EN: Numeric provider transaction state.</summary>
        public int Status { get; init; }

        /// <summary>TR: Provider account balance snapshot değeri. EN: Provider account balance snapshot.</summary>
        public decimal AccountBalance { get; init; }
    }

    /// <summary>TR: FakeBank statement item transport DTO'sunu temsil eder. EN: Represents FakeBank statement-item transport DTO.</summary>
    private sealed class StatementProviderItem
    {
        /// <summary>TR: Provider transaction kimliği. EN: Provider transaction identifier.</summary>
        public Guid TransactionId { get; init; }

        /// <summary>TR: Numeric provider transaction tipi. EN: Numeric provider transaction type.</summary>
        public int Type { get; init; }

        /// <summary>TR: Transaction tutarı. EN: Transaction amount.</summary>
        public decimal Amount { get; init; }

        /// <summary>TR: Provider currency kodu. EN: Provider currency code.</summary>
        public string Currency { get; init; } = string.Empty;

        /// <summary>TR: Provider tamamlanma UTC zamanı. EN: Provider UTC completion time.</summary>
        public DateTimeOffset CompletedAt { get; init; }
    }
}
