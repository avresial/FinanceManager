using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Services;
using Microsoft.Extensions.Caching.Memory;

namespace FinanceManager.Application.FinancialAccounts.Currencies.ExchangeRates;

internal sealed class CachedCurrencyExchangeService(
    ICurrencyExchangeService inner,
    IMemoryCache cache) : ICurrencyExchangeService, ICurrencyExchangeRateRangeService
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
        var key = GetCacheKey(fromCurrency, toCurrency, date);

        if (cache.TryGetValue(key, out CurrencyExchangeRateResult? cached) && cached is not null)
            return cached;

        var result = await inner.GetExchangeRateResultAsync(fromCurrency, toCurrency, date);
        cache.Set(key, result, result.Status == CurrencyExchangeRateStatus.Success ? _cacheOptions : _missCacheOptions);

        return result;
    }

    public async Task<List<(DateTime Date, decimal? Value)>> GetExchangeRateAsync(
        Currency fromCurrency,
        Currency toCurrency,
        DateTime dateStart,
        DateTime dateEnd)
    {
        if (inner is ICurrencyExchangeRateRangeService rangeService)
        {
            var rangeResults = await rangeService.GetExchangeRateRangeWithProvenanceAsync(fromCurrency, toCurrency, dateStart, dateEnd);
            CacheAuthoritativeRates(fromCurrency, toCurrency, rangeResults);
            return rangeResults.Select(r => (r.Date, r.Value)).ToList();
        }

        return await inner.GetExchangeRateAsync(fromCurrency, toCurrency, dateStart, dateEnd);
    }

    public async Task<List<(DateTime Date, decimal? Value, bool IsAuthoritative)>> GetExchangeRateRangeWithProvenanceAsync(
        Currency fromCurrency,
        Currency toCurrency,
        DateTime dateStart,
        DateTime dateEnd)
    {
        if (inner is ICurrencyExchangeRateRangeService rangeService)
        {
            var rangeResults = await rangeService.GetExchangeRateRangeWithProvenanceAsync(fromCurrency, toCurrency, dateStart, dateEnd);
            CacheAuthoritativeRates(fromCurrency, toCurrency, rangeResults);
            return rangeResults;
        }

        var rates = await inner.GetExchangeRateAsync(fromCurrency, toCurrency, dateStart, dateEnd);
        return rates.Select(r => (r.Date, r.Value, false)).ToList();
    }

    private void CacheAuthoritativeRates(
        Currency fromCurrency,
        Currency toCurrency,
        IEnumerable<(DateTime Date, decimal? Value, bool IsAuthoritative)> rates)
    {
        foreach (var (date, value, isAuthoritative) in rates)
        {
            if (isAuthoritative && value is decimal rate)
            {
                var key = GetCacheKey(fromCurrency, toCurrency, date);
                cache.Set(key, CurrencyExchangeRateResult.Success(rate), _cacheOptions);
            }
        }
    }

    private static string GetCacheKey(Currency fromCurrency, Currency toCurrency, DateTime date) =>
        $"EXCHANGE_RATE_RESULT_{NormalizeCurrency(fromCurrency.ShortName)}_{NormalizeCurrency(toCurrency.ShortName)}_{date:yyyyMMdd}";

    private static string NormalizeCurrency(string? currency) =>
        string.IsNullOrWhiteSpace(currency) ? string.Empty : currency.Trim().ToUpperInvariant();
}