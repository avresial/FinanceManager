using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Repositories;
using FinanceManager.Domain.FinancialAccounts.Currencies.Services;

namespace FinanceManager.Application.FinancialAccounts.Currencies.ExchangeRates;

internal class CurrencyExchangeService(
    IExchangeRateRepository exchangeRateRepository,
    IEnumerable<ICurrencyExchangeRateProvider> providers) : ICurrencyExchangeService
{
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

        const int batchSize = 50;
        List<(DateTime Date, decimal? Value)> rates = [];

        for (var offset = 0; offset < totalDays; offset += batchSize)
        {
            var currentBatchSize = Math.Min(batchSize, totalDays - offset);
            List<DateTime> batchDates = [];
            List<Task<decimal?>> batchTasks = [];

            for (var i = 0; i < currentBatchSize; i++)
            {
                var date = start.AddDays(offset + i);
                batchDates.Add(date);
                batchTasks.Add(GetExchangeRateAsync(fromCurrency, toCurrency, date));
            }

            var batchResults = await Task.WhenAll(batchTasks);
            for (var i = 0; i < batchResults.Length; i++)
                rates.Add((batchDates[i], batchResults[i]));
        }

        return rates;
    }

    public async Task<decimal?> GetExchangeRateAsync(Currency fromCurrency, Currency toCurrency, DateTime date)
    {
        var rate = await ResolveDirectAsync(fromCurrency, toCurrency, date);
        if (rate is not null) return rate;

        return await ResolveViaUsdAsync(fromCurrency, toCurrency, date);
    }

    // Cheapest sources first: the application's own database, then the configured providers
    // (local CSV files, then external APIs). External hits are persisted so the same pair and
    // date never leave the app twice.
    private async Task<decimal?> ResolveDirectAsync(Currency fromCurrency, Currency toCurrency, DateTime date)
    {
        var stored = await exchangeRateRepository.Get(fromCurrency.ShortName, toCurrency.ShortName, date);
        if (stored is not null) return stored;

        var storedInverse = await exchangeRateRepository.Get(toCurrency.ShortName, fromCurrency.ShortName, date);
        if (storedInverse is decimal inverse && inverse != 0) return 1m / inverse;

        foreach (var provider in providers)
        {
            var rate = await provider.GetExchangeRateAsync(fromCurrency, toCurrency, date);
            if (rate is not null)
            {
                await exchangeRateRepository.Add(fromCurrency.ShortName, toCurrency.ShortName, date, rate.Value);
                return rate;
            }
        }

        return null;
    }

    // When no source knows the pair directly, cross through USD (from → USD → to) so values can
    // still be expressed in the requested currency.
    private async Task<decimal?> ResolveViaUsdAsync(Currency fromCurrency, Currency toCurrency, DateTime date)
    {
        var usd = DefaultCurrency.USD;
        if (IsUsd(fromCurrency) || IsUsd(toCurrency)) return null;

        var fromToUsd = await ResolveDirectAsync(fromCurrency, usd, date);
        if (fromToUsd is null) return null;

        var usdToTarget = await ResolveDirectAsync(usd, toCurrency, date);
        if (usdToTarget is null) return null;

        return fromToUsd * usdToTarget;
    }

    private static bool IsUsd(Currency currency) =>
        string.Equals(currency.ShortName, DefaultCurrency.USD.ShortName, StringComparison.OrdinalIgnoreCase);
}