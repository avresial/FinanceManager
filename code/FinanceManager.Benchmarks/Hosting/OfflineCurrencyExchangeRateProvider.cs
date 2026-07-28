using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Services;

namespace FinanceManager.Benchmarks.Hosting;

/// <summary>
/// Returns a fixed exchange rate without leaving the process, replacing the CSV and HTTP-backed
/// providers for the duration of a benchmark run.
/// </summary>
/// <remarks>
/// The real chain ends at a third-party HTTP API. A single unlucky network round-trip inside a
/// measured iteration is worth more than every other cost in the endpoint combined, which would make
/// the baseline unreproducible. A constant rate keeps the conversion arithmetic on the hot path while
/// removing the variance — the exact rate is irrelevant to timing.
/// </remarks>
public sealed class OfflineCurrencyExchangeRateProvider : ICurrencyExchangeRateProvider
{
    private const decimal _rate = 1.25m;

    public Task<CurrencyExchangeRateProviderResult> GetExchangeRateAsync(Currency fromCurrency, Currency toCurrency, DateTime date) =>
        Task.FromResult(new CurrencyExchangeRateProviderResult(
            CurrencyExchangeRateProviderStatus.Success,
            fromCurrency.Id == toCurrency.Id ? 1m : _rate));

    public Task<List<(DateTime Date, CurrencyExchangeRateProviderResult Result)>> GetExchangeRateAsync(Currency fromCurrency, Currency toCurrency, DateTime dateStart, DateTime dateEnd)
    {
        var value = fromCurrency.Id == toCurrency.Id ? 1m : _rate;
        var rates = new List<(DateTime Date, CurrencyExchangeRateProviderResult Result)>();

        for (var date = dateStart.Date; date <= dateEnd.Date; date = date.AddDays(1))
            rates.Add((date, new(CurrencyExchangeRateProviderStatus.Success, value)));

        return Task.FromResult(rates);
    }
}