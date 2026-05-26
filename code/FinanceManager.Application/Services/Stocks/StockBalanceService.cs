using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.Entities.Stocks;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Repositories.Account;
using FinanceManager.Domain.Services;

namespace FinanceManager.Application.Services.Stocks;

internal class StockBalanceService(IFinancialAccountRepository financialAccountRepository, IStockPriceProvider stockPriceProvider) : IBalanceServiceTyped
{
    public bool IsOfType<T>() => typeof(T) == typeof(StockAccount);

    public Task<List<TimeSeriesModel>> GetInflow(int userId, Currency currency, DateTime start, DateTime end) =>
        GetInflow(userId, currency, start, end, []);

    public Task<List<TimeSeriesModel>> GetInflow(int userId, Currency currency, DateTime start, DateTime end, IReadOnlyCollection<int> accountIds) =>
        AggregateByDay(userId, currency, start, end, entry => entry.ValueChange > 0, accountIds);

    public Task<List<TimeSeriesModel>> GetOutflow(int userId, Currency currency, DateTime start, DateTime end) =>
        GetOutflow(userId, currency, start, end, []);

    public Task<List<TimeSeriesModel>> GetOutflow(int userId, Currency currency, DateTime start, DateTime end, IReadOnlyCollection<int> accountIds) =>
        AggregateByDay(userId, currency, start, end, entry => entry.ValueChange < 0, accountIds);

    public Task<List<TimeSeriesModel>> GetNetCashFlow(int userId, Currency currency, DateTime start, DateTime end) =>
        GetNetCashFlow(userId, currency, start, end, []);

    public Task<List<TimeSeriesModel>> GetNetCashFlow(int userId, Currency currency, DateTime start, DateTime end, IReadOnlyCollection<int> accountIds) =>
        AggregateByDay(userId, currency, start, end, _ => true, accountIds);

    public Task<List<TimeSeriesModel>> GetClosingBalance(int userId, Currency currency, DateTime start, DateTime end) =>
        GetClosingBalance(userId, currency, start, end, []);

    public async Task<List<TimeSeriesModel>> GetClosingBalance(int userId, Currency currency, DateTime start, DateTime end, IReadOnlyCollection<int> accountIds)
    {
        if (end > DateTime.UtcNow) end = DateTime.UtcNow;

        Dictionary<DateTime, decimal> prices = [];
        var accountIdFilter = accountIds.Count > 0 ? accountIds.ToHashSet() : [];
        List<StockAccount> accounts = [];

        await foreach (var account in financialAccountRepository.GetAccounts<StockAccount>(userId, start, end))
        {
            if (account is null) continue;
            if (accountIdFilter.Count > 0 && !accountIdFilter.Contains(account.AccountId)) continue;

            accounts.Add(account);
        }

        Dictionary<string, IReadOnlyDictionary<DateTime, decimal>> pricesByTicker = new(StringComparer.OrdinalIgnoreCase);
        var tickers = accounts.SelectMany(x => x.GetStoredTickers()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (tickers.Count > 0)
        {
            var preloadTasks = tickers.ToDictionary(
                ticker => ticker,
                ticker => stockPriceProvider.GetPricePerUnitSeriesAsync(ticker, currency, start.Date, end.Date),
                StringComparer.OrdinalIgnoreCase);

            await Task.WhenAll(preloadTasks.Values);
            foreach (var preloadTask in preloadTasks)
                pricesByTicker[preloadTask.Key] = await preloadTask.Value;
        }

        foreach (var account in accounts)
        {
            var storedTickers = account.GetStoredTickers();

            for (var date = start.Date; date <= end.Date; date = date.AddDays(1))
            {
                if (!prices.ContainsKey(date)) prices[date] = 0;

                foreach (var ticker in storedTickers)
                {
                    var entry = account.GetThisOrNextOlder(date, ticker);
                    if (entry is null) continue;

                    if (!pricesByTicker.TryGetValue(ticker, out var tickerPrices)) continue;
                    if (!tickerPrices.TryGetValue(date, out var pricePerUnit)) continue;

                    prices[date] += entry.Value * pricePerUnit;
                }
            }
        }

        return TimeBucketService.Get(prices.OrderBy(x => x.Key).Select(x => (x.Key, x.Value)))
                                .Select(bucket => new TimeSeriesModel(bucket.Date, bucket.Objects.Last()))
                                .ToList();
    }

    private async Task<List<TimeSeriesModel>> AggregateByDay(int userId, Currency currency, DateTime start, DateTime end, Func<StockAccountEntry, bool> predicate, IReadOnlyCollection<int> accountIds)
    {
        if (end > DateTime.UtcNow) end = DateTime.UtcNow;

        Dictionary<DateTime, decimal> result = [];
        var accountIdFilter = accountIds.Count > 0 ? accountIds.ToHashSet() : [];
        List<StockAccount> accounts = [];

        await foreach (var account in financialAccountRepository.GetAccounts<StockAccount>(userId, start, end))
        {
            if (account?.Entries is null) continue;
            if (accountIdFilter.Count > 0 && !accountIdFilter.Contains(account.AccountId)) continue;

            accounts.Add(account);
        }

        Dictionary<string, IReadOnlyDictionary<DateTime, decimal>> pricesByTicker = new(StringComparer.OrdinalIgnoreCase);
        var tickers = accounts.SelectMany(x => x.GetStoredTickers()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (tickers.Count > 0)
        {
            var preloadTasks = tickers.ToDictionary(
                ticker => ticker,
                ticker => stockPriceProvider.GetPricePerUnitSeriesAsync(ticker, currency, start.Date, end.Date),
                StringComparer.OrdinalIgnoreCase);

            await Task.WhenAll(preloadTasks.Values);
            foreach (var preloadTask in preloadTasks)
                pricesByTicker[preloadTask.Key] = await preloadTask.Value;
        }

        foreach (var account in accounts)
        {
            foreach (var entry in account.Entries)
            {
                if (entry.PostingDate.Date < start.Date || entry.PostingDate.Date > end.Date) continue;
                if (!predicate(entry)) continue;

                if (!pricesByTicker.TryGetValue(entry.Isin, out var tickerPrices)) continue;
                if (!tickerPrices.TryGetValue(entry.PostingDate.Date, out var pricePerUnit)) continue;

                if (!result.ContainsKey(entry.PostingDate.Date)) result[entry.PostingDate.Date] = 0;

                result[entry.PostingDate.Date] += entry.ValueChange * pricePerUnit;
            }
        }

        return TimeBucketService.Get(result.OrderBy(x => x.Key).Select(x => (x.Key, x.Value)))
                                .Select(bucket => new TimeSeriesModel(bucket.Date, bucket.Objects.Sum()))
                                .ToList();
    }
}