using FinanceManager.Components.Helpers;
using FinanceManager.Domain.Entities.Stocks;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;

namespace FinanceManager.Components.HttpClients;

public class StockPriceHttpClient(HttpClient httpClient, ILogger<StockPriceHttpClient> logger)
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

    public async Task<List<StockDetails>> GetStocks(CancellationToken cancellationToken = default)
    {
        var result = await httpClient.GetFromJsonAsync<List<StockDetails>>($"{httpClient.BaseAddress}api/StockPrice/get-stocks-details", cancellationToken);
        return result ?? [];
    }

    public async Task<StockDetails?> AddStockDetails(string ticker, string name, string type, string region, string currency, CancellationToken cancellationToken = default)
    {
        var request = new { Ticker = ticker, Name = name, Type = type, Region = region, Currency = currency };
        var response = await httpClient.PostAsJsonAsync($"{httpClient.BaseAddress}api/StockPrice/add-stock-details", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<StockDetails>(cancellationToken: cancellationToken);
    }

    public async Task<StockDetails?> GetStockDetails(string ticker, CancellationToken cancellationToken = default)
    {
        return await httpClient.GetFromJsonAsync<StockDetails>($"{httpClient.BaseAddress}api/StockPrice/get-stock-details/{Uri.EscapeDataString(ticker)}", cancellationToken);
    }

    public async Task<StockDetails?> UpdateStockDetails(string ticker, string name, string type, string region, string currency, CancellationToken cancellationToken = default)
    {
        var request = new { Ticker = ticker, Name = name, Type = type, Region = region, Currency = currency };
        var response = await httpClient.PutAsJsonAsync($"{httpClient.BaseAddress}api/StockPrice/update-stock-details", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<StockDetails>(cancellationToken: cancellationToken);
    }

    public async Task<bool> DeleteStockPrice(int id, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await httpClient.DeleteAsync($"{httpClient.BaseAddress}api/StockPrice/delete-stock-price/{id}", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, ex.Message);
            return false;
        }
    }

    public async Task<bool> DeleteStock(string ticker, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await httpClient.DeleteAsync($"{httpClient.BaseAddress}api/StockPrice/delete-stock/{Uri.EscapeDataString(ticker)}", cancellationToken);
            response.EnsureSuccessStatusCode();
            return true;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, ex.Message);
            return false;
        }
    }
}