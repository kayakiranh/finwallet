using System.Net.Http.Json;
using FinWallet.Application.Cutoff;
using FinWallet.Domain.Shared;

namespace FinWallet.Infrastructure.Cutoff;

/// <summary>TR: ICutoffProvider sınırını FakeCutoff Web API'sine uyarlayan ve provider DTO'larını Infrastructure içinde tutan anti-corruption adapter'ıdır. EN: Anti-corruption adapter mapping ICutoffProvider to FakeCutoff Web API while containing provider DTOs inside Infrastructure.</summary>
public sealed class FakeCutoffProvider : ICutoffProvider
{
    private readonly HttpClient _httpClient;

    /// <summary>TR: Önceden yapılandırılmış FakeCutoff HttpClient ile adapter'ı oluşturur. EN: Creates the adapter with a preconfigured FakeCutoff HttpClient.</summary>
    /// <param name="httpClient">TR: FakeCutoff çağrılarına ayrılmış HttpClient. EN: HttpClient dedicated to FakeCutoff calls.</param>
    public FakeCutoffProvider(HttpClient httpClient) => _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

    /// <inheritdoc />
    public async Task<CutoffEvaluationResult> EvaluateAsync(string countryCode, CurrencyCode currency, string transactionType, DateTimeOffset requestedAt, string correlationId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(countryCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(transactionType);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        using var request = new HttpRequestMessage(HttpMethod.Post, "api/v1/cutoffs/evaluate")
        {
            Content = JsonContent.Create(new ProviderRequest(countryCode.Trim().ToUpperInvariant(), currency.ToString(), transactionType.Trim(), requestedAt))
        };
        request.Headers.TryAddWithoutValidation("X-Correlation-Id", correlationId);

        try
        {
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            var envelope = await response.Content.ReadFromJsonAsync<ProviderEnvelope<ProviderResponse>>(cancellationToken: cancellationToken);
            if (response.IsSuccessStatusCode && envelope is { IsSuccess: true, Data: not null })
            {
                return new CutoffEvaluationResult(envelope.Data.ReferenceId, envelope.Data.CanProcessNow, envelope.Data.ProcessingDate, envelope.Data.SettlementDate, envelope.Data.Reason);
            }

            throw new CutoffProviderException(
                string.IsNullOrWhiteSpace(envelope?.Code) ? "CUTOFF_PROVIDER_ERROR" : envelope.Code,
                string.IsNullOrWhiteSpace(envelope?.Message) ? "Cutoff provider rejected the request." : envelope.Message);
        }
        catch (CutoffProviderException)
        {
            throw;
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new CutoffProviderException("CUTOFF_PROVIDER_TIMEOUT", "Cutoff provider did not respond within the allowed time.", exception);
        }
        catch (HttpRequestException exception)
        {
            throw new CutoffProviderException("CUTOFF_PROVIDER_NETWORK_ERROR", "Cutoff provider is temporarily unreachable.", exception);
        }
        catch (System.Text.Json.JsonException exception)
        {
            throw new CutoffProviderException("CUTOFF_PROVIDER_INVALID_RESPONSE", "Cutoff provider returned an invalid response.", exception);
        }
    }

    /// <summary>TR: FakeCutoff request transport modelidir. EN: FakeCutoff request transport model.</summary>
    /// <param name="CountryCode">TR: Ülke kodu. EN: Country code.</param>
    /// <param name="Currency">TR: Currency kodu. EN: Currency code.</param>
    /// <param name="TransactionType">TR: Transaction tipi. EN: Transaction type.</param>
    /// <param name="RequestedAt">TR: Request zamanı. EN: Request timestamp.</param>
    private sealed record ProviderRequest(string CountryCode, string Currency, string TransactionType, DateTimeOffset RequestedAt);

    /// <summary>TR: FakeCutoff response transport modelidir. EN: FakeCutoff response transport model.</summary>
    private sealed class ProviderResponse
    {
        /// <summary>TR: Provider referansı. EN: Provider reference.</summary>
        public Guid ReferenceId { get; init; }
        /// <summary>TR: Anlık işlenebilirlik. EN: Immediate processability.</summary>
        public bool CanProcessNow { get; init; }
        /// <summary>TR: Processing business tarihi. EN: Processing business date.</summary>
        public DateOnly ProcessingDate { get; init; }
        /// <summary>TR: Settlement business tarihi. EN: Settlement business date.</summary>
        public DateOnly SettlementDate { get; init; }
        /// <summary>TR: Provider reason kodu. EN: Provider reason code.</summary>
        public string Reason { get; init; } = string.Empty;
    }

    /// <summary>TR: FakeCutoff ServiceResult transport envelope'udur. EN: FakeCutoff ServiceResult transport envelope.</summary>
    private sealed class ProviderEnvelope<T>
    {
        /// <summary>TR: Başarı durumudur. EN: Success state.</summary>
        public bool IsSuccess { get; init; }
        /// <summary>TR: Result kodudur. EN: Result code.</summary>
        public string Code { get; init; } = string.Empty;
        /// <summary>TR: Güvenli result mesajıdır. EN: Safe result message.</summary>
        public string Message { get; init; } = string.Empty;
        /// <summary>TR: Başarılı data değeridir. EN: Successful data value.</summary>
        public T? Data { get; init; }
    }
}
