using FinanceManager.Components.Helpers;
using FinanceManager.Domain.Entities;
using System.Net.Http.Json;

namespace FinanceManager.Components.HttpContexts;

public class StockPriceHttpContext(HttpClient httpClient)
{
    private readonly HttpClient _httpClient = httpClient;

    public async Task AddStockPrice(string ticker, decimal pricePerUnit, string currency, DateTime date)
    {
        var response = await _httpClient.PostAsync($"{_httpClient.BaseAddress}api/StockPrice/add-stock-price?ticker={ticker}&pricePerUnit={pricePerUnit}&currency={currency}&date={date.ToRfc3339()}&", null);
        response.EnsureSuccessStatusCode();
    }
    public async Task UpdateStockPrice(string ticker, decimal pricePerUnit, string currency, DateTime date)
    {
        var response = await _httpClient.PostAsync($"{_httpClient.BaseAddress}api/StockPrice/update-stock-price?ticker={ticker}&pricePerUnit={pricePerUnit}&currency={currency}&date={date.ToRfc3339()}&", null);
        response.EnsureSuccessStatusCode();
    }
    public async Task<StockPrice?> GetStockPrice(string ticker, string currency, DateTime date)
    {
        if (_httpClient is null) return default;
        try
        {
            var result = await _httpClient.GetFromJsonAsync<StockPrice?>($"{_httpClient.BaseAddress}api/StockPrice/get-stock-price/?ticker={ticker.ToUpper()}&currency={currency.ToUpper()}&date={date.ToRfc3339()}");

            if (result is not null) return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fetching stock price: {ex.Message}");
        }
        return default;

    }
    public async Task<IEnumerable<StockPrice>> GetStockPrices(string ticker, DateTime start, DateTime end, TimeSpan step)
    {
        if (_httpClient is null) return [];

        try
        {
            var result = await _httpClient.GetFromJsonAsync<IEnumerable<StockPrice>>($"{_httpClient.BaseAddress}api/StockPrice/get-stock-prices/?ticker={ticker.ToUpper()}&start={start.ToRfc3339()}&end={end.ToRfc3339()}&step={step.Ticks}");
            if (result is not null) return result;
        }
        catch (Exception ex)
        {

        }

        return [];
    }
    public async Task<DateTime?> GetLatestMissingStockPrice(string ticker)
    {
        if (_httpClient is null) return default;

        var result = await _httpClient.GetFromJsonAsync<DateTime?>($"{_httpClient.BaseAddress}api/StockPrice/get-latest-missing-stock-price/?ticker={ticker.ToUpper()}");

        if (result is not null) return result;
        return default;
    }
    public async Task<TickerCurrency?> GetTickerCurrency(string ticker)
    {
        if (_httpClient is null) return default;
        try
        {
            return await _httpClient.GetFromJsonAsync<TickerCurrency>($"{_httpClient.BaseAddress}api/StockPrice/get-ticker-currency/?ticker={ticker.ToUpper()}");

        }
        catch (Exception)
        {
            return null;
        }
    }
}
