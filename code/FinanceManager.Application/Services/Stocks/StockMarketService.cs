using FinanceManager.Application.Options;
using FinanceManager.Domain.Dtos;
using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Entities.Stocks;
using FinanceManager.Domain.Repositories;
using FinanceManager.Domain.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FinanceManager.Application.Services.Stocks;

internal class StockMarketService(
    HttpClient httpClient,
    ILogger<StockMarketService> logger,
    IOptions<StockApiOptions> options,
    IStockPriceRepository stockPriceRepository,
    ICurrencyRepository currencyRepository,
    IStockDetailsRepository stockDetailsRepository) : IStockMarketService
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<IReadOnlyList<TickerSearchMatch>> SearchTicker(string keywords, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(keywords)) return Array.Empty<TickerSearchMatch>();

        var apiKey = options.Value.ApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            logger.LogWarning("Stock API key is missing.");
            return Array.Empty<TickerSearchMatch>();
        }

        var url = $"{options.Value.BaseUrl}?function=SYMBOL_SEARCH&keywords={Uri.EscapeDataString(keywords)}&apikey={apiKey}";

        try
        {
            var response = await httpClient.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Stock API search failed with status {StatusCode}", response.StatusCode);
                return Array.Empty<TickerSearchMatch>();
            }

            var content = await response.Content.ReadAsStringAsync(ct);
            var apiResponse = JsonSerializer.Deserialize<AlphaVantageSymbolSearchResponse>(content, _jsonOptions);
            if (apiResponse?.BestMatches is null || apiResponse.BestMatches.Count == 0) return Array.Empty<TickerSearchMatch>();

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
            return Array.Empty<TickerSearchMatch>();
        }
    }

    public async Task<IReadOnlyList<StockPrice>> GetDailyStock(string ticker, DateTime start, DateTime end, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(ticker) || start == default || end == default) return Array.Empty<StockPrice>();
        if (end < start) return Array.Empty<StockPrice>();

        var normalizedTicker = ticker.Trim().ToUpperInvariant();
        var existing = await stockPriceRepository.GetRange(normalizedTicker, start, end);

        if (NeedsFetch(existing, start, end))
        {
            var stockDetails = await ResolveStockDetails(normalizedTicker, ct);
            var apiPrices = await FetchDailySeries(normalizedTicker, start, end, stockDetails.Currency, ct);
            if (apiPrices.Count > 0)
            {
                await stockPriceRepository.Add(apiPrices);
                existing = MergeByDate(existing, apiPrices);
            }
        }

        return existing.OrderByDescending(x => x.Date).ToList();
    }

    public async Task<IReadOnlyList<StockDetails>> GetListingStatus(CancellationToken ct = default)
    {
        var apiKey = options.Value.ApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            logger.LogWarning("Stock API key is missing.");
            return [];
        }

        var url = $"{options.Value.BaseUrl}?function=LISTING_STATUS&apikey={apiKey}";

        try
        {
            var response = await httpClient.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Stock API listing status failed with status {StatusCode}", response.StatusCode);
                return [];
            }

            var content = await response.Content.ReadAsStringAsync(ct);
            var alphaVantageListings = ParseListingStatusCsv(content);

            var defaultCurrency = await currencyRepository.GetOrAdd(DefaultCurrency.USD.ShortName, DefaultCurrency.USD.Symbol, ct);
            var stockDetailsList = new List<StockDetails>();

            foreach (var listing in alphaVantageListings)
            {
                if (string.IsNullOrWhiteSpace(listing.Symbol)) continue;

                var stockDetails = new StockDetails
                {
                    Ticker = listing.Symbol,
                    Name = listing.Name ?? string.Empty,
                    Type = listing.AssetType ?? string.Empty,
                    Region = listing.Exchange ?? string.Empty,
                    Currency = defaultCurrency
                };

                stockDetailsList.Add(stockDetails);
            }

            return stockDetailsList;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Stock API listing status failed");
            return [];
        }
    }

    private static List<AlphaVantageListing> ParseListingStatusCsv(string csv)
    {
        var listings = new List<AlphaVantageListing>();
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        if (lines.Length == 0) return listings;

        // Skip header line
        for (int i = 1; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrWhiteSpace(line)) continue;

            var parts = line.Split(',');
            if (parts.Length < 7) continue;

            var listing = new AlphaVantageListing
            {
                Symbol = parts[0],
                Name = parts[1],
                Exchange = parts[2],
                AssetType = parts[3],
                IpoDate = ParseNullableDate(parts[4]),
                DelistingDate = ParseNullableDate(parts[5]),
                Status = parts[6]
            };

            listings.Add(listing);
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

    private async Task<StockDetails> ResolveStockDetails(string ticker, CancellationToken ct)
    {
        var existingDetails = await stockDetailsRepository.Get(ticker, ct);
        if (existingDetails is not null && !NeedsEnrichment(existingDetails))
            return existingDetails;

        var matches = await SearchTicker(ticker, ct);
        var exact = matches.FirstOrDefault(x => string.Equals(x.Symbol, ticker, StringComparison.OrdinalIgnoreCase));
        var selected = exact ?? matches.FirstOrDefault();

        Currency currency;
        if (selected is not null && !string.IsNullOrWhiteSpace(selected.Currency))
            currency = await currencyRepository.GetOrAdd(selected.Currency, selected.Currency, ct);
        else
            currency = existingDetails?.Currency ?? await currencyRepository.GetOrAdd(DefaultCurrency.USD.ShortName, DefaultCurrency.USD.Symbol, ct);

        var details = new StockDetails
        {
            Ticker = ticker,
            Name = selected?.Name ?? existingDetails?.Name ?? string.Empty,
            Type = selected?.Type ?? existingDetails?.Type ?? string.Empty,
            Region = selected?.Region ?? existingDetails?.Region ?? string.Empty,
            Currency = currency
        };

        return await stockDetailsRepository.Add(details, ct);
    }

    private static bool NeedsEnrichment(StockDetails details)
        => string.IsNullOrWhiteSpace(details.Name)
           || string.IsNullOrWhiteSpace(details.Type)
           || string.IsNullOrWhiteSpace(details.Region);

    private async Task<List<StockPrice>> FetchDailySeries(string ticker, DateTime start, DateTime end, Currency currency, CancellationToken ct)
    {
        var apiKey = options.Value.ApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            logger.LogWarning("Stock API key is missing.");
            return [];
        }

        var outputSize = string.IsNullOrWhiteSpace(options.Value.OutputSize) ? "compact" : options.Value.OutputSize;
        var url = $"{options.Value.BaseUrl}?function=TIME_SERIES_DAILY&symbol={Uri.EscapeDataString(ticker)}&outputsize={outputSize}&apikey={apiKey}";

        try
        {
            var response = await httpClient.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Stock API daily series failed with status {StatusCode}", response.StatusCode);
                return [];
            }

            var content = await response.Content.ReadAsStringAsync(ct);
            var apiResponse = JsonSerializer.Deserialize<AlphaVantageDailyResponse>(content, _jsonOptions);
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

    private static bool NeedsFetch(IReadOnlyList<StockPrice> existing, DateTime start, DateTime end)
    {
        if (existing.Count == 0) return true;

        var existingDates = existing.Select(x => x.Date.Date).ToHashSet();
        for (var date = start.Date; date <= end.Date; date = date.AddDays(1))
        {
            if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) continue;
            if (!existingDates.Contains(date)) return true;
        }

        return false;
    }

    private static IReadOnlyList<StockPrice> MergeByDate(IReadOnlyList<StockPrice> existing, List<StockPrice> fetched)
    {
        var merged = existing.Concat(fetched)
            .GroupBy(x => x.Date.Date)
            .Select(x => x.OrderByDescending(p => p.Date).First())
            .ToList();

        return merged;
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

    private sealed class AlphaVantageListing
    {
        public string? Symbol { get; set; }
        public string? Name { get; set; }
        public string? Exchange { get; set; }
        public string? AssetType { get; set; }
        public DateTime? IpoDate { get; set; }
        public DateTime? DelistingDate { get; set; }
        public string? Status { get; set; }
    }
}