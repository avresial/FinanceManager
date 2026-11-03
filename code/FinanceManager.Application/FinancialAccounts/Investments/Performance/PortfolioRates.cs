using FinanceManager.Application.FinancialAccounts.Shared;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Services;

namespace FinanceManager.Application.FinancialAccounts.Investments.Performance;

/// <summary>Daily FX rate series into the target currency, keyed by source currency (case-insensitive).</summary>
internal static class PortfolioRates
{
    public static async Task<Dictionary<string, IReadOnlyDictionary<DateTime, decimal>>> LoadAsync(
        ICurrencyExchangeService currencyExchangeService,
        IEnumerable<string> sourceCurrencies,
        Currency targetCurrency,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken)
    {
        var currencies = sourceCurrencies
            .Where(currency => !string.IsNullOrWhiteSpace(currency))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        Dictionary<string, IReadOnlyDictionary<DateTime, decimal>> result = new(StringComparer.OrdinalIgnoreCase);
        foreach (var sourceCurrency in currencies)
        {
            var source = new Currency(0, sourceCurrency, sourceCurrency);
            result[sourceCurrency] = await CurrencyRateSeries.LoadAsync(
                currencyExchangeService,
                source,
                targetCurrency,
                startDate,
                endDate,
                cancellationToken);
        }

        return result;
    }

    /// <summary>Maps a minor quote unit (e.g. GBX) to its major currency; other codes are only trimmed.</summary>
    public static string NormalizeCurrency(string currency)
    {
        var trimmed = currency.Trim();
        return DefaultCurrency.MinorQuoteUnits.TryGetValue(trimmed, out var majorCurrency)
            ? majorCurrency
            : trimmed;
    }

    /// <summary>
    /// The source→target rate on both interval edges. Same-currency pairs are 1/1; a missing or
    /// non-positive rate on either edge returns false.
    /// </summary>
    public static bool TryGetRatePair(
        string sourceCurrency,
        Currency targetCurrency,
        DateTime startDate,
        DateTime endDate,
        IReadOnlyDictionary<string, IReadOnlyDictionary<DateTime, decimal>> ratesByCurrency,
        out decimal startRate,
        out decimal endRate)
    {
        if (string.Equals(sourceCurrency, targetCurrency.ShortName, StringComparison.OrdinalIgnoreCase))
        {
            startRate = 1m;
            endRate = 1m;
            return true;
        }

        if (ratesByCurrency.TryGetValue(sourceCurrency, out var rates)
            && CurrencyRateSeries.TryGet(rates, startDate, out startRate)
            && CurrencyRateSeries.TryGet(rates, endDate, out endRate)
            && startRate > 0m
            && endRate > 0m)
            return true;

        startRate = 0m;
        endRate = 0m;
        return false;
    }
}