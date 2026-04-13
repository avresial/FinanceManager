using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Entities.Stocks;
using FinanceManager.Domain.Services;
using Microsoft.Extensions.Caching.Memory;

namespace FinanceManager.Application.Services.Currencies;

internal sealed class CachedCurrencyExchangeService(
    ICurrencyExchangeService inner,
    IMemoryCache cache) : ICurrencyExchangeService
{
    private static readonly MemoryCacheEntryOptions CacheOptions = new()
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
            cache.Set(key, rate, CacheOptions);

        return rate;
    }

    public async Task<List<(DateTime Date, decimal? Value)>> GetExchangeRateAsync(Currency fromCurrency, Currency toCurrency, DateTime dateStart, DateTime dateEnd)
        => await inner.GetExchangeRateAsync(fromCurrency, toCurrency, dateStart, dateEnd);

    public async Task<decimal?> GetPricePerUnit(StockPrice stockPrice, Currency currency, DateTime date)
        => await inner.GetPricePerUnit(stockPrice, currency, date);
}
