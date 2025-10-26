using FinanceManager.Domain.Entities;

namespace FinanceManager.Domain.Services
{
    /// <summary>
    /// Resolves a stock's price-per-unit for a given point in time and target currency.
    /// Implementations may cache per-request values to avoid repeated repository calls.
    /// </summary>
    public interface IStockPriceProvider
    {
        /// <summary>
        /// Get price per unit for <paramref name="ticker"/> converted to <paramref name="targetCurrency"/> as of <paramref name="asOf"/>.
        /// Returns1 when no price is available.
        /// </summary>
        Task<decimal> GetPricePerUnitAsync(string ticker, Currency targetCurrency, DateTime asOf);

    }
}
