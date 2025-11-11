using FinanceManager.Components.Helpers;
using FinanceManager.Domain.Entities;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;

namespace FinanceManager.Components.HttpContexts;

public class StockPriceHttpContext(HttpClient httpClient, ILogger<StockPriceHttpContext> logger)
{
    public async Task AddStockPrice(string ticker, decimal pricePerUnit, int currencyId, DateTime date)
    {
        var response = await httpClient.PostAsync($"{httpClient.BaseAddress}api/StockPrice/add-stock-price?ticker={ticker}&pricePerUnit={pricePerUnit}&currencyId={currencyId}&date={date.ToRfc3339()}", null);
        response.EnsureSuccessStatusCode();
    }
    public async Task UpdateStockPrice(string ticker, decimal pricePerUnit, int currencyId, DateTime date)
    {
        var response = await httpClient.PostAsync($"{httpClient.BaseAddress}api/StockPrice/update-stock-price?ticker={ticker}&pricePerUnit={pricePerUnit}&currencyId={currencyId}&date={date.ToRfc3339()}", null);
        response.EnsureSuccessStatusCode();
    }
    public async Task<StockPrice?> GetStockPrice(string ticker, int currencyId, DateTime date)
    {
        if (httpClient is null) return default;
        try
        {
            var result = await httpClient.GetFromJsonAsync<StockPrice?>($"{httpClient.BaseAddress}api/StockPrice/get-stock-price?ticker={ticker.ToUpper()}&currencyId={currencyId}&date={date.ToRfc3339()}&step=1");

            if (result is not null) return result;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, ex.Message);
            Console.WriteLine($"Error fetching stock price: {ex.Message}");
        }
        return default;

    }
    public async Task<IEnumerable<StockPrice>> GetStockPrices(string ticker, DateTime start, DateTime end, TimeSpan step)
    {
        if (httpClient is null) return [];

        try
        {
            var result = await httpClient.GetFromJsonAsync<IEnumerable<StockPrice>>($"{httpClient.BaseAddress}api/StockPrice/get-stock-prices?ticker={ticker.ToUpper()}&start={start.ToRfc3339()}&end={end.ToRfc3339()}&step={step.Ticks}");
            if (result is not null) return result;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, ex.Message);
        }

        return [];
    }
    public async Task<DateTime?> GetLatestMissingStockPrice(string ticker)
    {
        if (httpClient is null) return default;

        var result = await httpClient.GetFromJsonAsync<DateTime?>($"{httpClient.BaseAddress}api/StockPrice/get-latest-missing-stock-price/?ticker={ticker.ToUpper()}");

        if (result is not null) return result;
        return default;
    }
    public async Task<TickerCurrency?> GetTickerCurrency(string ticker)
    {
        if (httpClient is null) return default;
        try
        {
            return await httpClient.GetFromJsonAsync<TickerCurrency>($"{httpClient.BaseAddress}api/StockPrice/get-ticker-currency?ticker={ticker.ToUpper()}");
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, ex.Message);
            return null;
        }
    }
}