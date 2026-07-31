using FinanceManager.Application.Backfill.Currencies;
using FinanceManager.Application.Shared.ExternalServices;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FinanceManager.Infrastructure.Features.FinancialAccounts.Currencies.Providers;

/// <summary>
/// Fetches Alpha Vantage <c>FX_DAILY</c> series for the startup currency backfill. Reuses the shared
/// Alpha Vantage service configuration (base URL + API key) so it honours the same throttling and
/// key management as the stock client, and reports quota exhaustion distinctly so the backfill can
/// stop calling for the rest of the run.
/// </summary>
internal sealed class AlphaVantageFxClient(
    HttpClient httpClient,
    ILogger<AlphaVantageFxClient> logger,
    IExternalServiceConfigService configService) : IFxDailySource
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public string Name => "AlphaVantage";
    public int Priority => 100;

    public async Task<FxDailyResult> GetDailyRatesAsync(string fromCurrency, string toCurrency, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fromCurrency) || string.IsNullOrWhiteSpace(toCurrency))
            return FxDailyResult.Empty;

        var config = await configService.GetServiceAsync("AlphaVantage", cancellationToken);
        var apiKey = config.ApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            logger.LogWarning("Alpha Vantage API key is missing; cannot backfill FX pair {From}->{To}.", fromCurrency, toCurrency);
            return FxDailyResult.Empty;
        }

        var url = BuildUrl(
            $"function=FX_DAILY&from_symbol={Uri.EscapeDataString(fromCurrency)}&to_symbol={Uri.EscapeDataString(toCurrency)}&outputsize=compact&apikey={apiKey}",
            config.BaseUrl);

        try
        {
            using var response = await httpClient.GetAsync(url, cancellationToken);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
                return FxDailyResult.RateLimited;

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Alpha Vantage FX_DAILY failed with status {StatusCode} for {From}->{To}.", response.StatusCode, fromCurrency, toCurrency);
                return FxDailyResult.Failed;
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var parsed = JsonSerializer.Deserialize<AlphaVantageFxResponse>(content, _jsonOptions);

            // Alpha Vantage returns HTTP 200 with a "Note"/"Information" body when the free-tier quota
            // is exhausted; detect that so the backfill can stop instead of hammering the endpoint.
            if (IsRateLimit(parsed))
            {
                logger.LogWarning("Alpha Vantage FX_DAILY reported a rate limit for {From}->{To}.", fromCurrency, toCurrency);
                return FxDailyResult.RateLimited;
            }

            if (parsed?.Series is null || parsed.Series.Count == 0)
                return FxDailyResult.Empty;

            var points = new Dictionary<DateTime, decimal>(parsed.Series.Count);
            foreach (var entry in parsed.Series)
            {
                if (!TryParseDate(entry.Key, out var date)) continue;

                var close = ParseDecimal(entry.Value?.Close);
                if (close <= 0) continue;

                points[date] = close;
            }

            return points.Count == 0 ? FxDailyResult.Empty : FxDailyResult.Success(points);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Alpha Vantage FX_DAILY threw for {From}->{To}.", fromCurrency, toCurrency);
            return FxDailyResult.Failed;
        }
    }

    private static bool IsRateLimit(AlphaVantageFxResponse? response)
    {
        var message = response?.Note ?? response?.Information;
        if (string.IsNullOrWhiteSpace(message)) return false;

        return message.Contains("rate limit", StringComparison.OrdinalIgnoreCase)
            || message.Contains("call frequency", StringComparison.OrdinalIgnoreCase)
            || message.Contains("premium", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildUrl(string query, string baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl)) return query;
        return baseUrl.Contains('?') ? $"{baseUrl}&{query}" : $"{baseUrl}?{query}";
    }

    private static bool TryParseDate(string value, out DateTime date)
    {
        var ok = DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed);
        date = DateTime.SpecifyKind(parsed.Date, DateTimeKind.Utc);
        return ok;
    }

    private static decimal ParseDecimal(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return 0m;
        return decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0m;
    }

    private sealed class AlphaVantageFxResponse
    {
        public string? Information { get; set; }
        public string? Note { get; set; }

        [JsonPropertyName("Time Series FX (Daily)")]
        public Dictionary<string, AlphaVantageFxEntry>? Series { get; set; }
    }

    private sealed class AlphaVantageFxEntry
    {
        [JsonPropertyName("4. close")]
        public string? Close { get; set; }
    }
}