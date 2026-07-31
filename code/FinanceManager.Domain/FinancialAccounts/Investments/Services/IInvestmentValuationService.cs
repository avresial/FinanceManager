using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;

namespace FinanceManager.Domain.FinancialAccounts.Investments.Services;

/// <summary>
/// Values an investment account on the new asset model. Holdings are computed on read from the
/// account's <see cref="Entities.InvestmentTransaction"/> rows (signed Buy/Sell quantities per
/// <see cref="AssetListing"/>) rather than from a stored running balance, and valued through
/// <see cref="Services.IInvestmentPriceProvider"/> (which already normalises GBX-style quotes and
/// converts to the requested currency).
/// </summary>
public interface IInvestmentValuationService
{
    /// <summary>
    /// Net holdings per <see cref="AssetListing"/> for an account as of <paramref name="asOf"/>:
    /// the signed sum of Buy (+) and Sell (−) quantities for transactions on or before that date.
    /// Listings whose net holding is zero are omitted. Returns an empty map for an account with no
    /// transactions.
    /// </summary>
    Task<IReadOnlyDictionary<long, decimal>> GetHoldingsAsOfAsync(int accountId, DateOnly asOf, CancellationToken ct = default);

    /// <summary>
    /// Total value of an account's holdings converted to <paramref name="targetCurrency"/> as of
    /// <paramref name="asOf"/>. Each listing's holding is valued at its price per unit on that date.
    /// Returns 0 when the account holds nothing or no prices can be determined.
    /// </summary>
    Task<decimal> GetAccountValueAsync(int accountId, Currency targetCurrency, DateTime asOf, CancellationToken ct = default);

    /// <summary>
    /// Daily account value in <paramref name="targetCurrency"/> over [<paramref name="start"/>,
    /// <paramref name="end"/>]. Holdings are carried forward on days without a transaction and each
    /// listing is priced from its own per-day series, so a day's value reflects the holdings held
    /// that day at that day's price. Days that value to zero are omitted.
    /// </summary>
    Task<IReadOnlyDictionary<DateTime, decimal>> GetAccountValueSeriesAsync(
        int accountId,
        Currency targetCurrency,
        DateTime start,
        DateTime end,
        CancellationToken ct = default);

    /// <summary>
    /// Total value per account of the given accounts' holdings converted to
    /// <paramref name="targetCurrency"/> as of <paramref name="asOf"/>. Equivalent to calling
    /// <see cref="GetAccountValueAsync(int, Currency, DateTime, CancellationToken)"/> once per
    /// account, but issues a single transactions query and prices each distinct listing once across
    /// all accounts. Accounts that hold nothing are omitted from the result.
    /// </summary>
    Task<IReadOnlyDictionary<int, decimal>> GetAccountValueAsync(
        IReadOnlyCollection<int> accountIds,
        Currency targetCurrency,
        DateTime asOf,
        CancellationToken ct = default);

    /// <summary>
    /// Daily account value per account for the given accounts over [<paramref name="start"/>,
    /// <paramref name="end"/>]. Equivalent to calling
    /// <see cref="GetAccountValueSeriesAsync(int, Currency, DateTime, DateTime, CancellationToken)"/>
    /// once per account, but issues a single transactions query and prices each distinct listing once
    /// across all accounts. Accounts whose whole series is empty are omitted from the result.
    /// </summary>
    Task<IReadOnlyDictionary<int, IReadOnlyDictionary<DateTime, decimal>>> GetAccountValueSeriesAsync(
        IReadOnlyCollection<int> accountIds,
        Currency targetCurrency,
        DateTime start,
        DateTime end,
        CancellationToken ct = default);

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