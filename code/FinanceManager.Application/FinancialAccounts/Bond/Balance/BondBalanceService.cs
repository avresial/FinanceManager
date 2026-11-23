using FinanceManager.Application.Dashboard;
using FinanceManager.Application.FinancialAccounts.Bond.Valuation;
using FinanceManager.Application.FinancialAccounts.Shared;
using FinanceManager.Application.Shared;
using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Services;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
using FinanceManager.Domain.FinancialAccounts.Shared.Services;
using FinanceManager.Domain.Identity.Repositories;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.MoneyFlow.Entities;
using FinanceManager.Domain.Transactions.Bonds.Repositories;
using FinanceManager.Domain.Transactions.Shared;

namespace FinanceManager.Application.FinancialAccounts.Bond.Balance;

/// <remarks>
/// Without an account filter the balances come from the owner's pooled bond entries (units per bond definition, accruing
/// from the latest pooled entry); an account filter keeps reading the account's own entries for the bond account page.
/// </remarks>
internal class BondBalanceService(
    IBondTransactionReader bondTransactionReader,
    IFinancialAccountRepository financialAccountRepository,
    BondDashboardContext bondDashboardContext,
    ICurrencyExchangeService currencyExchangeService) : IBalanceServiceTyped
{
    public Task<List<TimeSeriesModel>> GetInflow(int userId, Currency currency, DateTime start, DateTime end) =>
        GetInflow(userId, currency, start, end, []);

    public Task<List<TimeSeriesModel>> GetInflow(int userId, Currency currency, DateTime start, DateTime end, IReadOnlyCollection<int> accountIds) =>
        AggregateByDay(userId, start, end, change => change > 0, accountIds);

    public Task<List<TimeSeriesModel>> GetOutflow(int userId, Currency currency, DateTime start, DateTime end) =>
        GetOutflow(userId, currency, start, end, []);

    public Task<List<TimeSeriesModel>> GetOutflow(int userId, Currency currency, DateTime start, DateTime end, IReadOnlyCollection<int> accountIds) =>
        AggregateByDay(userId, start, end, change => change < 0, accountIds);

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
        if (accountIds.Count == 0) return await GetOwnerCapital(userId, currency, start, end);

        var accountIdFilter = accountIds.ToHashSet();
        List<BondAccount> bondAccounts = [];
        await foreach (var account in financialAccountRepository.GetAccounts<BondAccount>(userId, start, end))
        {
            if (account is null) continue;
            if (accountIdFilter.Count > 0 && !accountIdFilter.Contains(account.AccountId)) continue;
            bondAccounts.Add(account);
        }

        // Only the definitions the accounts actually reference are loaded (detached, no-tracking),
        // shared with the other dashboard bond paths through the request-scoped context.
        var bondDetails = await bondDashboardContext.LoadReferencedDetailsAsync(bondAccounts);
        List<BondCapitalFlow> flows = [];

        foreach (var account in bondAccounts)
        {
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

        return await BucketCapital(flows, currency, start, end);
    }

    // Capital is what the user put in: the units already held before the range count on its first day, then each entry's unit movement.
    // Interest changes the pooled value through bond pricing and never changes contributed capital.
    private async Task<List<TimeSeriesModel>> GetOwnerCapital(int userId, Currency currency, DateTime start, DateTime end)
    {
        var range = await bondTransactionReader.GetValueRange(userId, start, end);
        var bondDetails = await bondDashboardContext.LoadReferencedDetailsAsync(
            range.Opening.Select(p => p.PartitionId).Concat(range.Points.Select(p => p.PartitionId)));

        BondDetails GetDetails(int bondDetailsId) => bondDetails.TryGetValue(bondDetailsId, out var details)
            ? details
            : throw new InvalidOperationException($"Bond capital requires details for bond id {bondDetailsId}.");

        List<BondCapitalFlow> flows =
        [
            .. range.Opening.Select(p => new BondCapitalFlow(start.Date, p.Value * GetDetails(p.PartitionId).UnitValue, GetDetails(p.PartitionId).Currency)),
            .. range.Points.Select(p => new BondCapitalFlow(p.PostingDate.Date, p.ValueChange * GetDetails(p.PartitionId).UnitValue, GetDetails(p.PartitionId).Currency)),
        ];

        return await BucketCapital(flows, currency, start, end);
    }

    private async Task<List<TimeSeriesModel>> BucketCapital(List<BondCapitalFlow> flows, Currency currency, DateTime start, DateTime end)
    {
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
        end = DashboardReadCutoff.Clamp(end, DateTime.UtcNow);
        if (accountIds.Count == 0) return await GetOwnerClosingBalance(userId, start, end);

        var accountIdFilter = accountIds.ToHashSet();
        List<BondAccount> bondAccounts = [];
        await foreach (var account in financialAccountRepository.GetAccounts<BondAccount>(userId, start, end))
        {
            if (account is null) continue;
            if (!accountIdFilter.Contains(account.AccountId)) continue;
            bondAccounts.Add(account);
        }

        var bondDetails = (await bondDashboardContext.LoadReferencedDetailsAsync(bondAccounts)).Values.ToList();

        Dictionary<DateTime, decimal> prices = [];
        foreach (var account in bondAccounts)
        {
            // Per-date prices resolve through the request-scoped context, which memoizes each
            // account/bond/date so the dashboard's net-worth path never recomputes the same price.
            foreach (var price in account.GetDailyPrice(
                         DateOnly.FromDateTime(start),
                         DateOnly.FromDateTime(end),
                         bondDetails,
                         (entry, _, date) => bondDashboardContext.GetOrComputePrice(account.AccountId, entry, date)))
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

    private async Task<List<TimeSeriesModel>> GetOwnerClosingBalance(int userId, DateTime start, DateTime end)
    {
        var walk = new OwnerLedgerWalk(await bondTransactionReader.GetValueRange(userId, start, end));
        if (walk.IsEmpty) return [];

        await PooledBondValuation.LoadDetailsAsync(bondDashboardContext, walk);
        var valuation = new PooledBondValuation(bondDashboardContext, DateOnly.FromDateTime(end));

        // Every day carries an explicit value so a bond sold to zero does not keep yesterday's balance.
        var prices = walk.EachDayEnd(start, end).Select(day =>
            (day.Day, Value: day.Positions.Sum(p => valuation.ValueOn(p, DateOnly.FromDateTime(day.Day)))));

        return TimeBucketService.Get(prices)
                                 .Select(bucket => new TimeSeriesModel(bucket.Date, bucket.Objects.Last()))
                                 .ToList();
    }

    private async Task<List<TimeSeriesModel>> AggregateByDay(int userId, DateTime start, DateTime end, Func<decimal, bool> predicate, IReadOnlyCollection<int> accountIds)
    {
        end = DashboardReadCutoff.Clamp(end, DateTime.UtcNow);
        if (accountIds.Count == 0) return await AggregateOwnerByDay(userId, start, end, predicate);

        var accountIdFilter = accountIds.ToHashSet();
        List<BondAccount> bondAccounts = [];
        await foreach (var account in financialAccountRepository.GetAccounts<BondAccount>(userId, start, end))
        {
            if (account?.Entries is null) continue;
            if (!accountIdFilter.Contains(account.AccountId)) continue;
            bondAccounts.Add(account);
        }

        var bondDetails = await bondDashboardContext.LoadReferencedDetailsAsync(bondAccounts);

        Dictionary<DateTime, decimal> result = [];
        foreach (var account in bondAccounts)
        {
            foreach (var entry in account.Entries)
            {
                if (entry.PostingDate.Date < start.Date || entry.PostingDate.Date > end.Date) continue;
                if (!predicate(entry.ValueChange)) continue;
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

    private async Task<List<TimeSeriesModel>> AggregateOwnerByDay(int userId, DateTime start, DateTime end, Func<decimal, bool> predicate)
    {
        var points = (await bondTransactionReader.GetValueRange(userId, start, end)).Points
            .Where(p => p.PostingDate.Date >= start.Date && p.PostingDate.Date <= end.Date && predicate(p.ValueChange))
            .ToList();
        var bondDetails = await bondDashboardContext.LoadReferencedDetailsAsync(points.Select(p => p.PartitionId));

        Dictionary<DateTime, decimal> result = [];
        foreach (var point in points)
        {
            if (!bondDetails.TryGetValue(point.PartitionId, out var details))
                throw new InvalidOperationException($"Bond valuation requires details for bond id {point.PartitionId}.");

            var priceAtDate = point.ValueChange * details.UnitValue;
            result[point.PostingDate.Date] = result.GetValueOrDefault(point.PostingDate.Date) + (point.ValueChange >= 0 ? priceAtDate : -Math.Abs(priceAtDate));
        }

        return TimeBucketService.Get(result.OrderBy(x => x.Key).Select(x => (x.Key, x.Value)))
                                 .Select(bucket => new TimeSeriesModel(bucket.Date, bucket.Objects.Sum()))
                                 .ToList();
    }

    private sealed record BondCapitalFlow(DateTime Date, decimal Amount, Currency Currency);
}