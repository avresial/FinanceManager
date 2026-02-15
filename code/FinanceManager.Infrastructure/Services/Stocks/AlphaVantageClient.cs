using FinanceManager.Application.Options;
using FinanceManager.Application.Services.Stocks;
using FinanceManager.Domain.Dtos;
using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Entities.Stocks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FinanceManager.Infrastructure.Services.Stocks;

internal sealed class AlphaVantageClient(
    HttpClient httpClient,
    ILogger<AlphaVantageClient> logger,
    IOptions<StockApiOptions> options) : IAlphaVantageClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<IReadOnlyList<TickerSearchMatch>> SearchTicker(string keywords, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(keywords)) return [];

        var apiKey = options.Value.ApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            logger.LogWarning("Stock API key is missing.");
            return [];
        }

        var url = BuildUrl($"function=SYMBOL_SEARCH&keywords={Uri.EscapeDataString(keywords)}&apikey={apiKey}");

        try
        {
            var response = await httpClient.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Stock API search failed with status {StatusCode}", response.StatusCode);
                return [];
            }

            var content = await response.Content.ReadAsStringAsync(ct);
            var apiResponse = JsonSerializer.Deserialize<AlphaVantageSymbolSearchResponse>(content, JsonOptions);
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
        if (string.IsNullOrWhiteSpace(ticker)) return [];

        var apiKey = options.Value.ApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            logger.LogWarning("Stock API key is missing.");
            return [];
        }

        var outputSize = string.IsNullOrWhiteSpace(options.Value.OutputSize) ? "compact" : options.Value.OutputSize;
        var url = BuildUrl($"function=TIME_SERIES_DAILY&symbol={Uri.EscapeDataString(ticker)}&outputsize={outputSize}&apikey={apiKey}");

        try
        {
            var response = await httpClient.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Stock API daily series failed with status {StatusCode}", response.StatusCode);
                return [];
            }

            var content = await response.Content.ReadAsStringAsync(ct);
            var apiResponse = JsonSerializer.Deserialize<AlphaVantageDailyResponse>(content, JsonOptions);
            if (apiResponse?.Series is null || apiResponse.Series.Count == 0) return [];

            var prices = new List<StockPrice>();
            foreach (var entry in apiResponse.Series)
            {
                if (!TryParseDate(entry.Key, out var date)) continue;
                if (date < start.Date || date > end.Date) continue;

                var close = ParseDecimal(entry.Value?.Close);
                if (close <= 0) continue;

                prices.Add(new StockPrice
                {
                    Ticker = ticker,
                    PricePerUnit = close,
                    Currency = currency,
                    Date = date
                });
            }

            return prices;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Stock API daily series failed for ticker {Ticker}", ticker);
            return [];
        }
    }

    public async Task<IReadOnlyList<StockListing>> GetListings(CancellationToken ct = default)
    {
        var apiKey = options.Value.ApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            logger.LogWarning("Stock API key is missing.");
            return [];
        }

        var url = BuildUrl($"function=LISTING_STATUS&apikey={apiKey}");

        try
        {
            var response = await httpClient.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Stock API listing status failed with status {StatusCode}", response.StatusCode);
                return [];
            }

            var content = await response.Content.ReadAsStringAsync(ct);
            return ParseListingStatusCsv(content);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Stock API listing status failed");
            return [];
        }
    }

    private string BuildUrl(string query)
    {
        var baseUrl = options.Value.BaseUrl;
        if (string.IsNullOrWhiteSpace(baseUrl)) return query;

        if (baseUrl.Contains('?'))
            return $"{baseUrl}&{query}";

        return $"{baseUrl}?{query}";
    }

    private static List<StockListing> ParseListingStatusCsv(string csv)
    {
        var listings = new List<StockListing>();
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        if (lines.Length == 0) return listings;

        for (int i = 1; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrWhiteSpace(line)) continue;

            var parts = line.Split(',');
            if (parts.Length < 7) continue;

            listings.Add(new StockListing(
                parts[0],
                parts[1],
                parts[2],
                parts[3],
                ParseNullableDate(parts[4]),
                ParseNullableDate(parts[5]),
                parts[6]));
        }

        return listings;
    }

    private static DateTime? ParseNullableDate(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Equals("null", StringComparison.OrdinalIgnoreCase))
            return null;

        return DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date)
            ? DateTime.SpecifyKind(date.Date, DateTimeKind.Utc)
            : null;
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
        [JsonPropertyName("Time Series (Daily)")]
        public Dictionary<string, AlphaVantageDailySeriesEntry>? Series { get; set; }
    }

    private sealed class AlphaVantageDailySeriesEntry
    {
        [JsonPropertyName("4. close")]
        public string? Close { get; set; }
    }
}