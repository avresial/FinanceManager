using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;

namespace FinanceManager.Domain.FinancialAccounts.Investments.Services;

public interface IInvestmentCapitalSeriesService
{
    /// <summary>
    /// Remaining weighted-average cost basis of the owner's pooled holdings (one cost pool per listing across every trade of
    /// <paramref name="userId"/>, linked to an account or not) over [<paramref name="start"/>,
    /// <paramref name="end"/>]. Buy rows add their purchase cost and fee; Sell rows remove only the
    /// historical cost of the quantity sold, never the sale proceeds. Activity before the range is
    /// replayed into the first visible point, so a sale price or a historical edit cannot create an
    /// unrelated capital jump. The current investment model has no standalone deposit, withdrawal,
    /// or transfer rows; unsupported future transaction types are ignored.
    /// </summary>
    Task<IReadOnlyDictionary<DateTime, decimal>> GetCapitalSeriesAsync(
        int userId,
        Currency targetCurrency,
        DateTime start,
        DateTime end,
        CancellationToken ct = default);
}