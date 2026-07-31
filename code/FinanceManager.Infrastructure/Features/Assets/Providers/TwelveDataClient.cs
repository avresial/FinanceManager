using FinanceManager.Application.Backfill.Currencies;
using FinanceManager.Application.FinancialAccounts.Stock.Pricing;
using FinanceManager.Application.Shared.ExternalServices;
using FinanceManager.Application.Shared.Options;
using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FinanceManager.Infrastructure.Features.Assets.Providers;

internal sealed class TwelveDataClient(
    HttpClient httpClient,
    ILogger<TwelveDataClient> logger,
    IOptions<TwelveDataOptions> options,
    IExternalServiceConfigService configService,
    TwelveDataCreditBudget budget) : IStockPriceSource, IFxDailySource
{
    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };
    private static readonly IReadOnlySet<AssetType> _assetTypes = new HashSet<AssetType> { AssetType.Stock, AssetType.ETF };
    private static readonly IReadOnlySet<string> _intervals = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "1day" };

    public string Name => "TwelveData";
    public MarketDataProvider? Provider => MarketDataProvider.TwelveData;
    public int Priority => options.Value.Priority;
    public IReadOnlySet<AssetType> SupportedAssetTypes => _assetTypes;
    public IReadOnlySet<string> SupportedIntervals => _intervals;

    public async Task<IReadOnlyList<StockPrice>> GetDailySeries(
        string symbol,
        string isin,
        DateTime start,
        DateTime end,
        Currency currency,
        CancellationToken ct = default)
    {
        var response = await GetTimeSeriesAsync(ToProviderSymbol(symbol), start, end, ct);
        if (response.Status != RequestStatus.Ok || response.Body?.Values is null)
            return [];

        var prices = response.Body.Values
            .Select(x => TryMapPrice(x, isin, currency))
            .Where(x => x is not null && x.Date >= start.Date && x.Date <= end.Date)
            .Cast<StockPrice>()
            .ToList();

        logger.LogInformation(
            "Twelve Data returned {Returned} daily records for {Symbol} over {Start:yyyy-MM-dd}..{End:yyyy-MM-dd}.",
            prices.Count, Sanitize(symbol), start, end);
        return prices;
    }

    public async Task<StockPrice?> GetLatestQuote(
        string symbol,
        string isin,
        Currency currency,
        CancellationToken ct = default)
    {
        var providerSymbol = ToProviderSymbol(symbol);
        var response = await SendAsync($"quote?{providerSymbol}&interval=1day", ct);
        if (response.Status != RequestStatus.Ok)
            return null;

        var quote = JsonSerializer.Deserialize<TwelveDataQuoteResponse>(response.Content, _jsonOptions);
        var close = ParseDecimal(quote?.Close);
        if (!TryParseDate(quote?.Datetime, out var date) || close <= 0)
            return null;

        return new StockPrice { Isin = isin, Currency = currency, Date = date, PricePerUnit = close };
    }

    public async Task<FxDailyResult> GetDailyRatesAsync(
        string fromCurrency,
        string toCurrency,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fromCurrency) || string.IsNullOrWhiteSpace(toCurrency))
            return FxDailyResult.Empty;

        var end = DateTime.UtcNow.Date;
        var response = await GetTimeSeriesAsync(
            $"symbol={Uri.EscapeDataString($"{fromCurrency}/{toCurrency}")}",
            end.AddDays(-30),
            end,
            cancellationToken);

        if (response.Status == RequestStatus.RateLimited)
            return FxDailyResult.RateLimited;
        if (response.Status == RequestStatus.Error)
            return FxDailyResult.Failed;
        if (response.Body?.Values is null)
            return FxDailyResult.Empty;

        var points = response.Body.Values
            .Select(x => (Date: ParseDate(x.Datetime), Close: ParseDecimal(x.Close)))
            .Where(x => x.Date is not null && x.Close > 0)
            .ToDictionary(x => x.Date!.Value, x => x.Close);
        return points.Count == 0 ? FxDailyResult.Empty : FxDailyResult.Success(points);
    }

    private async Task<TimeSeriesResult> GetTimeSeriesAsync(
        string providerSymbol,
        DateTime start,
        DateTime end,
        CancellationToken ct)
    {
        var response = await SendAsync(
            $"time_series?{providerSymbol}&interval=1day&start_date={start:yyyy-MM-dd}&end_date={end:yyyy-MM-dd}&outputsize=5000&order=ASC",
            ct);
        if (response.Status != RequestStatus.Ok)
            return new(response.Status, null);

        var body = JsonSerializer.Deserialize<TwelveDataTimeSeriesResponse>(response.Content, _jsonOptions);
        if (string.Equals(body?.Status, "error", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning("Twelve Data rejected {Symbol}: {Message}", Sanitize(providerSymbol), Sanitize(body?.Message ?? "unknown error"));
            return new(RequestStatus.Error, body);
        }

        return new(RequestStatus.Ok, body);
    }

    private async Task<HttpResult> SendAsync(string pathAndQuery, CancellationToken ct)
    {
        var config = await configService.GetServiceAsync("TwelveData", ct);
        if (!options.Value.Enabled || !config.IsEnabled || string.IsNullOrWhiteSpace(config.ApiKey))
        {
            logger.LogWarning("Twelve Data is disabled or its API key is missing.");
            return new(RequestStatus.Error, string.Empty);
        }

        if (!budget.TryConsume())
        {
            logger.LogWarning("Twelve Data configured credit budget is exhausted; deferring request.");
            return new(RequestStatus.RateLimited, string.Empty);
        }

        var url = $"{config.BaseUrl.TrimEnd('/')}/{pathAndQuery}";
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("apikey", config.ApiKey);
            using var response = await httpClient.SendAsync(request, ct);
            var content = await response.Content.ReadAsStringAsync(ct);
            var creditsUsed = response.Headers.TryGetValues("api-credits-used", out var used) ? used.FirstOrDefault() : null;
            var creditsLeft = response.Headers.TryGetValues("api-credits-left", out var left) ? left.FirstOrDefault() : null;
            logger.LogDebug("Twelve Data request consumed credits; provider reports used {Used}, left {Left}.", creditsUsed, creditsLeft);

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
                return new(RequestStatus.RateLimited, content);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Twelve Data request failed with status {StatusCode}.", response.StatusCode);
                return new(RequestStatus.Error, content);
            }

            var error = JsonSerializer.Deserialize<TwelveDataErrorResponse>(content, _jsonOptions);
            if (string.Equals(error?.Status, "error", StringComparison.OrdinalIgnoreCase))
            {
                var errorMessage = error?.Message;
                var status = error?.Code == 429
                    || errorMessage?.Contains("rate limit", StringComparison.OrdinalIgnoreCase) == true
                    || errorMessage?.Contains("credits", StringComparison.OrdinalIgnoreCase) == true
                    ? RequestStatus.RateLimited
                    : RequestStatus.Error;
                logger.LogWarning("Twelve Data rejected a request: {Message}", Sanitize(errorMessage ?? "unknown error"));
                return new(status, content);
            }

            return new(RequestStatus.Ok, content);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Twelve Data request failed.");
            return new(RequestStatus.Error, string.Empty);
        }
    }

    private static string ToProviderSymbol(string symbol)
    {
        var parts = symbol.Trim().Split(':', 2, StringSplitOptions.TrimEntries);
        var query = $"symbol={Uri.EscapeDataString(parts[0])}";
        return parts.Length == 2 ? $"{query}&exchange={Uri.EscapeDataString(parts[1])}" : query;
    }

    private static StockPrice? TryMapPrice(TwelveDataValue value, string isin, Currency currency)
    {
        if (!TryParseDate(value.Datetime, out var date)) return null;
        var close = ParseDecimal(value.Close);
        return close <= 0 ? null : new StockPrice { Isin = isin, Currency = currency, Date = date, PricePerUnit = close };
    }

    private static DateTime? ParseDate(string? value) => TryParseDate(value, out var date) ? date : null;

    private static bool TryParseDate(string? value, out DateTime date)
    {
        var ok = DateTime.TryParseExact(
            value,
            ["yyyy-MM-dd", "yyyy-MM-dd HH:mm:ss"],
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal,
            out var parsed);
        date = DateTime.SpecifyKind(parsed.Date, DateTimeKind.Utc);
        return ok;
    }

    private static decimal ParseDecimal(string? value) =>
        decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0m;

    private static string Sanitize(string value) => value.Replace("\r", string.Empty).Replace("\n", string.Empty);

    private enum RequestStatus { Ok, RateLimited, Error }
    private readonly record struct HttpResult(RequestStatus Status, string Content);
    private readonly record struct TimeSeriesResult(RequestStatus Status, TwelveDataTimeSeriesResponse? Body);

    private sealed class TwelveDataTimeSeriesResponse
    {
        public string? Status { get; set; }
        public string? Message { get; set; }
        public List<TwelveDataValue>? Values { get; set; }
    }

    private sealed class TwelveDataValue
    {
        public string? Datetime { get; set; }
        public string? Close { get; set; }
    }

    private sealed class TwelveDataQuoteResponse
    {
        [JsonPropertyName("datetime")]
        public string? Datetime { get; set; }
        [JsonPropertyName("close")]
        public string? Close { get; set; }
    }

    private sealed class TwelveDataErrorResponse
    {
        public int Code { get; set; }
        public string? Message { get; set; }
        public string? Status { get; set; }
    }
}