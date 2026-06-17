using FinanceManager.Application.FinancialAccounts.Stock.Resolution;
using FinanceManager.Components.Helpers;
using FinanceManager.Domain.Dtos;
using FinanceManager.Domain.FinancialAccounts.Stock.Dtos;
using FinanceManager.Domain.FinancialAccounts.Stock.Entities;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace FinanceManager.Components.HttpClients;

public class StockPriceHttpClient(HttpClient httpClient, ILogger<StockPriceHttpClient> logger)
{
    public async Task<InstrumentResolution?> SearchInstrument(string ticker, CancellationToken cancellationToken = default)
    {
        if (httpClient is null) return default;
        try
        {
            return await httpClient.GetFromJsonAsync<InstrumentResolution>($"{httpClient.BaseAddress}api/StockPrice/search-instrument?ticker={Uri.EscapeDataString(ticker)}", cancellationToken);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Error searching instrument: {Message}", ex.Message);
            return default;
        }
    }

    public async Task AddStockPrice(string isin, decimal pricePerUnit, int currencyId, DateTime date, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsync($"{httpClient.BaseAddress}api/StockPrice/add-stock-price?isin={Uri.EscapeDataString(isin)}&pricePerUnit={pricePerUnit}&currencyId={currencyId}&date={date.ToRfc3339()}", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
    public async Task UpdateStockPrice(string isin, decimal pricePerUnit, int currencyId, DateTime date, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsync($"{httpClient.BaseAddress}api/StockPrice/update-stock-price?isin={Uri.EscapeDataString(isin)}&pricePerUnit={pricePerUnit}&currencyId={currencyId}&date={date.ToRfc3339()}", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
    public async Task<StockPrice?> GetStockPrice(string isin, int currencyId, DateTime date, CancellationToken cancellationToken = default)
    {
        if (httpClient is null) return default;
        try
        {
            var result = await httpClient.GetFromJsonAsync<StockPrice?>($"{httpClient.BaseAddress}api/StockPrice/get-stock-price?isin={Uri.EscapeDataString(isin)}&currencyId={currencyId}&date={date.ToRfc3339()}", cancellationToken);

            if (result is not null) return result;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Error fetching stock price: {Message}", ex.Message);
        }
        return default;

    }
    public async Task<IEnumerable<StockPrice>> GetStockPrices(string isin, int currencyId, DateTime start, DateTime end, TimeSpan step, CancellationToken cancellationToken = default)
    {
        if (httpClient is null) return [];

        try
        {
            var result = await httpClient.GetFromJsonAsync<IEnumerable<StockPrice>>($"{httpClient.BaseAddress}api/StockPrice/get-stock-prices?isin={Uri.EscapeDataString(isin)}&currencyId={currencyId}&start={start.ToRfc3339()}&end={end.ToRfc3339()}&step={step.Ticks}", cancellationToken);
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

    public async Task<StockPriceBulkImportResultDto?> BulkImportClosePrices(IBrowserFile file, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);

        using var fileStream = file.OpenReadStream(maxAllowedSize: 30 * 1024 * 1024, cancellationToken: cancellationToken);
        using var streamContent = new StreamContent(fileStream);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue(string.IsNullOrWhiteSpace(file.ContentType) ? "text/csv" : file.ContentType);

        using var formData = new MultipartFormDataContent();
        formData.Add(streamContent, "file", file.Name);

        var response = await httpClient.PostAsync($"{httpClient.BaseAddress}api/StockPrice/bulk-import-close-prices", formData, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(error) ? "Bulk import failed." : error);
        }

        return await response.Content.ReadFromJsonAsync<StockPriceBulkImportResultDto>(cancellationToken: cancellationToken);
    }
}