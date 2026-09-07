using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Services;
using Microsoft.Extensions.Caching.Memory;

namespace FinanceManager.Application.FinancialAccounts.Currencies.ExchangeRates;

internal sealed class CachedCurrencyExchangeService(
    ICurrencyExchangeService inner,
    IMemoryCache cache) : ICurrencyExchangeService, ICurrencyExchangeRateResolutionService
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

    public async Task<CurrencyExchangeRateResult> GetExchangeRateResultAsync(Currency fromCurrency, Currency toCurrency, DateTime date) =>
        (await GetExchangeRateWithSourceAsync(fromCurrency, toCurrency, date)).Result;

    public async Task<CurrencyExchangeRateResolution> GetExchangeRateWithSourceAsync(
        Currency fromCurrency,
        Currency toCurrency,
        DateTime date)
    {
        var key = GetCacheKey(fromCurrency, toCurrency, date);
        if (cache.TryGetValue(key, out CurrencyExchangeRateResolution? cached) && cached is not null)
            return cached;

        var resolved = inner is ICurrencyExchangeRateResolutionService resolver
            ? await resolver.GetExchangeRateWithSourceAsync(fromCurrency, toCurrency, date)
            : new CurrencyExchangeRateResolution(
                await inner.GetExchangeRateResultAsync(fromCurrency, toCurrency, date),
                CurrencyExchangeRateSource.Unknown);
        cache.Set(key, resolved, resolved.Result.IsSuccess ? _cacheOptions : _missCacheOptions);
        return resolved;
    }

    public async Task<List<(DateTime Date, decimal? Value)>> GetExchangeRateAsync(
        Currency fromCurrency,
        Currency toCurrency,
        DateTime dateStart,
        DateTime dateEnd) =>
        (await GetExchangeRateRangeWithProvenanceAsync(fromCurrency, toCurrency, dateStart, dateEnd))
            .Select(rate => (rate.Date, rate.Value)).ToList();

    public async Task<List<(DateTime Date, decimal? Value, CurrencyExchangeRateSource Source)>> GetExchangeRateRangeWithProvenanceAsync(
        Currency fromCurrency,
        Currency toCurrency,
        DateTime dateStart,
        DateTime dateEnd,
        IReadOnlyDictionary<DateTime, CurrencyExchangeRateResolution>? cachedRates = null)
    {
        if (inner is not ICurrencyExchangeRateResolutionService resolver)
        {
            var rates = await inner.GetExchangeRateAsync(fromCurrency, toCurrency, dateStart, dateEnd);
            return rates.Select(r => (r.Date, r.Value, CurrencyExchangeRateSource.Unknown)).ToList();
        }

        if (dateStart == default || dateEnd == default) return [];
        var start = dateStart.Date;
        var end = dateEnd.Date;
        if (start > end) (start, end) = (end, start);
        if (end > DateTime.UtcNow.Date) end = DateTime.UtcNow.Date;
        if (start > end) return [];

        Dictionary<DateTime, CurrencyExchangeRateResolution> knownRates = [];
        for (var date = start; date <= end; date = date.AddDays(1))
        {
            if (cachedRates is not null && cachedRates.TryGetValue(date, out var supplied) && supplied.CanReuseForRange)
                knownRates[date] = supplied;
            else if (cache.TryGetValue(GetCacheKey(fromCurrency, toCurrency, date), out CurrencyExchangeRateResolution? cached)
                && cached is not null && cached.CanReuseForRange)
                knownRates[date] = cached;
        }

        // One call preserves the provider budget and carry-forward context across cache gaps.
        var rangeResults = await resolver.GetExchangeRateRangeWithProvenanceAsync(
            fromCurrency, toCurrency, start, end, knownRates);
        foreach (var (date, value, source) in rangeResults)
        {
            if (knownRates.ContainsKey(date) || value is not decimal rate) continue;
            var resolved = new CurrencyExchangeRateResolution(CurrencyExchangeRateResult.Success(rate), source);
            if (resolved.CanReuseForRange)
                cache.Set(GetCacheKey(fromCurrency, toCurrency, date), resolved, _cacheOptions);
        }

        return rangeResults;
    }

    private static string GetCacheKey(Currency fromCurrency, Currency toCurrency, DateTime date) =>
        $"EXCHANGE_RATE_RESULT_{NormalizeCurrency(fromCurrency.ShortName)}_{NormalizeCurrency(toCurrency.ShortName)}_{date:yyyyMMdd}";

    private static string NormalizeCurrency(string? currency) =>
        string.IsNullOrWhiteSpace(currency) ? string.Empty : currency.Trim().ToUpperInvariant();
}