using FinanceManager.Application.FinancialAccounts.Shared;
using FinanceManager.Application.Shared;
using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Bond.Repositories;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Services;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
using FinanceManager.Domain.FinancialAccounts.Shared.Services;
using FinanceManager.Domain.Identity.Repositories;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.MoneyFlow.Entities;

namespace FinanceManager.Application.FinancialAccounts.Bond.Balance;

internal class BondBalanceService(
    IFinancialAccountRepository financialAccountRepository,
    IBondDetailsRepository bondDetailsRepository,
    ICurrencyExchangeService currencyExchangeService) : IBalanceServiceTyped
{
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

    public Task<List<TimeSeriesModel>> GetCapital(int userId, Currency currency, DateTime start, DateTime end) =>
        GetCapital(userId, currency, start, end, []);

    public async Task<List<TimeSeriesModel>> GetCapital(int userId, Currency currency, DateTime start, DateTime end, IReadOnlyCollection<int> accountIds)
    {
        if (end > DateTime.UtcNow) end = DateTime.UtcNow;
        if (start == default || end == default || end.Date < start.Date) return [];

        var accountIdFilter = accountIds.Count > 0 ? accountIds.ToHashSet() : [];
        var bondDetails = await bondDetailsRepository.GetAllAsync().ToDictionaryAsync(x => x.Id);
        List<BondCapitalFlow> flows = [];

        await foreach (var account in financialAccountRepository.GetAccounts<BondAccount>(userId, start, end))
        {
            if (account is null) continue;
            if (accountIdFilter.Count > 0 && !accountIdFilter.Contains(account.AccountId)) continue;

            // GetAccounts carries only the latest pre-range row per bond. Its running Value is the
            // number of units already held, which seeds the visible range without treating accrued
            // interest as user-paid capital.
            var boundaryEntries = account.NextOlderEntries.Values
                .Concat(account.Entries.Where(entry => entry.PostingDate.Date < start.Date))
                .GroupBy(entry => entry.BondDetailsId)
                .Select(group => group.OrderByDescending(entry => entry.PostingDate).ThenByDescending(entry => entry.EntryId).First());

            foreach (var entry in boundaryEntries)
            {
                if (!bondDetails.TryGetValue(entry.BondDetailsId, out var details))
                    throw new InvalidOperationException($"Bond capital requires details for bond id {entry.BondDetailsId}.");

                flows.Add(new BondCapitalFlow(
                    start.Date,
                    entry.Value * details.UnitValue,
                    details.Currency));
            }

            foreach (var entry in account.Entries.Where(entry =>
                         entry.PostingDate.Date >= start.Date && entry.PostingDate.Date <= end.Date))
            {
                if (!bondDetails.TryGetValue(entry.BondDetailsId, out var details))
                    throw new InvalidOperationException($"Bond capital requires details for bond id {entry.BondDetailsId}.");

                // ValueChange is the cash-like unit movement. Interest changes Value through the
                // bond pricing model and therefore never changes contributed capital.
                flows.Add(new BondCapitalFlow(
                    entry.PostingDate.Date,
                    entry.ValueChange * details.UnitValue,
                    details.Currency));
            }
        }

        if (flows.Count == 0) return [];

        var ratesByCurrency = new Dictionary<string, IReadOnlyDictionary<DateTime, decimal>>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in flows.GroupBy(flow => flow.Currency.ShortName, StringComparer.OrdinalIgnoreCase))
        {
            ratesByCurrency[group.Key] = await CurrencyRateSeries.LoadAsync(
                currencyExchangeService,
                group.First().Currency,
                currency,
                start.Date,
                end.Date);
        }

        Dictionary<DateTime, decimal> dailyDeltas = [];
        foreach (var flow in flows)
        {
            if (!ratesByCurrency.TryGetValue(flow.Currency.ShortName, out var rates)
                || !CurrencyRateSeries.TryGet(rates, flow.Date, out var rate))
                continue;

            dailyDeltas[flow.Date] = dailyDeltas.GetValueOrDefault(flow.Date) + flow.Amount * rate;
        }

        Dictionary<DateTime, decimal> cumulative = [];
        decimal capital = 0m;
        for (var date = start.Date; date <= end.Date; date = date.AddDays(1))
        {
            capital += dailyDeltas.GetValueOrDefault(date);
            cumulative[date] = capital;
        }

        return TimeBucketService.Get(cumulative.Select(x => (x.Key, x.Value)))
                                .Select(bucket => new TimeSeriesModel(bucket.Date, bucket.Objects.Last()))
                                .ToList();
    }

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
                if (!bondDetails.TryGetValue(entry.BondDetailsId, out var details))
                    throw new InvalidOperationException($"Bond valuation requires details for bond id {entry.BondDetailsId}.");

                var priceAtDate = entry.ValueChange * details.UnitValue;
                if (!result.ContainsKey(entry.PostingDate.Date)) result[entry.PostingDate.Date] = 0;

                result[entry.PostingDate.Date] += entry.ValueChange >= 0 ? priceAtDate : -Math.Abs(priceAtDate);
            }
        }

        return TimeBucketService.Get(result.OrderBy(x => x.Key).Select(x => (x.Key, x.Value)))
                                .Select(bucket => new TimeSeriesModel(bucket.Date, bucket.Objects.Sum()))
                                .ToList();
    }

    private sealed record BondCapitalFlow(DateTime Date, decimal Amount, Currency Currency);
}