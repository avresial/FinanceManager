using FinanceManager.Application.Dashboard;
using FinanceManager.Application.Shared;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Services;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
using FinanceManager.Domain.FinancialAccounts.Shared.Services;
using FinanceManager.Domain.MoneyFlow.Entities;

namespace FinanceManager.Application.FinancialAccounts.Investments.Balance;

/// <summary>
/// Values the owner's pooled investment holdings (the new asset model) for the dashboard balance aggregates.
/// Holdings live in <c>InvestmentTransaction</c> rows of the user, linked to an account or not, and are valued through
/// <see cref="IInvestmentValuationService"/>; cost basis comes from <see cref="IInvestmentCapitalSeriesService"/>. Legacy stock
/// accounts have no investment transactions, so each Stock-type account is valued by exactly one service
/// (<see cref="Stock.Balance.StockBalanceService"/> for legacy, this one for investment) with no double counting.
/// </summary>
/// <remarks>
/// This service contributes <b>closing balance only</b>. Inflow/outflow/net-cash-flow return empty:
/// the cash account already books the monthly investment outflow (e.g. −500), so reporting the
/// holdings value as cash flow as well would double count against money-flow aggregates. Holdings
/// value belongs in assets / closing balance / net worth, not in cash flow — mirroring how
/// <see cref="Stock.Balance.StockBalanceService"/> contributes nothing for investment accounts.
/// An account-filtered call serves the legacy investment account page until the account screens are removed: it returns the
/// owner's pooled series when the filter names one of the user's investment accounts and nothing otherwise, so cash and bond account
/// charts never pick up investment values.
/// </remarks>
internal class InvestmentBalanceService(
    IAccountRepository<InvestmentAccount> investmentAccountRepository,
    IInvestmentValuationService investmentValuationService,
    IInvestmentCapitalSeriesService capitalSeriesService) : IBalanceServiceTyped
{
    // Cash flow is intentionally empty for investment accounts to avoid double counting against the
    // cash account that books the investment outflow. See the class remarks.
    public Task<List<TimeSeriesModel>> GetInflow(int userId, Currency currency, DateTime start, DateTime end) =>
        Task.FromResult(new List<TimeSeriesModel>());

    public Task<List<TimeSeriesModel>> GetInflow(int userId, Currency currency, DateTime start, DateTime end, IReadOnlyCollection<int> accountIds) =>
        Task.FromResult(new List<TimeSeriesModel>());

    public Task<List<TimeSeriesModel>> GetOutflow(int userId, Currency currency, DateTime start, DateTime end) =>
        Task.FromResult(new List<TimeSeriesModel>());

    public Task<List<TimeSeriesModel>> GetOutflow(int userId, Currency currency, DateTime start, DateTime end, IReadOnlyCollection<int> accountIds) =>
        Task.FromResult(new List<TimeSeriesModel>());

    public Task<List<TimeSeriesModel>> GetNetCashFlow(int userId, Currency currency, DateTime start, DateTime end) =>
        Task.FromResult(new List<TimeSeriesModel>());

    public Task<List<TimeSeriesModel>> GetNetCashFlow(int userId, Currency currency, DateTime start, DateTime end, IReadOnlyCollection<int> accountIds) =>
        Task.FromResult(new List<TimeSeriesModel>());

    public Task<List<TimeSeriesModel>> GetCapital(int userId, Currency currency, DateTime start, DateTime end) =>
        GetCapital(userId, currency, start, end, []);

    public async Task<List<TimeSeriesModel>> GetCapital(int userId, Currency currency, DateTime start, DateTime end, IReadOnlyCollection<int> accountIds)
    {
        if (end > DateTime.UtcNow) end = DateTime.UtcNow;
        if (userId == 0 || start == default || end == default || end.Date < start.Date) return [];
        if (!await IsInScope(userId, accountIds)) return [];

        var series = await capitalSeriesService.GetCapitalSeriesAsync(userId, currency, start.Date, end.Date);
        if (series.Count == 0) return [];

        Dictionary<DateTime, decimal> values = [];
        for (var date = start.Date; date <= end.Date; date = date.AddDays(1))
            values[date] = series.GetValueOrDefault(date);

        return ToBuckets(values);
    }

    public Task<List<TimeSeriesModel>> GetClosingBalance(int userId, Currency currency, DateTime start, DateTime end) =>
        GetClosingBalance(userId, currency, start, end, []);

    public async Task<List<TimeSeriesModel>> GetClosingBalance(int userId, Currency currency, DateTime start, DateTime end, IReadOnlyCollection<int> accountIds)
    {
        end = DashboardReadCutoff.Clamp(end, DateTime.UtcNow);
        if (userId == 0 || end.Date < start.Date) return [];
        if (!await IsInScope(userId, accountIds)) return [];

        // The owner's pooled holdings are valued as one portfolio, so the series does not depend on which account a trade was booked in.
        var series = await investmentValuationService.GetUserValueSeriesAsync(userId, currency, start.Date, end.Date);
        if (series.Count == 0) return [];

        // Carry every day in the window so the bucketing picks the right closing point even on days the
        // valuation service omitted (a day that values to zero is left out of its series).
        Dictionary<DateTime, decimal> values = [];
        for (var date = start.Date; date <= end.Date; date = date.AddDays(1))
            values[date] = series.GetValueOrDefault(date);

        return ToBuckets(values);
    }

    // Temporary guard until the account screens are removed: an empty filter means the whole portfolio; a filter is honoured only
    // when it names one of the user's investment accounts, otherwise (cash or bond account ids) there is nothing to report.
    private async Task<bool> IsInScope(int userId, IReadOnlyCollection<int> accountIds)
    {
        if (accountIds.Count == 0) return true;

        var accounts = await investmentAccountRepository.GetAll(userId);
        return accounts.Any(account => account.UserId == userId && accountIds.Contains(account.AccountId));
    }

    private static List<TimeSeriesModel> ToBuckets(Dictionary<DateTime, decimal> values) =>
        TimeBucketService.Get(values.OrderBy(x => x.Key).Select(x => (x.Key, x.Value)))
                         .Select(bucket => new TimeSeriesModel(bucket.Date, bucket.Objects.Last()))
                         .ToList();
}