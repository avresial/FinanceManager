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

    public Task<CurrencyExchangeRateProviderResult> GetExchangeRateAsync(
        Currency fromCurrency,
        Currency toCurrency,
        DateTime date) =>
        GetExchangeRateCoreAsync(fromCurrency, toCurrency, date, CancellationToken.None);

    Task<CurrencyExchangeRateProviderResult> ICurrencyExchangeRateProvider.GetExchangeRateAsync(
        Currency fromCurrency,
        Currency toCurrency,
        DateTime date,
        CancellationToken cancellationToken) =>
        GetExchangeRateCoreAsync(fromCurrency, toCurrency, date, cancellationToken);

    private async Task<CurrencyExchangeRateProviderResult> GetExchangeRateCoreAsync(
        Currency fromCurrency,
        Currency toCurrency,
        DateTime date,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var csvDirectory = configuration["CurrencyExchangeRates:CsvDirectory"];
            if (string.IsNullOrWhiteSpace(csvDirectory))
                return new(CurrencyExchangeRateProviderStatus.NotFound);

            var direct = await TryLoadCsvAsync(csvDirectory, fromCurrency.ShortName, toCurrency.ShortName, cancellationToken);
            if (direct is not null)
            {
                var entry = direct.FirstOrDefault(x => x.Date.Date <= date.Date);
                if (entry != default)
                    return new(CurrencyExchangeRateProviderStatus.Success, entry.Close);
            }

            var inverse = await TryLoadCsvAsync(csvDirectory, toCurrency.ShortName, fromCurrency.ShortName, cancellationToken);
            if (inverse is not null)
            {
                var entry = inverse.FirstOrDefault(x => x.Date.Date <= date.Date);
                if (entry != default && entry.Close != 0)
                    return new(CurrencyExchangeRateProviderStatus.Success, 1m / entry.Close);
            }

            return new(CurrencyExchangeRateProviderStatus.NotFound);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
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

    public Task<List<(DateTime Date, CurrencyExchangeRateProviderResult Result)>> GetExchangeRateAsync(
        Currency fromCurrency,
        Currency toCurrency,
        DateTime dateStart,
        DateTime dateEnd) =>
        GetExchangeRateRangeCoreAsync(fromCurrency, toCurrency, dateStart, dateEnd, CancellationToken.None);

    Task<List<(DateTime Date, CurrencyExchangeRateProviderResult Result)>> ICurrencyExchangeRateProvider.GetExchangeRateAsync(
        Currency fromCurrency,
        Currency toCurrency,
        DateTime dateStart,
        DateTime dateEnd,
        CancellationToken cancellationToken) =>
        GetExchangeRateRangeCoreAsync(fromCurrency, toCurrency, dateStart, dateEnd, cancellationToken);

    private async Task<List<(DateTime Date, CurrencyExchangeRateProviderResult Result)>> GetExchangeRateRangeCoreAsync(
        Currency fromCurrency,
        Currency toCurrency,
        DateTime dateStart,
        DateTime dateEnd,
        CancellationToken cancellationToken)
    {
        var start = dateStart.Date;
        var end = dateEnd.Date;
        var totalDays = (end - start).Days + 1;
        if (totalDays <= 0) return [];

        List<(DateTime Date, CurrencyExchangeRateProviderResult Result)> rates = [];
        for (var i = 0; i < totalDays; i++)
        {
            var date = start.AddDays(i);
            var rate = await GetExchangeRateCoreAsync(fromCurrency, toCurrency, date, cancellationToken);
            rates.Add((date, rate));
        }

        return rates;
    }

    private async Task<List<(DateTime Date, decimal Close)>?> TryLoadCsvAsync(
        string directory,
        string from,
        string to,
        CancellationToken cancellationToken)
    {
        var filePath = Path.Combine(directory, $"{from.ToLowerInvariant()}{to.ToLowerInvariant()}_d.csv");
        if (!File.Exists(filePath))
            return null;

        if (_csvCache.TryGetValue(filePath, out var cached))
            return cached;

        var rows = await LoadCsvRowsAsync(filePath, cancellationToken);
        _csvCache[filePath] = rows;
        return rows;
    }

    private static async Task<List<(DateTime Date, decimal Close)>> LoadCsvRowsAsync(
        string filePath,
        CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(filePath);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            BadDataFound = null,
            MissingFieldFound = null,
        });

        cancellationToken.ThrowIfCancellationRequested();
        await csv.ReadAsync();
        csv.ReadHeader();

        var rows = new List<(DateTime Date, decimal Close)>();
        while (await csv.ReadAsync())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (csv.TryGetField<DateTime>("Date", out var d) && csv.TryGetField<decimal>("Close", out var c))
                rows.Add((d, c));
        }

        rows.Sort((a, b) => b.Date.CompareTo(a.Date));
        return rows;
    }
}