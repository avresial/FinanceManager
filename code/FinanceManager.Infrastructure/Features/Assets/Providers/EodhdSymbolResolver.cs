using FinanceManager.Application.FinancialAccounts.Investments.Discovery;
using FinanceManager.Application.FinancialAccounts.Stock.Pricing;
using FinanceManager.Application.FinancialAccounts.Stock.Resolution;
using FinanceManager.Application.Shared.ExternalServices;
using FinanceManager.Domain.Assets.Discovery;
using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using System.Text.Json;

namespace FinanceManager.Infrastructure.Features.Assets.Providers;

internal sealed class EodhdSymbolResolver(
    HttpClient httpClient,
    IExternalServiceConfigService configService,
    EodhdClient priceClient) : IMarketDataSymbolResolver
{
    public MarketDataProvider Provider => MarketDataProvider.Eodhd;
    public int Priority => 300;

    public async Task<MarketDataSymbolResolution> ResolveAsync(InstrumentDiscoveryResultDto instrument, CancellationToken ct = default)
    {
        var config = await configService.GetServiceAsync("Eodhd", ct);
        if (!config.IsEnabled || string.IsNullOrWhiteSpace(config.ApiKey)) return Unavailable("EODHD is disabled or unconfigured.");
        var url = $"{config.BaseUrl.TrimEnd('/')}/search/{Uri.EscapeDataString(instrument.Ticker)}?api_token={Uri.EscapeDataString(config.ApiKey)}&fmt=json&limit=20";
        try
        {
            using var response = await httpClient.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode) return Unavailable("EODHD search failed.");
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            if (json.RootElement.ValueKind != JsonValueKind.Array) return Unavailable("EODHD returned an invalid search response.");
            foreach (var item in json.RootElement.EnumerateArray())
            {
                var code = Text(item, "Code");
                var exchange = Text(item, "Exchange");
                var currency = Text(item, "Currency");
                var isin = Text(item, "ISIN");
                var mic = MapExchange(exchange);
                if (!string.Equals(code, instrument.Ticker, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(mic, instrument.ExchangeMic, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(currency, instrument.TradingCurrency, StringComparison.OrdinalIgnoreCase)) continue;
                if (!string.IsNullOrWhiteSpace(instrument.Isin) && !string.IsNullOrWhiteSpace(isin) &&
                    !string.Equals(isin, instrument.Isin, StringComparison.OrdinalIgnoreCase)) continue;
                if (mic is null && !string.Equals(instrument.ProviderSymbol, $"{code}.{exchange}", StringComparison.OrdinalIgnoreCase)) continue;
                return new(Provider, Priority, "candidate", $"{code}.{exchange}", exchange, currency,
                    Text(item, "Name"), Text(item, "Type"), exchange);
            }
            return new(Provider, Priority, "not found");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (OperationCanceledException) { return Unavailable("EODHD search timed out."); }
        catch (HttpRequestException) { return Unavailable("EODHD search failed."); }
        catch (JsonException) { return Unavailable("EODHD returned an invalid search response."); }
    }

    public async Task<bool> ValidateAsync(MarketDataSymbolResolution candidate, CancellationToken ct = default)
    {
        if (candidate.Provider != Provider || candidate.Status != "candidate" || string.IsNullOrWhiteSpace(candidate.Symbol) || string.IsNullOrWhiteSpace(candidate.Currency)) return false;
        return (await priceClient.GetDailySeries(candidate.Symbol, string.Empty, DateTime.UtcNow.Date.AddDays(-10), DateTime.UtcNow.Date,
            new Currency(0, candidate.Currency, string.Empty), ct)).Any(price => price.PricePerUnit > 0);
    }

    private static string Text(JsonElement element, string property) => element.TryGetProperty(property, out var value) ? value.GetString() ?? string.Empty : string.Empty;
    private static string? MapExchange(string exchange) => exchange.ToUpperInvariant() switch
    {
        "LSE" or "LON" => "XLON",
        "XETRA" or "DEX" => "XETR",
        "F" or "FRA" or "FRK" => "XFRA",
        _ => BrokerSymbol.TryLookupByVenueToken(exchange)?.Mic
    };
    private MarketDataSymbolResolution Unavailable(string error) => new(Provider, Priority, "provider unavailable", Error: error);
}