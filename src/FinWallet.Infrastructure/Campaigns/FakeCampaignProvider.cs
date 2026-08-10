using System.Net.Http.Json;
using FinWallet.Application.Campaigns;
using FinWallet.Domain.Shared;

namespace FinWallet.Infrastructure.Campaigns;

/// <summary>TR: ICampaignProvider sınırını FakeCampaign Web API'sine uyarlayan ve provider DTO/enum ayrıntılarını Infrastructure içinde tutan anti-corruption adapter'ıdır. EN: Anti-corruption adapter mapping ICampaignProvider to FakeCampaign Web API while containing provider DTO/enum details inside Infrastructure.</summary>
public sealed class FakeCampaignProvider : ICampaignProvider
{
    private readonly HttpClient _httpClient;

    /// <summary>TR: Önceden yapılandırılmış FakeCampaign HttpClient ile adapter'ı oluşturur. EN: Creates the adapter with a preconfigured FakeCampaign HttpClient.</summary>
    /// <param name="httpClient">TR: FakeCampaign çağrılarına ayrılmış HttpClient. EN: HttpClient dedicated to FakeCampaign calls.</param>
    public FakeCampaignProvider(HttpClient httpClient) => _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

    /// <inheritdoc />
    public async Task<CampaignEvaluationResult> EvaluateAsync(Guid customerReference, string merchantId, Money amount, DateTimeOffset requestedAt, string correlationId, CancellationToken cancellationToken)
    {
        if (customerReference == Guid.Empty) throw new ArgumentException("Customer reference cannot be empty.", nameof(customerReference));
        ArgumentException.ThrowIfNullOrWhiteSpace(merchantId);
        if (!amount.IsPositive) throw new ArgumentOutOfRangeException(nameof(amount));
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        using var request = new HttpRequestMessage(HttpMethod.Post, "api/v1/campaigns/evaluate")
        {
            Content = JsonContent.Create(new ProviderRequest(customerReference, merchantId.Trim(), amount.Amount, amount.Currency.ToString(), requestedAt))
        };
        request.Headers.TryAddWithoutValidation("X-Correlation-Id", correlationId);

        try
        {
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            var envelope = await response.Content.ReadFromJsonAsync<ProviderEnvelope<ProviderResponse>>(cancellationToken: cancellationToken);
            if (response.IsSuccessStatusCode && envelope is { IsSuccess: true, Data: not null })
            {
                if (!Enum.TryParse<CurrencyCode>(envelope.Data.Currency, true, out var currency))
                {
                    throw new CampaignProviderException("CAMPAIGN_PROVIDER_INVALID_RESPONSE", "Campaign provider returned an unsupported currency.");
                }

                CampaignSponsor? sponsor = envelope.Data.SponsorType switch
                {
                    null => null,
                    1 => CampaignSponsor.Platform,
                    2 => CampaignSponsor.Merchant,
                    _ => throw new CampaignProviderException("CAMPAIGN_PROVIDER_INVALID_RESPONSE", "Campaign provider returned an unsupported sponsor type.")
                };

                return new CampaignEvaluationResult(
                    envelope.Data.ProviderReference,
                    envelope.Data.Eligible,
                    envelope.Data.CampaignId,
                    envelope.Data.OriginalAmount,
                    envelope.Data.DiscountAmount,
                    envelope.Data.FinalAmount,
                    currency,
                    sponsor,
                    envelope.Data.Reason);
            }

            throw new CampaignProviderException(
                string.IsNullOrWhiteSpace(envelope?.Code) ? "CAMPAIGN_PROVIDER_ERROR" : envelope.Code,
                string.IsNullOrWhiteSpace(envelope?.Message) ? "Campaign provider rejected the request." : envelope.Message);
        }
        catch (CampaignProviderException)
        {
            throw;
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new CampaignProviderException("CAMPAIGN_PROVIDER_TIMEOUT", "Campaign provider did not respond within the allowed time.", exception);
        }
        catch (HttpRequestException exception)
        {
            throw new CampaignProviderException("CAMPAIGN_PROVIDER_NETWORK_ERROR", "Campaign provider is temporarily unreachable.", exception);
        }
        catch (System.Text.Json.JsonException exception)
        {
            throw new CampaignProviderException("CAMPAIGN_PROVIDER_INVALID_RESPONSE", "Campaign provider returned an invalid response.", exception);
        }
    }

    /// <summary>TR: FakeCampaign request transport modelidir. EN: FakeCampaign request transport model.</summary>
    /// <param name="CustomerReference">TR: Customer referansı. EN: Customer reference.</param>
    /// <param name="MerchantId">TR: Merchant kimliği. EN: Merchant identifier.</param>
    /// <param name="Amount">TR: Orijinal tutar. EN: Original amount.</param>
    /// <param name="Currency">TR: Currency kodu. EN: Currency code.</param>
    /// <param name="RequestedAt">TR: Request zamanı. EN: Request timestamp.</param>
    private sealed record ProviderRequest(Guid CustomerReference, string MerchantId, decimal Amount, string Currency, DateTimeOffset RequestedAt);

    /// <summary>TR: FakeCampaign response transport modelidir. EN: FakeCampaign response transport model.</summary>
    private sealed class ProviderResponse
    {
        /// <summary>TR: Provider referansı. EN: Provider reference.</summary>
        public Guid ProviderReference { get; init; }
        /// <summary>TR: Kampanya uygunluğu. EN: Campaign eligibility.</summary>
        public bool Eligible { get; init; }
        /// <summary>TR: Kampanya kimliği. EN: Campaign identifier.</summary>
        public string? CampaignId { get; init; }
        /// <summary>TR: Orijinal tutar. EN: Original amount.</summary>
        public decimal OriginalAmount { get; init; }
        /// <summary>TR: İndirim tutarı. EN: Discount amount.</summary>
        public decimal DiscountAmount { get; init; }
        /// <summary>TR: Final tutar. EN: Final amount.</summary>
        public decimal FinalAmount { get; init; }
        /// <summary>TR: Currency kodu. EN: Currency code.</summary>
        public string Currency { get; init; } = string.Empty;
        /// <summary>TR: Numeric sponsor tipi. EN: Numeric sponsor type.</summary>
        public int? SponsorType { get; init; }
        /// <summary>TR: Provider reason kodu. EN: Provider reason code.</summary>
        public string Reason { get; init; } = string.Empty;
    }

    /// <summary>TR: FakeCampaign ServiceResult transport envelope'udur. EN: FakeCampaign ServiceResult transport envelope.</summary>
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
