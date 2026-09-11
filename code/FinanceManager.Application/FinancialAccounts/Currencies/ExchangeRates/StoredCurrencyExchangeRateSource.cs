using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Repositories;
using FinanceManager.Domain.FinancialAccounts.Currencies.Services;

namespace FinanceManager.Application.FinancialAccounts.Currencies.ExchangeRates;

/// <summary>Resolves exact rates from the database before delegating misses to providers.</summary>
internal sealed class StoredCurrencyExchangeRateSource(
    IExchangeRateRepository repository,
    ICurrencyExchangeRateSource inner) : ICurrencyExchangeRateSource
{
    // The source is scoped, so this lock serialises overlapping range fills within one request
    // scope and lets the next caller observe rates persisted by the previous one.
    private readonly SemaphoreSlim _rangeSemaphore = new(1, 1);

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

        await _rangeSemaphore.WaitAsync(ct);
        try
        {
            return await ResolveCoreAsync(
                fromCurrency,
                toCurrency,
                orderedDates,
                reuseCachedMisses,
                ct,
                context);
        }
        finally
        {
            _rangeSemaphore.Release();
        }
    }

    private async Task<IReadOnlyDictionary<DateTime, CurrencyExchangeRateResolution>> ResolveCoreAsync(
        Currency fromCurrency,
        Currency toCurrency,
        IReadOnlyList<DateTime> orderedDates,
        bool reuseCachedMisses,
        CancellationToken ct,
        CurrencyExchangeRateResolutionContext context)
    {
        Dictionary<DateTime, CurrencyExchangeRateResolution> results = [];
        var isPointMode = orderedDates.Count == 1 && reuseCachedMisses;
        if (isPointMode)
        {
            var date = orderedDates[0];
            var direct = await repository.Get(fromCurrency.ShortName, toCurrency.ShortName, date, ct);
            if (direct is decimal directRate)
            {
                results[date] = Stored(directRate);
            }
            else
            {
                var inverse = await repository.Get(toCurrency.ShortName, fromCurrency.ShortName, date, ct);
                if (inverse is decimal inverseRate && inverseRate != 0)
                    results[date] = Stored(1m / inverseRate);
            }
        }
        else
        {
            var stored = await repository.GetRange(
                fromCurrency.ShortName,
                toCurrency.ShortName,
                orderedDates[0],
                orderedDates[^1],
                ct);
            var normalizedFrom = NormalizeCurrency(fromCurrency.ShortName);
            var normalizedTo = NormalizeCurrency(toCurrency.ShortName);
            foreach (var date in orderedDates)
            {
                if (stored.TryGetValue((normalizedFrom, normalizedTo, date), out var rate))
                    results[date] = Stored(rate);
            }
        }

        var missingDates = orderedDates.Where(date => !results.ContainsKey(date)).ToList();
        if (missingDates.Count == 0)
            return results;

        var delegated = await inner.ResolveAsync(fromCurrency, toCurrency, missingDates, reuseCachedMisses, ct, context);
        foreach (var (date, resolution) in delegated)
            results[date] = resolution;

        var ratesToPersist = delegated
            .Where(pair => pair.Value.Source == CurrencyExchangeRateSource.Provider && pair.Value.Result.IsSuccess)
            .Select(pair => (pair.Key, pair.Value.Result.Value!.Value))
            .ToList();
        if (ratesToPersist.Count > 0)
        {
            if (isPointMode && ratesToPersist.Count == 1)
                await repository.Add(fromCurrency.ShortName, toCurrency.ShortName, ratesToPersist[0].Item1, ratesToPersist[0].Item2, ct);
            else
                await repository.AddRange(fromCurrency.ShortName, toCurrency.ShortName, ratesToPersist, ct);
        }

        return results;
    }

    private static CurrencyExchangeRateResolution Stored(decimal rate) =>
        new(CurrencyExchangeRateResult.Success(rate), CurrencyExchangeRateSource.Stored);

    private static string NormalizeCurrency(string currency) =>
        currency.Trim().ToUpperInvariant();

    private static DateTime NormalizeDate(DateTime date) =>
        DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);
}