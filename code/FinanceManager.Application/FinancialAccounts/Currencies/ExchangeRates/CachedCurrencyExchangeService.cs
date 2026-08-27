using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Services;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Services;
using FinanceManager.Domain.Identity.Services;
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

    // Failed lookups are cached too, with a shorter TTL: a pair/date no provider knows would
    // otherwise re-run the full provider chain (DB + external HTTP) on every call, and a chart
    // request can ask for the same unknown rate hundreds of times.
    private static readonly MemoryCacheEntryOptions _missCacheOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15)
    };

    public async Task<decimal?> GetExchangeRateAsync(Currency fromCurrency, Currency toCurrency, DateTime date) =>
        (await GetExchangeRateResultAsync(fromCurrency, toCurrency, date)).Value;

    public async Task<CurrencyExchangeRateResult> GetExchangeRateResultAsync(Currency fromCurrency, Currency toCurrency, DateTime date)
    {
        var key = $"EXCHANGE_RATE_RESULT_{fromCurrency.ShortName}_{toCurrency.ShortName}_{date:yyyyMMdd}";

        if (cache.TryGetValue(key, out CurrencyExchangeRateResult? cached) && cached is not null)
            return cached;

        var result = await inner.GetExchangeRateResultAsync(fromCurrency, toCurrency, date);
        cache.Set(key, result, result.Status == CurrencyExchangeRateStatus.Success ? _cacheOptions : _missCacheOptions);

        return result;
    }

    public async Task<List<(DateTime Date, decimal? Value)>> GetExchangeRateAsync(Currency fromCurrency, Currency toCurrency, DateTime dateStart, DateTime dateEnd)
    {
        var rates = await inner.GetExchangeRateAsync(fromCurrency, toCurrency, dateStart, dateEnd);

        foreach (var (date, value) in rates)
        {
            if (value is not null)
            {
                var key = $"EXCHANGE_RATE_{fromCurrency.ShortName}_{toCurrency.ShortName}_{date:yyyyMMdd}";
                cache.Set(key, value, _cacheOptions);
            }
        }

        return rates;
    }
}