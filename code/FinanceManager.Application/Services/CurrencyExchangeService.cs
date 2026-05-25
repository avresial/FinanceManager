using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Entities.Stocks;
using FinanceManager.Domain.Services;

namespace FinanceManager.Application.Services;

internal class CurrencyExchangeService(
    IEnumerable<ICurrencyExchangeRateProvider> providers) : ICurrencyExchangeService
{
    public async Task<List<(DateTime Date, decimal? Value)>> GetExchangeRateAsync(Currency fromCurrency, Currency toCurrency, DateTime dateStart, DateTime dateEnd)
    {
        if (dateStart == default || dateEnd == default) return [];

        var start = dateStart.Date;
        var end = dateEnd.Date;

        if (start > end)
            (start, end) = (end, start);

        if (end > DateTime.UtcNow.Date)
            end = DateTime.UtcNow.Date;

        var totalDays = (end - start).Days + 1;
        if (totalDays <= 0) return [];

        if (fromCurrency == toCurrency)
        {
            List<(DateTime Date, decimal? Value)> sameCurrencyRates = [];
            for (var i = 0; i < totalDays; i++)
                sameCurrencyRates.Add((start.AddDays(i), 1m));

            return sameCurrencyRates;
        }

        const int batchSize = 50;
        List<(DateTime Date, decimal? Value)> rates = [];

        for (var offset = 0; offset < totalDays; offset += batchSize)
        {
            var currentBatchSize = Math.Min(batchSize, totalDays - offset);
            List<DateTime> batchDates = [];
            List<Task<decimal?>> batchTasks = [];

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

    public async Task<decimal?> GetExchangeRateAsync(Currency fromCurrency, Currency toCurrency, DateTime date)
    {
        foreach (var provider in providers)
        {
            var rate = await provider.GetExchangeRateAsync(fromCurrency, toCurrency, date);
            if (rate is not null) return rate;
        }

        return null;
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