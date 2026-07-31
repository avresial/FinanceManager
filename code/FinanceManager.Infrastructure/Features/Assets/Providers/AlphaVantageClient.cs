using FinanceManager.Application.FinancialAccounts.Stock.Pricing;
using FinanceManager.Application.Shared.ExternalServices;
using FinanceManager.Application.Shared.Options;
using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Dtos;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Dtos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FinanceManager.Infrastructure.Features.Assets.Providers;

internal sealed class AlphaVantageClient(
    HttpClient httpClient,
    ILogger<AlphaVantageClient> logger,
    IOptions<StockApiOptions> options,
    IExternalServiceConfigService configService) : IAlphaVantageClient, IStockPriceSource
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public string Name => "AlphaVantage";
    public MarketDataProvider? Provider => MarketDataProvider.AlphaVantage;
    public int Priority => 100;

    public async Task<IReadOnlyList<TickerSearchMatch>> SearchTicker(string keywords, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(keywords)) return [];

        var config = await configService.GetServiceAsync("AlphaVantage", ct);
        var apiKey = config.ApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            logger.LogWarning("Stock API key is missing.");
            return [];
        }

        var url = BuildUrl($"function=SYMBOL_SEARCH&keywords={Uri.EscapeDataString(keywords)}&apikey={apiKey}", config.BaseUrl);

        try
        {
            var response = await httpClient.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Stock API search failed with status {StatusCode}", response.StatusCode);
                return [];
            }

            var content = await response.Content.ReadAsStringAsync(ct);
            var apiResponse = JsonSerializer.Deserialize<AlphaVantageSymbolSearchResponse>(content, _jsonOptions);
            if (apiResponse?.BestMatches is null || apiResponse.BestMatches.Count == 0) return [];

            var result = new List<TickerSearchMatch>(apiResponse.BestMatches.Count);
            foreach (var match in apiResponse.BestMatches)
            {
                result.Add(new TickerSearchMatch
                {
                    Symbol = match.Symbol ?? string.Empty,
                    Name = match.Name ?? string.Empty,
                    Type = match.Type ?? string.Empty,
                    Region = match.Region ?? string.Empty,
                    MarketOpen = match.MarketOpen ?? string.Empty,
                    MarketClose = match.MarketClose ?? string.Empty,
                    Timezone = match.Timezone ?? string.Empty,
                    Currency = match.Currency ?? string.Empty,
                    MatchScore = ParseDecimal(match.MatchScore)
                });
            }

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Stock API search failed for keywords {Keywords}", keywords);
            return [];
        }
    }

    public async Task<IReadOnlyList<StockPrice>> GetDailySeries(string ticker, DateTime start, DateTime end, Currency currency, CancellationToken ct = default)
    {
        return await GetDailySeries(ticker, string.Empty, start, end, currency, ct);
    }

    public async Task<IReadOnlyList<StockPrice>> GetDailySeries(string ticker, string isin, DateTime start, DateTime end, Currency currency, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(ticker)) return [];

        var config = await configService.GetServiceAsync("AlphaVantage", ct);
        var apiKey = config.ApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            logger.LogWarning("Stock API key is missing.");
            return [];
        }

        var outputSize = string.IsNullOrWhiteSpace(options.Value.OutputSize) ? "compact" : options.Value.OutputSize;
        try
        {
            var apiResponse = await FetchDailySeries("TIME_SERIES_DAILY_ADJUSTED", ticker, outputSize, apiKey, config.BaseUrl, ct);
            if (apiResponse?.Series is null or { Count: 0 })
            {
                await Task.Delay(TimeSpan.FromSeconds(1), ct);
                apiResponse = await FetchDailySeries("TIME_SERIES_DAILY", ticker, "compact", apiKey, config.BaseUrl, ct);
            }

            if (apiResponse?.Series is null || apiResponse.Series.Count == 0) return [];

            var prices = new List<StockPrice>();
            foreach (var entry in apiResponse.Series)
            {
                if (!TryParseDate(entry.Key, out var date)) continue;
                if (date < start.Date || date > end.Date) continue;

                // Prefer the adjusted close; fall back to the raw close if the adjusted field is absent.
                var close = ParseDecimal(entry.Value?.AdjustedClose);
                if (close <= 0) close = ParseDecimal(entry.Value?.Close);
                if (close <= 0) continue;

                prices.Add(new StockPrice
                {
                    Isin = isin,
                    PricePerUnit = close,
                    Currency = currency,
                    Date = date
                });
            }

            if (prices.Count == 0)
                logger.LogWarning("Stock API returned {Count} daily prices for {Ticker}, but none between {Start} and {End}.",
                    apiResponse.Series.Count, ticker, start.Date, end.Date);

            return prices;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Stock API daily series failed for ticker {Ticker}", ticker);
            return [];
        }
    }

    private async Task<AlphaVantageDailyResponse?> FetchDailySeries(
        string function, string ticker, string outputSize, string apiKey, string baseUrl, CancellationToken ct)
    {
        var url = BuildUrl($"function={function}&symbol={Uri.EscapeDataString(ticker)}&outputsize={outputSize}&apikey={apiKey}", baseUrl);
        using var response = await httpClient.GetAsync(url, ct);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Stock API daily series failed with status {StatusCode}", response.StatusCode);
            return null;
        }

        var result = JsonSerializer.Deserialize<AlphaVantageDailyResponse>(await response.Content.ReadAsStringAsync(ct), _jsonOptions);
        if (result?.Series is null or { Count: 0 })
        {
            var reason = (result?.Information ?? result?.Note ?? "empty response").Replace('\r', ' ').Replace('\n', ' ');
            logger.LogWarning("Stock API function {Function} returned no daily prices for {Ticker} ({Reason}).", function, ticker, reason);
        }

        return result;
    }

    private static string BuildUrl(string query, string baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl)) return query;

        if (baseUrl.Contains('?'))
            return $"{baseUrl}&{query}";

        return $"{baseUrl}?{query}";
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

    private sealed class AlphaVantageSymbolSearchResponse
    {
        [JsonPropertyName("bestMatches")]
        public List<AlphaVantageSymbolSearchMatch> BestMatches { get; set; } = [];
    }

    private sealed class AlphaVantageSymbolSearchMatch
    {
        [JsonPropertyName("1. symbol")]
        public string? Symbol { get; set; }

        [JsonPropertyName("2. name")]
        public string? Name { get; set; }

        [JsonPropertyName("3. type")]
        public string? Type { get; set; }

        [JsonPropertyName("4. region")]
        public string? Region { get; set; }

        [JsonPropertyName("5. marketOpen")]
        public string? MarketOpen { get; set; }

        [JsonPropertyName("6. marketClose")]
        public string? MarketClose { get; set; }

        [JsonPropertyName("7. timezone")]
        public string? Timezone { get; set; }

        [JsonPropertyName("8. currency")]
        public string? Currency { get; set; }

        [JsonPropertyName("9. matchScore")]
        public string? MatchScore { get; set; }
    }

    private sealed class AlphaVantageDailyResponse
    {
        public string? Information { get; set; }
        public string? Note { get; set; }

        [JsonPropertyName("Time Series (Daily)")]
        public Dictionary<string, AlphaVantageDailySeriesEntry>? Series { get; set; }
    }

    private sealed class AlphaVantageDailySeriesEntry
    {
        [JsonPropertyName("4. close")]
        public string? Close { get; set; }

        [JsonPropertyName("5. adjusted close")]
        public string? AdjustedClose { get; set; }
    }
}