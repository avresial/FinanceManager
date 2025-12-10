using FinanceManager.Domain.Providers;
using FinanceManager.Domain.ValueObjects;

namespace FinanceManager.Infrastructure.Providers;

/// <summary>
/// In-memory implementation of inflation data provider.
/// This is a temporary solution until API integration is implemented.
/// Contains historical Polish Zloty (PLN) inflation data.
/// </summary>
public class InMemoryInflationDataProvider : IInflationDataProvider
{
    private readonly List<InflationRate> _inflationRates =
    [
        // PLN (CurrencyId = 1) - Polish Zloty inflation rates
        new(1, new DateOnly(2020, 1, 1), 4.4m),
        new(1, new DateOnly(2020, 2, 1), 4.7m),
        new(1, new DateOnly(2020, 3, 1), 4.6m),
        new(1, new DateOnly(2020, 4, 1), 3.4m),
        new(1, new DateOnly(2020, 5, 1), 3.0m),
        new(1, new DateOnly(2020, 6, 1), 3.3m),
        new(1, new DateOnly(2020, 7, 1), 3.0m),
        new(1, new DateOnly(2020, 8, 1), 2.9m),
        new(1, new DateOnly(2020, 9, 1), 3.2m),
        new(1, new DateOnly(2020, 10, 1), 3.1m),
        new(1, new DateOnly(2020, 11, 1), 3.0m),
        new(1, new DateOnly(2020, 12, 1), 2.4m),

        new(1, new DateOnly(2021, 1, 1), 2.7m),
        new(1, new DateOnly(2021, 2, 1), 2.4m),
        new(1, new DateOnly(2021, 3, 1), 3.2m),
        new(1, new DateOnly(2021, 4, 1), 4.3m),
        new(1, new DateOnly(2021, 5, 1), 4.8m),
        new(1, new DateOnly(2021, 6, 1), 4.4m),
        new(1, new DateOnly(2021, 7, 1), 5.0m),
        new(1, new DateOnly(2021, 8, 1), 5.5m),
        new(1, new DateOnly(2021, 9, 1), 5.9m),
        new(1, new DateOnly(2021, 10, 1), 6.8m),
        new(1, new DateOnly(2021, 11, 1), 7.8m),
        new(1, new DateOnly(2021, 12, 1), 8.6m),

        new(1, new DateOnly(2022, 1, 1), 9.4m),
        new(1, new DateOnly(2022, 2, 1), 8.5m),
        new(1, new DateOnly(2022, 3, 1), 11.0m),
        new(1, new DateOnly(2022, 4, 1), 12.4m),
        new(1, new DateOnly(2022, 5, 1), 13.9m),
        new(1, new DateOnly(2022, 6, 1), 15.5m),
        new(1, new DateOnly(2022, 7, 1), 15.6m),
        new(1, new DateOnly(2022, 8, 1), 16.1m),
        new(1, new DateOnly(2022, 9, 1), 17.2m),
        new(1, new DateOnly(2022, 10, 1), 17.9m),
        new(1, new DateOnly(2022, 11, 1), 17.5m),
        new(1, new DateOnly(2022, 12, 1), 16.6m),

        new(1, new DateOnly(2023, 1, 1), 16.6m),
        new(1, new DateOnly(2023, 2, 1), 18.4m),
        new(1, new DateOnly(2023, 3, 1), 17.9m),
        new(1, new DateOnly(2023, 4, 1), 14.7m),
        new(1, new DateOnly(2023, 5, 1), 13.0m),
        new(1, new DateOnly(2023, 6, 1), 11.5m),
        new(1, new DateOnly(2023, 7, 1), 10.8m),
        new(1, new DateOnly(2023, 8, 1), 10.1m),
        new(1, new DateOnly(2023, 9, 1), 8.2m),
        new(1, new DateOnly(2023, 10, 1), 6.5m),
        new(1, new DateOnly(2023, 11, 1), 6.6m),
        new(1, new DateOnly(2023, 12, 1), 6.2m),

        new(1, new DateOnly(2024, 1, 1), 3.9m),
        new(1, new DateOnly(2024, 2, 1), 2.8m),
        new(1, new DateOnly(2024, 3, 1), 2.0m),
        new(1, new DateOnly(2024, 4, 1), 2.4m),
        new(1, new DateOnly(2024, 5, 1), 2.5m),
        new(1, new DateOnly(2024, 6, 1), 2.6m),
        new(1, new DateOnly(2024, 7, 1), 4.2m),
        new(1, new DateOnly(2024, 8, 1), 4.3m),
        new(1, new DateOnly(2024, 9, 1), 4.9m),
        new(1, new DateOnly(2024, 10, 1), 5.0m),
        new(1, new DateOnly(2024, 11, 1), 4.7m),

        new(1, new DateOnly(2025, 1, 1), 4.8m),
        new(1, new DateOnly(2025, 2, 1), 5.1m),
        new(1, new DateOnly(2025, 3, 1), 5.0m),
        new(1, new DateOnly(2025, 4, 1), 4.8m),
        new(1, new DateOnly(2025, 5, 1), 4.7m),
        new(1, new DateOnly(2025, 6, 1), 4.5m),
        new(1, new DateOnly(2025, 7, 1), 4.4m),
        new(1, new DateOnly(2025, 8, 1), 4.3m),
        new(1, new DateOnly(2025, 9, 1), 4.3m),
        new(1, new DateOnly(2025, 10, 1), 4.2m),
    ];

    public Task<InflationRate?> GetInflationRateAsync(int currencyId, DateOnly date, CancellationToken cancellationToken = default)
    {
        var rate = _inflationRates.FirstOrDefault(r =>
            r.CurrencyId == currencyId &&
            r.Date == date);

        return Task.FromResult(rate);
    }

    public Task<IEnumerable<InflationRate>> GetInflationRatesAsync(int currencyId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        var rates = _inflationRates
            .Where(r =>
                r.CurrencyId == currencyId &&
                r.Date >= from &&
                r.Date <= to)
            .OrderBy(r => r.Date);

        return Task.FromResult(rates.AsEnumerable());
    }
}