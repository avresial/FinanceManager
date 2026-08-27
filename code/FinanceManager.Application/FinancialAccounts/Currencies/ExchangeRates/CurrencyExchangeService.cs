using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Repositories;
using FinanceManager.Domain.FinancialAccounts.Currencies.Services;

namespace FinanceManager.Application.FinancialAccounts.Currencies.ExchangeRates;

internal class CurrencyExchangeService(
    IExchangeRateRepository exchangeRateRepository,
    IEnumerable<ICurrencyExchangeRateProvider> providers) : ICurrencyExchangeService
{
    // A wide range (years of chart history) can miss thousands of daily rates. Each provider
    // resolution is a chain of DB lookups plus external HTTP calls, so resolving every missing
    // date inside a single request can exceed the browser's 100 s HTTP timeout. Resolved rates
    // are persisted, so successive requests keep narrowing the gap until the range is covered.
    private const int _maxProviderResolutionsPerCall = 60;

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

        var stored = await exchangeRateRepository.GetRange(fromCurrency.ShortName, toCurrency.ShortName, start, end);
        var normalizedFrom = Normalize(fromCurrency.ShortName);
        var normalizedTo = Normalize(toCurrency.ShortName);

        List<(DateTime Date, decimal? Value)> rates = [];
        List<DateTime> missingDates = [];

        for (var i = 0; i < totalDays; i++)
        {
            var date = start.AddDays(i);
            var key = (normalizedFrom, normalizedTo, NormalizeDate(date));
            if (stored.TryGetValue(key, out var rate))
            {
                rates.Add((date, rate));
            }
            else
            {
                missingDates.Add(date);
            }
        }

        if (missingDates.Count > 0)
        {
            var toResolve = missingDates.Count <= _maxProviderResolutionsPerCall
                ? missingDates
                : missingDates.Take(_maxProviderResolutionsPerCall).ToList();

            const int batchSize = 50;
            for (var offset = 0; offset < toResolve.Count; offset += batchSize)
            {
                var currentBatchSize = Math.Min(batchSize, toResolve.Count - offset);
                List<Task<decimal?>> batchTasks = [];

                for (var i = 0; i < currentBatchSize; i++)
                {
                    var date = toResolve[offset + i];
                    batchTasks.Add(GetExchangeRateAsync(fromCurrency, toCurrency, date));
                }

                var batchResults = await Task.WhenAll(batchTasks);
                for (var i = 0; i < batchResults.Length; i++)
                    rates.Add((toResolve[offset + i], batchResults[i]));
            }

            // Dates past the per-call resolution cap carry the nearest earlier known rate
            // (daily FX barely moves day-to-day) instead of hitting the providers.
            if (toResolve.Count < missingDates.Count)
            {
                var knownAscending = rates.Where(x => x.Value is not null).OrderBy(x => x.Date).ToList();
                var knownIndex = 0;
                decimal? carried = null;
                var todayUtc = DateTime.UtcNow.Date;
                foreach (var date in missingDates.Skip(toResolve.Count))
                {
                    // A current-day rate is deliberately never forward-filled. The caller must
                    // receive the publication-window outcome instead of a previous day's value.
                    if (date.Date == todayUtc)
                    {
                        rates.Add((date, null));
                        continue;
                    }

                    while (knownIndex < knownAscending.Count && knownAscending[knownIndex].Date <= date)
                        carried = knownAscending[knownIndex++].Value;

                    rates.Add((date, carried));
                }
            }
        }

        return rates.OrderBy(x => x.Date).ToList();
    }

    public async Task<decimal?> GetExchangeRateAsync(Currency fromCurrency, Currency toCurrency, DateTime date) =>
        (await GetExchangeRateResultAsync(fromCurrency, toCurrency, date)).Value;

    public async Task<CurrencyExchangeRateResult> GetExchangeRateResultAsync(Currency fromCurrency, Currency toCurrency, DateTime date)
    {
        var state = new ResolutionState();
        var direct = await ResolveDirectAsync(fromCurrency, toCurrency, date, state);
        if (direct.IsSuccess || direct.Status == CurrencyExchangeRateStatus.NotYetPublished)
            return direct;

        return await ResolveViaUsdAsync(fromCurrency, toCurrency, date, state);
    }

    // Cheapest sources first: the application's own database, then the configured providers
    // (local CSV files, then external APIs). External hits are persisted so the same pair and
    // date never leave the app twice.
    private async Task<CurrencyExchangeRateResult> ResolveDirectAsync(
        Currency fromCurrency,
        Currency toCurrency,
        DateTime date,
        ResolutionState state)
    {
        var stored = await exchangeRateRepository.Get(fromCurrency.ShortName, toCurrency.ShortName, date);
        if (stored is decimal storedRate) return CurrencyExchangeRateResult.Success(storedRate);

        var storedInverse = await exchangeRateRepository.Get(toCurrency.ShortName, fromCurrency.ShortName, date);
        if (storedInverse is decimal inverse && inverse != 0)
            return CurrencyExchangeRateResult.Success(1m / inverse);

        foreach (var provider in providers)
        {
            if (state.OutOfRangeProviders.Contains(provider))
                continue;

            var result = await provider.GetExchangeRateAsync(fromCurrency, toCurrency, date);
            if (result.Status == CurrencyExchangeRateProviderStatus.OutOfRange)
            {
                state.OutOfRangeProviders.Add(provider);
                continue;
            }

            if (result is { Status: CurrencyExchangeRateProviderStatus.Success, Value: decimal rate })
            {
                await exchangeRateRepository.Add(fromCurrency.ShortName, toCurrency.ShortName, date, rate);
                return CurrencyExchangeRateResult.Success(rate);
            }

            state.Observe(result, date);
        }

        return state.Build(date);
    }

    // When no source knows the pair directly, cross through USD (from → USD → to) so values can
    // still be expressed in the requested currency.
    private async Task<CurrencyExchangeRateResult> ResolveViaUsdAsync(
        Currency fromCurrency,
        Currency toCurrency,
        DateTime date,
        ResolutionState state)
    {
        var usd = DefaultCurrency.USD;
        if (IsUsd(fromCurrency) || IsUsd(toCurrency)) return state.Build(date);

        var fromToUsd = await ResolveDirectAsync(fromCurrency, usd, date, state);
        if (!fromToUsd.IsSuccess) return state.Build(date);

        var usdToTarget = await ResolveDirectAsync(usd, toCurrency, date, state);
        if (!usdToTarget.IsSuccess) return state.Build(date);

        return CurrencyExchangeRateResult.Success(fromToUsd.Value!.Value * usdToTarget.Value!.Value);
    }

    private static bool IsUsd(Currency currency) =>
        string.Equals(currency.ShortName, DefaultCurrency.USD.ShortName, StringComparison.OrdinalIgnoreCase);

    private static string Normalize(string currency) => currency.Trim().ToUpperInvariant();

    private static DateTime NormalizeDate(DateTime date) => DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);

    private sealed class ResolutionState
    {
        public HashSet<ICurrencyExchangeRateProvider> OutOfRangeProviders { get; } = [];

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