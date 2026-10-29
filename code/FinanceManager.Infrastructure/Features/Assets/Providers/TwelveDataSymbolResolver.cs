using FinanceManager.Application.FinancialAccounts.Investments.Discovery;
using FinanceManager.Application.FinancialAccounts.Stock.Pricing;
using FinanceManager.Application.Shared.ExternalServices;
using FinanceManager.Application.Shared.Options;
using FinanceManager.Domain.Assets.Discovery;
using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text.Json;

namespace FinanceManager.Infrastructure.Features.Assets.Providers;

internal sealed class TwelveDataSymbolResolver(
    HttpClient httpClient,
    IExternalServiceConfigService configService,
    IOptions<TwelveDataOptions> options,
    TwelveDataCreditBudget budget,
    TwelveDataClient priceClient) : IMarketDataSymbolResolver
{
    public MarketDataProvider Provider => MarketDataProvider.TwelveData;
    public int Priority => options.Value.Priority;

    public async Task<MarketDataSymbolResolution> ResolveAsync(InstrumentDiscoveryResultDto instrument, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(instrument.Ticker)
            || string.IsNullOrWhiteSpace(instrument.ExchangeMic)
            || string.IsNullOrWhiteSpace(instrument.TradingCurrency))
            return new(Provider, Priority, "not found");

        var config = await configService.GetServiceAsync("TwelveData", ct);
        if (!options.Value.Enabled || !config.IsEnabled || string.IsNullOrWhiteSpace(config.ApiKey)) return Unavailable("Twelve Data is disabled or unconfigured.");
        if (!budget.TryConsume()) return Unavailable("Twelve Data request budget is exhausted.");
        var url = $"{config.BaseUrl.TrimEnd('/')}/symbol_search?symbol={Uri.EscapeDataString(instrument.Ticker)}";
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("apikey", config.ApiKey);
            using var response = await httpClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode) return Unavailable("Twelve Data search failed.");
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            if (!json.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
                return json.RootElement.TryGetProperty("status", out var status) && status.GetString() == "error" ? Unavailable("Twelve Data rejected the search.") : Unavailable("Twelve Data returned an invalid search response.");
            foreach (var match in data.EnumerateArray())
            {
                var symbol = Text(match, "symbol");
                var mic = Text(match, "mic_code");
                var currency = Text(match, "currency");
                if (!string.Equals(symbol, instrument.Ticker, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(mic, instrument.ExchangeMic, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(currency, instrument.TradingCurrency, StringComparison.OrdinalIgnoreCase)) continue;
                var exchange = Text(match, "exchange");
                return new(Provider, Priority, "candidate", $"{symbol}:{exchange}", exchange, currency,
                    Text(match, "instrument_name"), Text(match, "instrument_type"), exchange);
            }
            return new(Provider, Priority, "not found");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (OperationCanceledException) { return Unavailable("Twelve Data search timed out."); }
        catch (HttpRequestException) { return Unavailable("Twelve Data search failed."); }
        catch (JsonException) { return Unavailable("Twelve Data returned an invalid search response."); }
    }

    public async Task<bool> ValidateAsync(MarketDataSymbolResolution candidate, CancellationToken ct = default)
    {
        if (candidate.Provider != Provider || candidate.Status != "candidate" || string.IsNullOrWhiteSpace(candidate.Symbol) || string.IsNullOrWhiteSpace(candidate.Currency)) return false;
        return (await priceClient.GetDailySeries(candidate.Symbol, string.Empty, DateTime.UtcNow.Date.AddDays(-10), DateTime.UtcNow.Date,
            new Currency(0, candidate.Currency, string.Empty), ct)).Any(price => price.PricePerUnit > 0);
    }

    private static string Text(JsonElement element, string property) => element.TryGetProperty(property, out var value) ? value.GetString() ?? string.Empty : string.Empty;
    private MarketDataSymbolResolution Unavailable(string error) => new(Provider, Priority, "provider unavailable", Error: error);
}