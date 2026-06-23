using FinanceManager.Application.Shared;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Services;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
using FinanceManager.Domain.FinancialAccounts.Shared.Services;
using FinanceManager.Domain.FinancialAccounts.Stock.Entities;
using FinanceManager.Domain.MoneyFlow.Entities;

namespace FinanceManager.Application.FinancialAccounts.Investments.Balance;

/// <summary>
/// Values investment accounts (the new asset model) for the dashboard balance aggregates.
/// Investment accounts share <c>AccountType.Stock</c> with legacy stock accounts but hold no
/// <see cref="StockAccountEntry"/> rows; their holdings live in <c>InvestmentTransaction</c> rows and
/// are valued through <see cref="IInvestmentValuationService"/>. Legacy stock accounts have no
/// investment transactions, so the valuation service returns an empty series for them — meaning each
/// Stock-type account is valued by exactly one service (<see cref="Stock.Balance.StockBalanceService"/>
/// for legacy, this one for investment) with no double counting.
/// </summary>
/// <remarks>
/// This service contributes <b>closing balance only</b>. Inflow/outflow/net-cash-flow return empty:
/// the cash account already books the monthly investment outflow (e.g. −500), so reporting the
/// holdings value as cash flow as well would double count against money-flow aggregates. Holdings
/// value belongs in assets / closing balance / net worth, not in cash flow — mirroring how
/// <see cref="Stock.Balance.StockBalanceService"/> contributes nothing for investment accounts.
/// </remarks>
internal class InvestmentBalanceService(
    IFinancialAccountRepository financialAccountRepository,
    IInvestmentValuationService investmentValuationService) : IBalanceServiceTyped
{
    public bool IsOfType<T>() => typeof(T) == typeof(StockAccount);

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

    public Task<List<TimeSeriesModel>> GetClosingBalance(int userId, Currency currency, DateTime start, DateTime end) =>
        GetClosingBalance(userId, currency, start, end, []);

    public async Task<List<TimeSeriesModel>> GetClosingBalance(int userId, Currency currency, DateTime start, DateTime end, IReadOnlyCollection<int> accountIds)
    {
        if (end > DateTime.UtcNow) end = DateTime.UtcNow;
        if (end.Date < start.Date) return [];

        var accountIdFilter = accountIds.Count > 0 ? accountIds.ToHashSet() : [];
        List<int> investmentAccountIds = [];

        await foreach (var account in financialAccountRepository.GetAccounts<StockAccount>(userId, start, end))
        {
            if (account is null) continue;
            if (accountIdFilter.Count > 0 && !accountIdFilter.Contains(account.AccountId)) continue;

            investmentAccountIds.Add(account.AccountId);
        }

        if (investmentAccountIds.Count == 0) return [];

        // One series fetch per account, fanned out concurrently. Legacy stock accounts return an empty
        // series here (no investment transactions) and so contribute nothing.
        var seriesTasks = investmentAccountIds.ToDictionary(
            accountId => accountId,
            accountId => investmentValuationService.GetAccountValueSeriesAsync(accountId, currency, start.Date, end.Date));
        await Task.WhenAll(seriesTasks.Values);

        // Carry every day in the window so the bucketing picks the right closing point even on days the
        // valuation service omitted (a day that values to zero is left out of its per-account series).
        Dictionary<DateTime, decimal> values = [];
        for (var date = start.Date; date <= end.Date; date = date.AddDays(1))
            values[date] = 0;

        foreach (var task in seriesTasks.Values)
        {
            foreach (var (date, value) in await task)
            {
                if (values.ContainsKey(date.Date))
                    values[date.Date] += value;
            }
        }

        return TimeBucketService.Get(values.OrderBy(x => x.Key).Select(x => (x.Key, x.Value)))
                                .Select(bucket => new TimeSeriesModel(bucket.Date, bucket.Objects.Last()))
                                .ToList();
    }
}