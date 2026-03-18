using CsvHelper;
using CsvHelper.Configuration;
using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Entities.Stocks;
using FinanceManager.Domain.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System.Collections.Concurrent;
using System.Globalization;

namespace FinanceManager.Application.Services;

internal class CurrencyExchangeService(
    HttpClient httpClient,
    ILogger<CurrencyExchangeService> logger,
    IConfiguration configuration) : ICurrencyExchangeService
{
    private static readonly ConcurrentDictionary<string, List<(DateTime Date, decimal Close)>> _csvCache = new();

    public async Task<decimal?> GetExchangeRateAsync(Currency fromCurrency, Currency toCurrency, DateTime date)
    {
        try
        {
            var response = await httpClient.GetAsync($"https://cdn.jsdelivr.net/npm/@fawazahmed0/currency-api@{date:yyyy-MM-dd}/v1/currencies/{fromCurrency.ShortName.ToLower()}.json");
            var jObject = JObject.Parse(await response.Content.ReadAsStringAsync());
            var tokenPath = $"$.{fromCurrency.ShortName.ToLower()}.{toCurrency.ShortName.ToLower()}";

            var rate = (decimal?)jObject.SelectToken(tokenPath);
            if (rate is not null) return rate;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get exchange rate from {FromCurrency} to {ToCurrency} on {Date}", fromCurrency, toCurrency, date);
        }

        return await GetRateFromCsvAsync(fromCurrency.ShortName, toCurrency.ShortName, date);
    }

    private async Task<decimal?> GetRateFromCsvAsync(string from, string to, DateTime date)
    {
        var csvDirectory = configuration["CurrencyExchangeRates:CsvDirectory"];
        if (string.IsNullOrWhiteSpace(csvDirectory))
            return null;

        var direct = await TryLoadCsvAsync(csvDirectory, from, to);
        if (direct is not null)
        {
            var entry = direct.FirstOrDefault(x => x.Date.Date <= date.Date);
            if (entry != default) return entry.Close;
        }

        var inverse = await TryLoadCsvAsync(csvDirectory, to, from);
        if (inverse is not null)
        {
            var entry = inverse.FirstOrDefault(x => x.Date.Date <= date.Date);
            if (entry != default && entry.Close != 0) return 1m / entry.Close;
        }

        return null;
    }

    private async Task<List<(DateTime Date, decimal Close)>?> TryLoadCsvAsync(string directory, string from, string to)
    {
        var filePath = Path.Combine(directory, $"{from.ToLowerInvariant()}{to.ToLowerInvariant()}_d.csv");
        if (!File.Exists(filePath))
            return null;

        if (_csvCache.TryGetValue(filePath, out var cached))
            return cached;

        try
        {
            var rows = await LoadCsvRowsAsync(filePath);
            _csvCache[filePath] = rows;
            return rows;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load CSV exchange rate file {FilePath}", filePath);
            return null;
        }
    }

    private static async Task<List<(DateTime Date, decimal Close)>> LoadCsvRowsAsync(string filePath)
    {
        using var reader = new StreamReader(filePath);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            BadDataFound = null,
            MissingFieldFound = null,
        });

        await csv.ReadAsync();
        csv.ReadHeader();

        var rows = new List<(DateTime Date, decimal Close)>();
        while (await csv.ReadAsync())
        {
            if (csv.TryGetField<DateTime>("Date", out var d) && csv.TryGetField<decimal>("Close", out var c))
                rows.Add((d, c));
        }

        rows.Sort((a, b) => b.Date.CompareTo(a.Date));
        return rows;
    }

    public async Task<decimal?> GetPricePerUnit(StockPrice stockPrice, Currency currency, DateTime date)
    {
        if (stockPrice is null) return null;
        if (stockPrice.Currency == currency) return stockPrice.PricePerUnit;
        if (date > DateTime.UtcNow) date = DateTime.UtcNow;
        var priceInRightCurrency = await GetExchangeRateAsync(stockPrice.Currency, currency, date.Date);
        if (priceInRightCurrency is not null)
            return stockPrice.PricePerUnit * priceInRightCurrency.Value;

        return null;
    }
}