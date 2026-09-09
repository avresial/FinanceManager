using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using Microsoft.Extensions.Caching.Memory;

namespace FinanceManager.Application.FinancialAccounts.Currencies.ExchangeRates;

/// <summary>Resolves exact daily rates from memory before delegating unresolved dates.</summary>
internal sealed class CachedCurrencyExchangeRateSource(
    ICurrencyExchangeRateSource inner,
    IMemoryCache cache) : ICurrencyExchangeRateSource
{
    private static readonly MemoryCacheEntryOptions _cacheOptions = new()
    {
        SlidingExpiration = TimeSpan.FromHours(1)
    };

    private static readonly MemoryCacheEntryOptions _missCacheOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15)
    };

    public async Task<IReadOnlyDictionary<DateTime, CurrencyExchangeRateResolution>> ResolveAsync(
        Currency fromCurrency,
        Currency toCurrency,
        IReadOnlyCollection<DateTime> dates,
        bool reuseCachedMisses,
        CancellationToken ct = default,
        CurrencyExchangeRateResolutionContext? context = null)
    {
        context ??= new CurrencyExchangeRateResolutionContext();
        var orderedDates = dates
            .Select(NormalizeDate)
            .Distinct()
            .OrderBy(date => date)
            .ToList();
        if (orderedDates.Count == 0)
            return new Dictionary<DateTime, CurrencyExchangeRateResolution>();

        Dictionary<DateTime, CurrencyExchangeRateResolution> results = [];
        List<DateTime> missingDates = [];
        foreach (var date in orderedDates)
        {
            if (cache.TryGetValue(GetCacheKey(fromCurrency, toCurrency, date), out CurrencyExchangeRateResolution? cached)
                && cached is not null
                && ((cached.Result.IsSuccess && IsExact(cached.Source))
                    || (reuseCachedMisses && !cached.Result.IsSuccess)))
            {
                results[date] = cached;
            }
            else
            {
                missingDates.Add(date);
            }
        }

        if (missingDates.Count == 0)
            return results;

        var delegated = await inner.ResolveAsync(fromCurrency, toCurrency, missingDates, reuseCachedMisses, ct, context);
        foreach (var (date, resolution) in delegated)
        {
            results[date] = resolution;
            if (resolution.Result.IsSuccess && IsExact(resolution.Source))
            {
                cache.Set(GetCacheKey(fromCurrency, toCurrency, date), resolution, _cacheOptions);
            }
            else if (reuseCachedMisses && !resolution.Result.IsSuccess)
            {
                cache.Set(GetCacheKey(fromCurrency, toCurrency, date), resolution, _missCacheOptions);
            }
        }

        return results;
    }

    private static string GetCacheKey(Currency fromCurrency, Currency toCurrency, DateTime date) =>
        $"EXCHANGE_RATE_RESULT_{NormalizeCurrency(fromCurrency.ShortName)}_{NormalizeCurrency(toCurrency.ShortName)}_{date:yyyyMMdd}";

    private static bool IsExact(CurrencyExchangeRateSource source) =>
        source is CurrencyExchangeRateSource.Stored
            or CurrencyExchangeRateSource.Provider
            or CurrencyExchangeRateSource.SameCurrency;

    private static string NormalizeCurrency(string? currency) =>
        string.IsNullOrWhiteSpace(currency) ? string.Empty : currency.Trim().ToUpperInvariant();

    private static DateTime NormalizeDate(DateTime date) =>
        DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);
}