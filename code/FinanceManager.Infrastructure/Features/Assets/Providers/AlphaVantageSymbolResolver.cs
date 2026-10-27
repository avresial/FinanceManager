using FinanceManager.Application.FinancialAccounts.Investments.Discovery;
using FinanceManager.Application.FinancialAccounts.Stock.Pricing;
using FinanceManager.Application.FinancialAccounts.Stock.Resolution;
using FinanceManager.Application.Shared.ExternalServices;
using FinanceManager.Domain.Assets.Discovery;
using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using System.Text.Json;

namespace FinanceManager.Infrastructure.Features.Assets.Providers;

internal sealed class AlphaVantageSymbolResolver(
    HttpClient httpClient,
    IExternalServiceConfigService configService,
    AlphaVantageRequestBudget budget,
    IAlphaVantageClient priceClient) : IMarketDataSymbolResolver
{
    public MarketDataProvider Provider => MarketDataProvider.AlphaVantage;
    public int Priority => 100;

    public async Task<MarketDataSymbolResolution> ResolveAsync(InstrumentDiscoveryResultDto instrument, CancellationToken ct = default)
    {
        var config = await configService.GetServiceAsync("AlphaVantage", ct);
        if (!config.IsEnabled || string.IsNullOrWhiteSpace(config.ApiKey) || !budget.TryConsume())
            return Unavailable("Alpha Vantage is disabled, unconfigured, or over budget.");

        var url = $"{config.BaseUrl}{(config.BaseUrl.Contains('?') ? "&" : "?")}function=SYMBOL_SEARCH&keywords={Uri.EscapeDataString(instrument.Ticker)}&apikey={Uri.EscapeDataString(config.ApiKey)}";
        try
        {
            using var response = await httpClient.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode) return Unavailable("Alpha Vantage search failed.");
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            if (!json.RootElement.TryGetProperty("bestMatches", out var matches) || matches.ValueKind != JsonValueKind.Array)
                return Unavailable("Alpha Vantage returned an invalid search response.");

            var expectedMic = instrument.ExchangeMic;
            foreach (var match in matches.EnumerateArray())
            {
                var symbol = Text(match, "1. symbol");
                var region = Text(match, "4. region");
                var currency = Text(match, "8. currency");
                var (baseTicker, suffixMic, suffixCode) = ParseProviderSymbol(symbol);
                if (!string.Equals(baseTicker, instrument.Ticker, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(currency, instrument.TradingCurrency, StringComparison.OrdinalIgnoreCase)) continue;

                var (regionMic, regionCode) = MapRegion(region);
                var mic = suffixMic ?? regionMic;
                var exchangeCode = suffixCode ?? regionCode;
                if (expectedMic is not null && !string.Equals(expectedMic, mic, StringComparison.OrdinalIgnoreCase)) continue;
                if (mic is null && (exchangeCode != "US" || expectedMic is not null || !string.Equals(instrument.ProviderSymbol, symbol, StringComparison.OrdinalIgnoreCase))) continue;

                return new(Provider, Priority, "candidate", symbol, exchangeCode, currency,
                    Text(match, "2. name"), Text(match, "3. type"), region);
            }

            return new(Provider, Priority, "not found");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (OperationCanceledException) { return Unavailable("Alpha Vantage search timed out."); }
        catch (HttpRequestException) { return Unavailable("Alpha Vantage search failed."); }
        catch (JsonException) { return Unavailable("Alpha Vantage returned an invalid search response."); }
    }

    public async Task<bool> ValidateAsync(MarketDataSymbolResolution candidate, CancellationToken ct = default)
    {
        if (candidate.Provider != Provider || candidate.Status != "candidate" || string.IsNullOrWhiteSpace(candidate.Symbol) || string.IsNullOrWhiteSpace(candidate.Currency)) return false;
        var prices = await priceClient.GetDailySeries(candidate.Symbol, DateTime.UtcNow.Date.AddDays(-10), DateTime.UtcNow.Date,
            new Currency(0, candidate.Currency, string.Empty), ct);
        return prices.Any(price => price.PricePerUnit > 0);
    }

    private static string Text(JsonElement element, string property) => element.TryGetProperty(property, out var value) ? value.GetString() ?? string.Empty : string.Empty;
    private static (string? Mic, string? Code) MapRegion(string region)
    {
        if (region.Equals("United States", StringComparison.OrdinalIgnoreCase) || region.Equals("USA", StringComparison.OrdinalIgnoreCase) || region.Equals("US", StringComparison.OrdinalIgnoreCase))
            return (null, "US");
        var mic = region.ToLowerInvariant() switch
        {
            "frankfurt" => "XFRA",
            _ => BrokerSymbol.TryLookupByVenueToken(region)?.Mic
        };
        var code = mic switch { "XLON" => "LON", "XETR" => "DEX", "XFRA" => "FRK", _ => null };
        return (mic, code);
    }
    private static (string BaseTicker, string? Mic, string? Code) ParseProviderSymbol(string symbol)
    {
        foreach (var (suffix, mic, code) in new[] { (".DEX", "XETR", "DEX"), (".FRK", "XFRA", "FRK"), (".LON", "XLON", "LON") })
            if (symbol.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) return (symbol[..^suffix.Length], mic, code);
        return (symbol, null, null);
    }
    private MarketDataSymbolResolution Unavailable(string error) => new(Provider, Priority, "provider unavailable", Error: error);
}