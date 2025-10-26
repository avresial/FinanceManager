using FinanceManager.Domain.Entities;
using FinanceManager.Domain.Entities.Accounts;
using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Extensions;
using FinanceManager.Domain.Repositories.Account;
using FinanceManager.Domain.Services;

namespace FinanceManager.Application.Services;

public class AssetsService(IFinancialAccountRepository financialAccountRepository, IStockPriceProvider stockPriceProvider) : IAssetsService
{
    public async Task<bool> IsAnyAccountWithAssets(int userId)
    {
        var end = DateTime.UtcNow;
        var start = end.AddDays(+1);

        var bankAccounts = financialAccountRepository.GetAccounts<BankAccount>(userId, start, end);
        if (await bankAccounts.AnyAsync(x => x.ContainsAssets)) return true;

        var stockAccounts = financialAccountRepository.GetAccounts<StockAccount>(userId, start, end);
        if (await stockAccounts.AnyAsync(x => x.ContainsAssets)) return true;

        return false;
    }
    public async IAsyncEnumerable<NameValueResult> GetEndAssetsPerAccount(int userId, Currency currency, DateTime asOfDate)
    {
        await foreach (var account in financialAccountRepository.GetAccounts<BankAccount>(userId, asOfDate.AddMinutes(-1), asOfDate))
        {
            if (!account.ContainsAssets) continue;

            yield return new()
            {
                Name = account.Name,
                Value = account.Entries.First().Value
            };
        }

        await foreach (StockAccount account in financialAccountRepository.GetAccounts<StockAccount>(userId, asOfDate.AddMinutes(-1), asOfDate))
        {
            if (!account.ContainsAssets) continue;

            NameValueResult result = new()
            {
                Name = account.Name,
                Value = 0
            };

            foreach (var ticker in account.GetStoredTickers())
            {
                var pricePerUnit = await stockPriceProvider.GetPricePerUnitAsync(ticker, currency, asOfDate);
                var latestEntry = account.Get(asOfDate).First(x => x.Ticker == ticker);

                result.Value += latestEntry.Value * pricePerUnit;
            }

            yield return result;
        }
    }
    public async IAsyncEnumerable<NameValueResult> GetEndAssetsPerType(int userId, Currency currency, DateTime asOfDate)
    {
        Dictionary<AccountLabel, NameValueResult> accountLabelResults = [];

        await foreach (BankAccount account in financialAccountRepository.GetAccounts<BankAccount>(userId, asOfDate.AddMinutes(-1), asOfDate))
        {
            if (!account.ContainsAssets) continue;

            var entry = account.Entries.Count != 0 ? account.Entries.FirstOrDefault() : account.NextOlderEntry;
            if (entry is null || entry.Value <= 0) continue;

            var existingResult = accountLabelResults.ContainsKey(account.AccountType) ? accountLabelResults[account.AccountType] : null;
            if (existingResult is null)
            {
                accountLabelResults.Add(account.AccountType, new()
                {
                    Name = account.AccountType.ToString(),
                    Value = entry.Value
                });
            }
            else
            {
                existingResult.Value += entry.Value;
            }
        }

        foreach (var value in accountLabelResults.Values)
            yield return value;

        Dictionary<InvestmentType, NameValueResult> investmentTypeResults = [];
        await foreach (StockAccount account in financialAccountRepository.GetAccounts<StockAccount>(userId, asOfDate.AddMinutes(-1), asOfDate))
        {
            if (!account.ContainsAssets) continue;

            foreach (var ticker in account.GetStoredTickers())
            {
                var pricePerUnit = await stockPriceProvider.GetPricePerUnitAsync(ticker, currency, asOfDate);
                var latestEntry = account.Entries.First(x => x.Ticker == ticker);

                if (!investmentTypeResults.TryGetValue(latestEntry.InvestmentType, out NameValueResult? existingResult))
                {
                    investmentTypeResults.Add(latestEntry.InvestmentType, new()
                    {
                        Name = latestEntry.InvestmentType.ToString(),
                        Value = latestEntry.Value * pricePerUnit
                    });
                }
                else
                {
                    existingResult.Value += latestEntry.Value * pricePerUnit;
                }
            }
        }

        foreach (var value in investmentTypeResults.Values)
            yield return value;

    }
    public async Task<List<TimeSeriesModel>> GetAssetsTimeSeries(int userId, Currency currency, DateTime start, DateTime end)
    {
        if (end > DateTime.UtcNow) end = DateTime.UtcNow;
        if (start == new DateTime()) return [];

        Dictionary<DateTime, decimal> prices = [];
        TimeSpan step = new(1, 0, 0, 0);

        await foreach (var account in financialAccountRepository.GetAccounts<BankAccount>(userId, start, end).Where(x => x.ContainsAssets))
        {
            var entry = account.GetThisOrNextOlder(end);
            if (entry is null || entry.Value <= 0) continue;

            decimal previousValue = entry.Value;

            for (DateTime date = start; date <= end; date = date.Add(step))
            {
                if (!prices.ContainsKey(date)) prices.Add(date, 0);

                var newestEntry = account.GetThisOrNextOlder(date);
                if (newestEntry is null)
                {
                    prices[date] += previousValue;
                    continue;
                }

                prices[date] += newestEntry.Value;
                previousValue = prices[date];
            }
        }

        await foreach (var account in financialAccountRepository.GetAccounts<StockAccount>(userId, start, end).Where(x => x.ContainsAssets))
        {
            foreach (var date in prices.Keys)
            {
                var tickerEntries = account.Get(date).GroupBy(x => x.Ticker).ToList();
                foreach (var ticker in account.GetStoredTickers())
                {
                    var entry = account.GetThisOrNextOlder(date, ticker);
                    if (entry is null) continue;

                    var pricePerUnit = await stockPriceProvider.GetPricePerUnitAsync(ticker, currency, end);
                    prices[date] += entry.Value * pricePerUnit;
                }
            }
        }

        var timeBucket = TimeBucketService.Get(prices.Select(x => (x.Key, x.Value))).OrderByDescending(x => x.Date);
        return timeBucket.Select(x => new TimeSeriesModel() { DateTime = x.Date, Value = x.Objects.Last() }).ToList();
    }
    public async Task<List<TimeSeriesModel>> GetAssetsTimeSeries(int userId, Currency currency, DateTime start, DateTime end, InvestmentType investmentType)
    {
        if (end > DateTime.UtcNow) end = DateTime.UtcNow;
        List<(DateTime, decimal)> assets = [];

        await foreach (var account in financialAccountRepository.GetAccounts<BankAccount>(userId, start, end).Where(x => x.ContainsAssets && x.AccountType.ToString() == investmentType.ToString()))
        {
            if (account is null || account.Entries is null) continue;

            assets.AddRange(account.Entries.GetAssets(start, end));
        }

        await foreach (var account in financialAccountRepository.GetAccounts<StockAccount>(userId, start, end).Where(x => x.ContainsAssets))
        {
            if (account is null || account.Entries is null) continue;

            assets.AddRange(await account.Entries.Where(x => x.InvestmentType == investmentType)
                .GetAssets(start, end, currency, stockPriceProvider.GetPricePerUnitAsync));
        }


        List<TimeSeriesModel> result = [];
        for (DateTime i = end; i >= start; i = i.AddDays(-1))
        {
            var assetsToSum = assets.Where(x => x.Item1 == i);
            result.Add(new()
            {
                DateTime = i,
                Value = assetsToSum.Any() ? assetsToSum.Sum(x => x.Item2) : 0,
            });
        }

        return result;
    }
}
