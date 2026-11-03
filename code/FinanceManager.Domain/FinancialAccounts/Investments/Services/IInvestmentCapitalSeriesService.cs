using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;

namespace FinanceManager.Domain.FinancialAccounts.Investments.Services;

public interface IInvestmentCapitalSeriesService
{
    /// <summary>
    /// Remaining weighted-average cost basis for each investment account over [<paramref name="start"/>,
    /// <paramref name="end"/>]. Buy rows add their purchase cost and fee; Sell rows remove only the
    /// historical cost of the quantity sold, never the sale proceeds. Activity before the range is
    /// replayed into the first visible point, so a sale price or a historical edit cannot create an
    /// unrelated capital jump. The current investment model has no standalone deposit, withdrawal,
    /// or transfer rows; unsupported future transaction types are ignored.
    /// </summary>
    Task<IReadOnlyDictionary<int, IReadOnlyDictionary<DateTime, decimal>>> GetCapitalSeriesAsync(
        IReadOnlyCollection<int> accountIds,
        Currency targetCurrency,
        DateTime start,
        DateTime end,
        CancellationToken ct = default);
}