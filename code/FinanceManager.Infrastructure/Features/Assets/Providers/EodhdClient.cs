using FinanceManager.Application.FinancialAccounts.Stock.Pricing;
using FinanceManager.Application.Shared.ExternalServices;
using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FinanceManager.Infrastructure.Features.Assets.Providers;

/// <summary>
/// EODHD (eodhd.com) price source. Fetches split/dividend-adjusted end-of-day history via the
/// <c>/eod/{symbol}</c> endpoint and maps <c>adjusted_close</c> onto <see cref="StockPrice"/>.
/// Used as the fallback behind Alpha Vantage. See: https://eodhd.com/financial-apis/api-for-historical-data-and-volumes
/// </summary>
internal sealed class EodhdClient(
    HttpClient httpClient,
    ILogger<EodhdClient> logger,
    IExternalServiceConfigService configService) : IStockPriceSource
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public string Name => "Eodhd";
    public MarketDataProvider? Provider => MarketDataProvider.Eodhd;
    public int Priority => 300;

    public async Task<IReadOnlyList<StockPrice>> GetDailySeries(string symbol, string isin, DateTime start, DateTime end, Currency currency, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(symbol)) return [];

        var config = await configService.GetServiceAsync("Eodhd", ct);
        var apiKey = config.ApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            logger.LogWarning("EODHD API key is missing; skipping fallback price source.");
            return [];
        }

        var eodhdSymbol = ToEodhdSymbol(symbol);
        var from = start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var to = end.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var url = $"{config.BaseUrl.TrimEnd('/')}/eod/{Uri.EscapeDataString(eodhdSymbol)}?api_token={Uri.EscapeDataString(apiKey)}&fmt=json&period=d&from={from}&to={to}";

        try
        {
            using var response = await httpClient.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("EODHD daily series failed with status {StatusCode} for {Symbol}", response.StatusCode, Sanitize(eodhdSymbol));
                return [];
            }

            var content = await response.Content.ReadAsStringAsync(ct);
            List<EodhdEodEntry>? entries;
            try
            {
                entries = JsonSerializer.Deserialize<List<EodhdEodEntry>>(content, _jsonOptions);
            }
            catch (JsonException ex)
            {
                logger.LogWarning(ex, "EODHD returned unparseable content for {Symbol}", Sanitize(eodhdSymbol));
                return [];
            }

            if (entries is null || entries.Count == 0) return [];

            var prices = new List<StockPrice>(entries.Count);
            foreach (var entry in entries)
            {
                if (!TryParseDate(entry.Date, out var date)) continue;
                if (date < start.Date || date > end.Date) continue;

                // Prefer the adjusted close; fall back to the raw close when absent.
                var close = entry.AdjustedClose ?? entry.Close ?? 0m;
                if (close <= 0) continue;

                prices.Add(new StockPrice
                {
                    Isin = isin,
                    PricePerUnit = close,
                    Currency = currency,
                    Date = date
                });
            }

            return prices;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "EODHD daily series failed for {Symbol}", Sanitize(eodhdSymbol));
            return [];
        }
    }

    // Strip CR/LF so an attacker-influenced symbol cannot forge log entries (log injection).
    private static string Sanitize(string value)
        => value.Replace("\r", string.Empty).Replace("\n", string.Empty);

    // #470 — provider-specific symbology belongs on the Listing entity. Until then, best-effort:
    // Alpha Vantage US symbols are bare ("AAPL") while EODHD requires an exchange suffix, so default
    // a suffix-less symbol to the US market. Symbols that already carry a suffix pass through as-is.
    private static string ToEodhdSymbol(string symbol)
    {
        var trimmed = symbol.Trim();
        return trimmed.Contains('.') ? trimmed : $"{trimmed}.US";
    }

    private static bool TryParseDate(string? value, out DateTime date)
    {
        var ok = DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed);
        date = DateTime.SpecifyKind(parsed.Date, DateTimeKind.Utc);
        return ok;
    }

    private sealed class EodhdEodEntry
    {
        [JsonPropertyName("date")]
        public string? Date { get; set; }

        [JsonPropertyName("close")]
        public decimal? Close { get; set; }

        [JsonPropertyName("adjusted_close")]
        public decimal? AdjustedClose { get; set; }
    }
}