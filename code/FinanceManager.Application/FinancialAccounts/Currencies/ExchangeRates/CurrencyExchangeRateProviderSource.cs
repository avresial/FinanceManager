using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Services;

namespace FinanceManager.Application.FinancialAccounts.Currencies.ExchangeRates;

/// <summary>Resolves missing direct rates through the configured provider chain.</summary>
internal sealed class CurrencyExchangeRateProviderSource(
    IEnumerable<ICurrencyExchangeRateProvider> providers) : ICurrencyExchangeRateSource
{
    private const int _maxProviderResolutionsPerCall = 60;
    private const int _maxProviderRangeDays = 180;

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

        if (orderedDates.Count == 1 && reuseCachedMisses)
            return await ResolvePointAsync(fromCurrency, toCurrency, orderedDates[0], context, ct);

        // A range request may contain thousands of dates. Resolve only one bounded prefix and
        // leave the tail for the resolver's carry-forward policy on a later request.
        var toResolve = orderedDates.Take(_maxProviderResolutionsPerCall).ToList();
        var results = await ResolveProviderRangeAsync(fromCurrency, toCurrency, toResolve, context, ct);

        return results.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.IsSuccess
                ? new CurrencyExchangeRateResolution(pair.Value, CurrencyExchangeRateSource.Provider)
                : new CurrencyExchangeRateResolution(pair.Value, CurrencyExchangeRateSource.Unavailable));
    }

    private async Task<IReadOnlyDictionary<DateTime, CurrencyExchangeRateResolution>> ResolvePointAsync(
        Currency fromCurrency,
        Currency toCurrency,
        DateTime date,
        CurrencyExchangeRateResolutionContext context,
        CancellationToken ct)
    {
        var state = new ResolutionState();
        foreach (var provider in providers)
        {
            ct.ThrowIfCancellationRequested();
            if (context.GetOutOfRangeProviders(date).Contains(provider))
                continue;

            var result = ct.CanBeCanceled
                ? await provider.GetExchangeRateAsync(fromCurrency, toCurrency, date, ct)
                : await provider.GetExchangeRateAsync(fromCurrency, toCurrency, date);
            if (result.Status == CurrencyExchangeRateProviderStatus.OutOfRange)
            {
                context.GetOutOfRangeProviders(date).Add(provider);
                continue;
            }

            if (result is { Status: CurrencyExchangeRateProviderStatus.Success, Value: decimal rate })
            {
                return new Dictionary<DateTime, CurrencyExchangeRateResolution>
                {
                    [date] = new(CurrencyExchangeRateResult.Success(rate), CurrencyExchangeRateSource.Provider)
                };
            }

            state.Observe(result, date);
        }

        return new Dictionary<DateTime, CurrencyExchangeRateResolution>
        {
            [date] = new(state.Build(date), CurrencyExchangeRateSource.Unavailable)
        };
    }

    private async Task<Dictionary<DateTime, CurrencyExchangeRateResult>> ResolveProviderRangeAsync(
        Currency fromCurrency,
        Currency toCurrency,
        IReadOnlyList<DateTime> dates,
        CurrencyExchangeRateResolutionContext context,
        CancellationToken ct)
    {
        var results = new Dictionary<DateTime, CurrencyExchangeRateResult>();
        var states = dates.ToDictionary(date => date, _ => new ResolutionState());

        foreach (var provider in providers)
        {
            ct.ThrowIfCancellationRequested();
            var unresolvedDates = dates
                .Where(date => !results.ContainsKey(date) && !context.GetOutOfRangeProviders(date).Contains(provider))
                .ToList();

            for (var offset = 0; offset < unresolvedDates.Count;)
            {
                ct.ThrowIfCancellationRequested();
                var windowEnd = offset;
                while (windowEnd + 1 < unresolvedDates.Count &&
                       (unresolvedDates[windowEnd + 1] - unresolvedDates[offset]).Days < _maxProviderRangeDays)
                {
                    windowEnd++;
                }

                var windowStartDate = unresolvedDates[offset];
                var windowEndDate = unresolvedDates[windowEnd];
                var providerResults = ct.CanBeCanceled
                    ? await provider.GetExchangeRateAsync(
                        fromCurrency,
                        toCurrency,
                        windowStartDate,
                        windowEndDate,
                        ct)
                    : await provider.GetExchangeRateAsync(
                        fromCurrency,
                        toCurrency,
                        windowStartDate,
                        windowEndDate);
                var resultByDate = providerResults
                    .GroupBy(result => NormalizeDate(result.Date))
                    .ToDictionary(group => group.Key, group => group.Last().Result);

                for (var index = offset; index <= windowEnd; index++)
                {
                    var date = unresolvedDates[index];
                    if (results.ContainsKey(date) || !resultByDate.TryGetValue(date, out var providerResult))
                        continue;

                    if (providerResult.Status == CurrencyExchangeRateProviderStatus.OutOfRange)
                    {
                        context.GetOutOfRangeProviders(date).Add(provider);
                        continue;
                    }

                    if (providerResult is { Status: CurrencyExchangeRateProviderStatus.Success, Value: decimal rate })
                    {
                        results[date] = CurrencyExchangeRateResult.Success(rate);
                        continue;
                    }

                    states[date].Observe(providerResult, date);
                }

                offset = windowEnd + 1;
            }
        }

        foreach (var date in dates)
        {
            if (!results.ContainsKey(date))
                results[date] = states[date].Build(date);
        }

        return results;
    }

    private static DateTime NormalizeDate(DateTime date) =>
        DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);

    private sealed class ResolutionState
    {
        private bool HasFailure { get; set; }

        private DateTime? PendingRetryAtUtc { get; set; }

        public void Observe(CurrencyExchangeRateProviderResult result, DateTime requestedDate)
        {
            if (result.Status == CurrencyExchangeRateProviderStatus.Failed)
                HasFailure = true;

            if (result.Status != CurrencyExchangeRateProviderStatus.NotYetPublished)
                return;

            var retryAtUtc = result.RetryAtUtc ?? requestedDate.Date.AddDays(1);
            PendingRetryAtUtc = PendingRetryAtUtc is null
                ? DateTime.SpecifyKind(retryAtUtc, DateTimeKind.Utc)
                : DateTime.SpecifyKind(
                    retryAtUtc < PendingRetryAtUtc.Value ? retryAtUtc : PendingRetryAtUtc.Value,
                    DateTimeKind.Utc);
        }

        public CurrencyExchangeRateResult Build(DateTime requestedDate)
        {
            if (PendingRetryAtUtc is DateTime retryAtUtc)
                return CurrencyExchangeRateResult.NotYetPublished(requestedDate, retryAtUtc);

            return HasFailure
                ? CurrencyExchangeRateResult.Failed()
                : CurrencyExchangeRateResult.NotFound();
        }
    }
}