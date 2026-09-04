using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Services;

namespace FinanceManager.Application.FinancialAccounts.Shared;

/// <summary>
/// Loads a historical FX range once and carries known rates across non-publishing days. Chart
/// calculations use this instead of resolving a rate for every transaction or day.
/// </summary>
internal static class CurrencyRateSeries
{
    public static async Task<Dictionary<DateTime, decimal>> LoadAsync(
        ICurrencyExchangeService exchangeService,
        Currency fromCurrency,
        Currency toCurrency,
        DateTime start,
        DateTime end)
    {
        var startDate = start.Date;
        var endDate = end.Date;
        if (startDate > endDate) return [];

        if (string.Equals(fromCurrency.ShortName, toCurrency.ShortName, StringComparison.OrdinalIgnoreCase))
        {
            var sameCurrency = new Dictionary<DateTime, decimal>();
            for (var date = startDate; date <= endDate; date = date.AddDays(1))
                sameCurrency[date] = 1m;

            return sameCurrency;
        }

        var rates = await exchangeService.GetExchangeRateAsync(fromCurrency, toCurrency, startDate, endDate);
        var knownRates = rates
            .Where(x => x.Value is > 0m)
            .ToDictionary(x => x.Date.Date, x => x.Value!.Value);
        if (knownRates.Count == 0) return knownRates;

        // FX providers do not publish on every calendar day. Use the last known rate for the
        // trailing gaps and the first known rate for a range that starts before publication.
        decimal? carried = null;
        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            if (knownRates.TryGetValue(date, out var rate))
                carried = rate;
            else if (carried is decimal previousRate)
                knownRates[date] = previousRate;
        }

        var firstRate = knownRates.OrderBy(x => x.Key).First().Value;
        for (var date = startDate; date <= endDate && !knownRates.ContainsKey(date); date = date.AddDays(1))
            knownRates[date] = firstRate;

        return knownRates;
    }

    public static bool TryGet(
        IReadOnlyDictionary<DateTime, decimal> rates,
        DateTime date,
        out decimal rate) =>
        rates.TryGetValue(date.Date, out rate);
}