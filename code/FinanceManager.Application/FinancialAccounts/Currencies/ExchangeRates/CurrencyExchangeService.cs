using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Services;

namespace FinanceManager.Application.FinancialAccounts.Currencies.ExchangeRates;

/// <summary>
/// Assembles a currency exchange-rate request from exact source values and applies the application's
/// direct-pair, USD-cross, and carry-forward policies.
/// </summary>
internal sealed class CurrencyExchangeService(
    ICurrencyExchangeRateSource source) : ICurrencyExchangeService
{
    public Task<List<(DateTime Date, decimal? Value)>> GetExchangeRateAsync(
        Currency fromCurrency,
        Currency toCurrency,
        DateTime dateStart,
        DateTime dateEnd) => GetExchangeRateAsyncCore(fromCurrency, toCurrency, dateStart, dateEnd, CancellationToken.None);

    Task<List<(DateTime Date, decimal? Value)>> ICurrencyExchangeService.GetExchangeRateAsync(
        Currency fromCurrency,
        Currency toCurrency,
        DateTime dateStart,
        DateTime dateEnd,
        CancellationToken cancellationToken) => GetExchangeRateAsyncCore(fromCurrency, toCurrency, dateStart, dateEnd, cancellationToken);

    public Task<List<(DateTime Date, decimal? Value, CurrencyExchangeRateSource Source)>> GetExchangeRateRangeWithProvenanceAsync(
        Currency fromCurrency,
        Currency toCurrency,
        DateTime dateStart,
        DateTime dateEnd) =>
        GetExchangeRateRangeWithProvenanceAsyncCore(fromCurrency, toCurrency, dateStart, dateEnd, CancellationToken.None);

    private async Task<List<(DateTime Date, decimal? Value)>> GetExchangeRateAsyncCore(
        Currency fromCurrency,
        Currency toCurrency,
        DateTime dateStart,
        DateTime dateEnd,
        CancellationToken cancellationToken) =>
        (await GetExchangeRateRangeWithProvenanceAsyncCore(fromCurrency, toCurrency, dateStart, dateEnd, cancellationToken))
            .Select(rate => (rate.Date, rate.Value))
            .ToList();

    private async Task<List<(DateTime Date, decimal? Value, CurrencyExchangeRateSource Source)>> GetExchangeRateRangeWithProvenanceAsyncCore(
        Currency fromCurrency,
        Currency toCurrency,
        DateTime dateStart,
        DateTime dateEnd,
        CancellationToken cancellationToken)
    {
        if (dateStart == default || dateEnd == default)
            return [];

        var start = dateStart.Date;
        var end = dateEnd.Date;
        if (start > end)
            (start, end) = (end, start);

        if (end > DateTime.UtcNow.Date)
            end = DateTime.UtcNow.Date;

        var totalDays = (end - start).Days + 1;
        if (totalDays <= 0)
            return [];

        var dates = Enumerable.Range(0, totalDays)
            .Select(i => NormalizeDate(start.AddDays(i)))
            .ToList();

        if (IsSameCurrency(fromCurrency, toCurrency))
            return dates
                .Select(date => (date, (decimal?)1m, CurrencyExchangeRateSource.SameCurrency))
                .ToList();

        var context = new CurrencyExchangeRateResolutionContext();
        var direct = await source.ResolveAsync(fromCurrency, toCurrency, dates, reuseCachedMisses: false, ct: cancellationToken, context: context);
        Dictionary<DateTime, (decimal? Value, CurrencyExchangeRateSource Source)> rates = [];
        List<DateTime> crossDates = [];
        List<DateTime> deferredDates = [];

        foreach (var date in dates)
        {
            if (!direct.TryGetValue(date, out var resolution))
            {
                // The provider source omits dates beyond its per-call budget. They are filled from
                // the nearest earlier known exact or derived value below.
                deferredDates.Add(date);
                continue;
            }

            if (resolution.Result.IsSuccess)
            {
                rates[date] = (resolution.Result.Value, resolution.Source);
            }
            else if (resolution.Result.Status == CurrencyExchangeRateStatus.NotYetPublished)
            {
                // A publication miss must not be replaced by yesterday's value.
                rates[date] = (null, CurrencyExchangeRateSource.Unavailable);
            }
            else
            {
                crossDates.Add(date);
            }
        }

        if (crossDates.Count > 0)
        {
            if (IsUsd(fromCurrency) || IsUsd(toCurrency))
            {
                foreach (var date in crossDates)
                    rates[date] = (null, CurrencyExchangeRateSource.Unavailable);
            }
            else
            {
                var usd = DefaultCurrency.USD;
                var fromUsd = await source.ResolveAsync(fromCurrency, usd, crossDates, reuseCachedMisses: false, ct: cancellationToken, context: context);
                List<DateTime> targetDates = [];

                foreach (var date in crossDates)
                {
                    if (!fromUsd.TryGetValue(date, out var fromUsdResolution))
                    {
                        deferredDates.Add(date);
                        continue;
                    }

                    if (!fromUsdResolution.Result.IsSuccess)
                    {
                        rates[date] = (null, CurrencyExchangeRateSource.Unavailable);
                        continue;
                    }

                    targetDates.Add(date);
                }

                var usdTarget = await source.ResolveAsync(usd, toCurrency, targetDates, reuseCachedMisses: false, ct: cancellationToken, context: context);
                foreach (var date in targetDates)
                {
                    if (!usdTarget.TryGetValue(date, out var usdTargetResolution))
                    {
                        deferredDates.Add(date);
                        continue;
                    }

                    if (usdTargetResolution.Result.IsSuccess && fromUsd[date].Result.Value is decimal fromRate)
                    {
                        rates[date] = (
                            fromRate * usdTargetResolution.Result.Value!.Value,
                            CurrencyExchangeRateSource.DerivedViaUsd);
                    }
                    else
                    {
                        rates[date] = (null, CurrencyExchangeRateSource.Unavailable);
                    }
                }
            }
        }

        AddCarryForwardRates(rates, deferredDates);

        return dates
            .Select(date =>
            {
                var rate = rates.TryGetValue(date, out var resolved)
                    ? resolved
                    : ((decimal?)null, CurrencyExchangeRateSource.Unavailable);
                return (date, rate.Item1, rate.Item2);
            })
            .ToList();
    }

    public Task<decimal?> GetExchangeRateAsync(
        Currency fromCurrency,
        Currency toCurrency,
        DateTime date) =>
        GetExchangeRateCoreAsync(fromCurrency, toCurrency, date, CancellationToken.None);

    Task<decimal?> ICurrencyExchangeService.GetExchangeRateAsync(
        Currency fromCurrency,
        Currency toCurrency,
        DateTime date,
        CancellationToken cancellationToken) =>
        GetExchangeRateCoreAsync(fromCurrency, toCurrency, date, cancellationToken);

    public Task<CurrencyExchangeRateResult> GetExchangeRateResultAsync(
        Currency fromCurrency,
        Currency toCurrency,
        DateTime date) =>
        GetExchangeRateResultCoreAsync(fromCurrency, toCurrency, date, CancellationToken.None);

    Task<CurrencyExchangeRateResult> ICurrencyExchangeService.GetExchangeRateResultAsync(
        Currency fromCurrency,
        Currency toCurrency,
        DateTime date,
        CancellationToken cancellationToken) =>
        GetExchangeRateResultCoreAsync(fromCurrency, toCurrency, date, cancellationToken);

    public Task<CurrencyExchangeRateResolution> GetExchangeRateWithSourceAsync(
        Currency fromCurrency,
        Currency toCurrency,
        DateTime date) =>
        GetExchangeRateWithSourceCoreAsync(fromCurrency, toCurrency, date, CancellationToken.None);

    private async Task<decimal?> GetExchangeRateCoreAsync(
        Currency fromCurrency,
        Currency toCurrency,
        DateTime date,
        CancellationToken cancellationToken) =>
        (await GetExchangeRateResultCoreAsync(fromCurrency, toCurrency, date, cancellationToken)).Value;

    private async Task<CurrencyExchangeRateResult> GetExchangeRateResultCoreAsync(
        Currency fromCurrency,
        Currency toCurrency,
        DateTime date,
        CancellationToken cancellationToken) =>
        (await GetExchangeRateWithSourceCoreAsync(fromCurrency, toCurrency, date, cancellationToken)).Result;

    private async Task<CurrencyExchangeRateResolution> GetExchangeRateWithSourceCoreAsync(
        Currency fromCurrency,
        Currency toCurrency,
        DateTime date,
        CancellationToken cancellationToken)
    {
        var requestedDate = NormalizeDate(date);
        if (IsSameCurrency(fromCurrency, toCurrency))
            return new(CurrencyExchangeRateResult.Success(1m), CurrencyExchangeRateSource.SameCurrency);

        var context = new CurrencyExchangeRateResolutionContext();
        var direct = await ResolveSingleAsync(fromCurrency, toCurrency, requestedDate, reuseCachedMisses: true, cancellationToken, context);
        if (direct.Result.IsSuccess || direct.Result.Status == CurrencyExchangeRateStatus.NotYetPublished)
            return direct;

        if (IsUsd(fromCurrency) || IsUsd(toCurrency))
            return direct;

        var fromUsd = await ResolveSingleAsync(fromCurrency, DefaultCurrency.USD, requestedDate, reuseCachedMisses: true, cancellationToken, context);
        if (!fromUsd.Result.IsSuccess)
            return new(CombineFailure(direct.Result, fromUsd.Result), CurrencyExchangeRateSource.Unavailable);

        var usdTarget = await ResolveSingleAsync(DefaultCurrency.USD, toCurrency, requestedDate, reuseCachedMisses: true, cancellationToken, context);
        if (!usdTarget.Result.IsSuccess)
            return new(CombineFailure(direct.Result, usdTarget.Result), CurrencyExchangeRateSource.Unavailable);

        return new(
            CurrencyExchangeRateResult.Success(fromUsd.Result.Value!.Value * usdTarget.Result.Value!.Value),
            CurrencyExchangeRateSource.DerivedViaUsd);
    }

    private async Task<CurrencyExchangeRateResolution> ResolveSingleAsync(
        Currency fromCurrency,
        Currency toCurrency,
        DateTime date,
        bool reuseCachedMisses,
        CancellationToken cancellationToken,
        CurrencyExchangeRateResolutionContext context)
    {
        var resolved = await source.ResolveAsync(fromCurrency, toCurrency, [date], reuseCachedMisses, ct: cancellationToken, context: context);
        return resolved.TryGetValue(date, out var result)
            ? result
            : new(CurrencyExchangeRateResult.NotFound(), CurrencyExchangeRateSource.Unavailable);
    }

    private static void AddCarryForwardRates(
        IDictionary<DateTime, (decimal? Value, CurrencyExchangeRateSource Source)> rates,
        IEnumerable<DateTime> deferredDates)
    {
        var deferred = deferredDates.Distinct().OrderBy(date => date).ToList();
        if (deferred.Count == 0)
            return;

        var known = rates
            .Where(pair => pair.Value.Value is not null)
            .OrderBy(pair => pair.Key)
            .ToList();
        var knownIndex = 0;
        decimal? carried = null;
        var todayUtc = DateTime.UtcNow.Date;

        foreach (var date in deferred)
        {
            if (date.Date == todayUtc)
            {
                rates[date] = (null, CurrencyExchangeRateSource.Unavailable);
                continue;
            }

            while (knownIndex < known.Count && known[knownIndex].Key <= date)
                carried = known[knownIndex++].Value.Value;

            rates[date] = (
                carried,
                carried is null
                    ? CurrencyExchangeRateSource.Unavailable
                    : CurrencyExchangeRateSource.CarriedForward);
        }
    }

    private static CurrencyExchangeRateResult CombineFailure(
        CurrencyExchangeRateResult first,
        CurrencyExchangeRateResult second)
    {
        if (first.Status == CurrencyExchangeRateStatus.NotYetPublished)
            return first;
        if (second.Status == CurrencyExchangeRateStatus.NotYetPublished)
            return second;
        if (first.Status == CurrencyExchangeRateStatus.Failed || second.Status == CurrencyExchangeRateStatus.Failed)
            return CurrencyExchangeRateResult.Failed();
        return CurrencyExchangeRateResult.NotFound();
    }

    private static bool IsSameCurrency(Currency fromCurrency, Currency toCurrency) =>
        string.Equals(
            NormalizeCurrency(fromCurrency.ShortName),
            NormalizeCurrency(toCurrency.ShortName),
            StringComparison.Ordinal);

    private static bool IsUsd(Currency currency) =>
        string.Equals(
            NormalizeCurrency(currency.ShortName),
            NormalizeCurrency(DefaultCurrency.USD.ShortName),
            StringComparison.Ordinal);

    private static string NormalizeCurrency(string? currency) =>
        string.IsNullOrWhiteSpace(currency) ? string.Empty : currency.Trim().ToUpperInvariant();

    private static DateTime NormalizeDate(DateTime date) =>
        DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);
}