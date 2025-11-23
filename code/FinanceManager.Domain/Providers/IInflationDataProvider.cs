using FinanceManager.Domain.ValueObjects;

namespace FinanceManager.Domain.Providers;

/// <summary>
/// Provides inflation rate data for different currencies over time.
/// Implementations may retrieve data from APIs, databases, or in-memory sources.
/// </summary>
public interface IInflationDataProvider
{
    /// <summary>
    /// Get the inflation rate for a specific currency on a given date.
    /// Returns null when no data is available.
    /// </summary>
    Task<InflationRate?> GetInflationRateAsync(int currencyId, DateOnly date, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get inflation rates for a specific currency within a date range.
    /// </summary>
    Task<IEnumerable<InflationRate>> GetInflationRatesAsync(int currencyId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default);
}