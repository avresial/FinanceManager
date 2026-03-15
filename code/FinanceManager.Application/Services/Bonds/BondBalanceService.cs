using FinanceManager.Domain.Entities.Bonds;
using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.Repositories;
using FinanceManager.Domain.Repositories.Account;
using FinanceManager.Domain.Services;

namespace FinanceManager.Application.Services.Bonds;

internal class BondBalanceService(IFinancialAccountRepository financialAccountRepository, IBondDetailsRepository bondDetailsRepository) : IBalanceServiceTyped
{
    public bool IsOfType<T>() => typeof(T) == typeof(BondAccount);

    public Task<List<TimeSeriesModel>> GetInflow(int userId, Currency currency, DateTime start, DateTime end) =>
        GetInflow(userId, currency, start, end, []);

    public Task<List<TimeSeriesModel>> GetInflow(int userId, Currency currency, DateTime start, DateTime end, IReadOnlyCollection<int> accountIds) =>
        AggregateByDay(userId, start, end, entry => entry.ValueChange > 0, accountIds);

    public Task<List<TimeSeriesModel>> GetOutflow(int userId, Currency currency, DateTime start, DateTime end) =>
        GetOutflow(userId, currency, start, end, []);

    public Task<List<TimeSeriesModel>> GetOutflow(int userId, Currency currency, DateTime start, DateTime end, IReadOnlyCollection<int> accountIds) =>
        AggregateByDay(userId, start, end, entry => entry.ValueChange < 0, accountIds);

    public Task<List<TimeSeriesModel>> GetNetCashFlow(int userId, Currency currency, DateTime start, DateTime end) =>
        GetNetCashFlow(userId, currency, start, end, []);

    public Task<List<TimeSeriesModel>> GetNetCashFlow(int userId, Currency currency, DateTime start, DateTime end, IReadOnlyCollection<int> accountIds) =>
        AggregateByDay(userId, start, end, _ => true, accountIds);

    public Task<List<TimeSeriesModel>> GetClosingBalance(int userId, Currency currency, DateTime start, DateTime end) =>
        GetClosingBalance(userId, currency, start, end, []);

    public async Task<List<TimeSeriesModel>> GetClosingBalance(int userId, Currency currency, DateTime start, DateTime end, IReadOnlyCollection<int> accountIds)
    {
        if (end > DateTime.UtcNow) end = DateTime.UtcNow;

        Dictionary<DateTime, decimal> prices = [];
        var accountIdFilter = accountIds.Count > 0 ? accountIds.ToHashSet() : [];
        var bondDetails = await bondDetailsRepository.GetAllAsync().ToListAsync();

        await foreach (var account in financialAccountRepository.GetAccounts<BondAccount>(userId, start, end))
        {
            if (account is null) continue;
            if (accountIdFilter.Count > 0 && !accountIdFilter.Contains(account.AccountId)) continue;

            foreach (var price in account.GetDailyPrice(DateOnly.FromDateTime(start), DateOnly.FromDateTime(end), bondDetails))
            {
                var date = price.Key.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
                if (!prices.ContainsKey(date))
                    prices[date] = price.Value;
                else
                    prices[date] += price.Value;
            }
        }

        return TimeBucketService.Get(prices.OrderBy(x => x.Key).Select(x => (x.Key, x.Value)))
                                .Select(bucket => new TimeSeriesModel(bucket.Date, bucket.Objects.Last()))
                                .ToList();
    }

    private async Task<List<TimeSeriesModel>> AggregateByDay(int userId, DateTime start, DateTime end, Func<BondAccountEntry, bool> predicate, IReadOnlyCollection<int> accountIds)
    {
        if (end > DateTime.UtcNow) end = DateTime.UtcNow;

        Dictionary<DateTime, decimal> result = [];
        var accountIdFilter = accountIds.Count > 0 ? accountIds.ToHashSet() : [];
        var bondDetails = await bondDetailsRepository.GetAllAsync().ToDictionaryAsync(x => x.Id);

        await foreach (var account in financialAccountRepository.GetAccounts<BondAccount>(userId, start, end))
        {
            if (account?.Entries is null) continue;
            if (accountIdFilter.Count > 0 && !accountIdFilter.Contains(account.AccountId)) continue;

            foreach (var entry in account.Entries)
            {
                if (entry.PostingDate.Date < start.Date || entry.PostingDate.Date > end.Date) continue;
                if (!predicate(entry)) continue;
                if (!bondDetails.TryGetValue(entry.BondDetailsId, out var details)) continue;

                var priceAtDate = entry.ValueChange * details.UnitValue;
                if (!result.ContainsKey(entry.PostingDate.Date)) result[entry.PostingDate.Date] = 0;

                result[entry.PostingDate.Date] += entry.ValueChange >= 0 ? priceAtDate : -Math.Abs(priceAtDate);
            }
        }

        return TimeBucketService.Get(result.OrderBy(x => x.Key).Select(x => (x.Key, x.Value)))
                                .Select(bucket => new TimeSeriesModel(bucket.Date, bucket.Objects.Sum()))
                                .ToList();
    }
}