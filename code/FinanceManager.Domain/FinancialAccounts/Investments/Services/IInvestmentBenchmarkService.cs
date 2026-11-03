using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;

namespace FinanceManager.Domain.FinancialAccounts.Investments.Services;

public interface IInvestmentBenchmarkService
{
    /// <summary>
    /// Daily value of a shadow portfolio that receives the same contributions as
    /// <paramref name="accountId"/> but invests them in the benchmark instead, in
    /// <paramref name="targetCurrency"/> over [<paramref name="start"/>, <paramref name="end"/>].
    /// A null <paramref name="assetListingId"/> selects the default Polish inflation index.
    /// </summary>
    /// <remarks>
    /// Rebasing onto a single opening value would compare the account against a fixed sum while the
    /// account itself also grows by contributions, so the benchmark could never keep up and simply
    /// hugged the bottom of the chart. Instead, holdings carried in from before the range open the
    /// shadow portfolio at their value on the first day, and every later trade moves the same amount
    /// of money in or out of the benchmark on its own trade date. Both series therefore reflect the
    /// same cash flows and the gap between them is the account's real over- or under-performance.
    /// Days the shadow portfolio is worth nothing are omitted, as in the account's own value series.
    /// </remarks>
    Task<IReadOnlyDictionary<DateTime, decimal>> GetBenchmarkSeriesAsync(
        long? assetListingId,
        int accountId,
        Currency targetCurrency,
        DateTime start,
        DateTime end,
        CancellationToken ct = default);
}