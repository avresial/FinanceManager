using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Entities.Stocks;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.Services;
using Microsoft.Extensions.Caching.Memory;

namespace FinanceManager.Application.FinancialAccounts.Currencies.ExchangeRates;

internal sealed class CachedCurrencyExchangeService(
    ICurrencyExchangeService inner,
    IMemoryCache cache) : ICurrencyExchangeService
{
    private static readonly MemoryCacheEntryOptions _cacheOptions = new()
    {
        SlidingExpiration = TimeSpan.FromHours(1)
    };

    public async Task<decimal?> GetExchangeRateAsync(Currency fromCurrency, Currency toCurrency, DateTime date)
    {
        var key = $"EXCHANGE_RATE_{fromCurrency.ShortName}_{toCurrency.ShortName}_{date:yyyyMMdd}";

        if (cache.TryGetValue(key, out decimal? cached))
            return cached;

        var rate = await inner.GetExchangeRateAsync(fromCurrency, toCurrency, date);

        if (rate is not null)
            cache.Set(key, rate, _cacheOptions);

        return rate;
    }

    public async Task<List<(DateTime Date, decimal? Value)>> GetExchangeRateAsync(Currency fromCurrency, Currency toCurrency, DateTime dateStart, DateTime dateEnd)
    {
        if (dateStart == default || dateEnd == default) return [];

        var start = dateStart.Date;
        var end = dateEnd.Date;
        if (start > end) (start, end) = (end, start);
        if (end > DateTime.UtcNow.Date) end = DateTime.UtcNow.Date;

        var totalDays = (end - start).Days + 1;
        if (totalDays <= 0) return [];

        if (fromCurrency == toCurrency)
        {
            List<(DateTime Date, decimal? Value)> sameCurrencyRates = new(totalDays);
            for (var i = 0; i < totalDays; i++)
                sameCurrencyRates.Add((start.AddDays(i), 1m));
            return sameCurrencyRates;
        }

        const int batchSize = 50;
        List<(DateTime Date, decimal? Value)> rates = new(totalDays);

        for (var offset = 0; offset < totalDays; offset += batchSize)
        {
            var currentBatchSize = Math.Min(batchSize, totalDays - offset);
            List<DateTime> batchDates = new(currentBatchSize);
            List<Task<decimal?>> batchTasks = new(currentBatchSize);

            for (var i = 0; i < currentBatchSize; i++)
            {
                var date = start.AddDays(offset + i);
                batchDates.Add(date);
                batchTasks.Add(GetExchangeRateAsync(fromCurrency, toCurrency, date));
            }

            var batchResults = await Task.WhenAll(batchTasks);
            for (var i = 0; i < batchResults.Length; i++)
                rates.Add((batchDates[i], batchResults[i]));
        }

        return rates;
    }

    public async Task<decimal?> GetPricePerUnit(StockPrice stockPrice, Currency currency, DateTime date)
        => await inner.GetPricePerUnit(stockPrice, currency, date);
}