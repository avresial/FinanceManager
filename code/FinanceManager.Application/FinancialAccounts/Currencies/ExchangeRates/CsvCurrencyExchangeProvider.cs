using CsvHelper;
using CsvHelper.Configuration;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Services;
using FinanceManager.Domain.FinancialAccounts.Shared.Services;
using FinanceManager.Domain.Identity.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Globalization;

namespace FinanceManager.Application.FinancialAccounts.Currencies.ExchangeRates;

internal sealed class CsvCurrencyExchangeProvider(
    IConfiguration configuration,
    ILogger<CsvCurrencyExchangeProvider> logger) : ICurrencyExchangeRateProvider
{
    private static readonly ConcurrentDictionary<string, List<(DateTime Date, decimal Close)>> _csvCache = new();

    public async Task<CurrencyExchangeRateProviderResult> GetExchangeRateAsync(Currency fromCurrency, Currency toCurrency, DateTime date)
    {
        try
        {
            var csvDirectory = configuration["CurrencyExchangeRates:CsvDirectory"];
            if (string.IsNullOrWhiteSpace(csvDirectory))
                return new(CurrencyExchangeRateProviderStatus.NotFound);

            var direct = await TryLoadCsvAsync(csvDirectory, fromCurrency.ShortName, toCurrency.ShortName);
            if (direct is not null)
            {
                var entry = direct.FirstOrDefault(x => x.Date.Date <= date.Date);
                if (entry != default)
                    return new(CurrencyExchangeRateProviderStatus.Success, entry.Close);
            }

            var inverse = await TryLoadCsvAsync(csvDirectory, toCurrency.ShortName, fromCurrency.ShortName);
            if (inverse is not null)
            {
                var entry = inverse.FirstOrDefault(x => x.Date.Date <= date.Date);
                if (entry != default && entry.Close != 0)
                    return new(CurrencyExchangeRateProviderStatus.Success, 1m / entry.Close);
            }

            return new(CurrencyExchangeRateProviderStatus.NotFound);
        }
        catch (OperationCanceledException ex)
        {
            logger.LogDebug(ex, "CSV exchange-rate lookup cancelled or timed out for {FromCurrency} to {ToCurrency} on {Date}.", fromCurrency, toCurrency, date);
            return new(CurrencyExchangeRateProviderStatus.Failed);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load CSV exchange rate for {FromCurrency} to {ToCurrency} on {Date}", fromCurrency, toCurrency, date);
            return new(CurrencyExchangeRateProviderStatus.Failed);
        }
    }

    public async Task<List<(DateTime Date, CurrencyExchangeRateProviderResult Result)>> GetExchangeRateAsync(Currency fromCurrency, Currency toCurrency, DateTime dateStart, DateTime dateEnd)
    {
        var start = dateStart.Date;
        var end = dateEnd.Date;
        var totalDays = (end - start).Days + 1;
        if (totalDays <= 0) return [];

        List<(DateTime Date, CurrencyExchangeRateProviderResult Result)> rates = [];
        for (var i = 0; i < totalDays; i++)
        {
            var date = start.AddDays(i);
            var rate = await GetExchangeRateAsync(fromCurrency, toCurrency, date);
            rates.Add((date, rate));
        }

        return rates;
    }

    private async Task<List<(DateTime Date, decimal Close)>?> TryLoadCsvAsync(string directory, string from, string to)
    {
        var filePath = Path.Combine(directory, $"{from.ToLowerInvariant()}{to.ToLowerInvariant()}_d.csv");
        if (!File.Exists(filePath))
            return null;

        if (_csvCache.TryGetValue(filePath, out var cached))
            return cached;

        var rows = await LoadCsvRowsAsync(filePath);
        _csvCache[filePath] = rows;
        return rows;
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
}