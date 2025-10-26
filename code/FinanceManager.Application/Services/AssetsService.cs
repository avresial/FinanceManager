using FinanceManager.Domain.Entities;
using FinanceManager.Domain.Entities.Accounts;
using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Extensions;
using FinanceManager.Domain.Repositories;
using FinanceManager.Domain.Repositories.Account;
using FinanceManager.Domain.Services;

namespace FinanceManager.Application.Services;

public class AssetsService(IFinancialAccountRepository financialAccountRepository, IStockPriceRepository stockRepository, ICurrencyExchangeService currencyExchangeService) : IAssetsService
{
    public async Task<bool> IsAnyAccountWithAssets(int userId)
    {
        var start = DateTime.UtcNow.AddDays(-1);
        var end = start.AddDays(1);

        var bankAccounts = financialAccountRepository.GetAccounts<BankAccount>(userId, start, end);

        await foreach (var bankAccount in bankAccounts)
        {
            if (bankAccount.Entries is not null && bankAccount.Entries.Count > 0)
            {
                var youngestEntry = bankAccount.Entries.FirstOrDefault();
                if (youngestEntry is not null && youngestEntry.Value > 0)
                    return true;
            }
            else if (bankAccount.NextOlderEntry is not null)
            {
                if (bankAccount.NextOlderEntry.Value > 0)
                    return true;
            }
        }

        var stockAccounts = financialAccountRepository.GetAccounts<StockAccount>(userId, start, end);

        await foreach (var stockAccount in stockAccounts)
        {
            if (stockAccount.Entries is not null && stockAccount.Entries.Count > 0)
            {
                var youngestEntry = stockAccount.Entries.FirstOrDefault();
                if (youngestEntry is not null && youngestEntry.Value > 0)
                    return true;
            }
            else if (stockAccount.NextOlderEntries.Any())
            {
                if (stockAccount.NextOlderEntries.Any(x => x.Value.Value > 0))
                    return true;
            }
        }

        return await Task.FromResult(false);
    }
    public async Task<List<NameValueResult>> GetEndAssetsPerAccount(int userId, Currency currency, DateTime start, DateTime end)
    {
        if (end > DateTime.UtcNow) end = DateTime.UtcNow;
        List<NameValueResult> result = [];

        await foreach (BankAccount account in financialAccountRepository.GetAccounts<BankAccount>(userId, start, end))
        {
            if (account is null || account.Entries is null) return result;

            var entry = account.Entries.Any() ? account.Entries.FirstOrDefault() : account.NextOlderEntry;

            if (entry is null || entry.Value <= 0) continue;

            result.Add(new NameValueResult()
            {
                Name = account.Name,
                Value = entry.Value
            });
        }

        var InvestmentAccounts = financialAccountRepository.GetAccounts<StockAccount>(userId, start, end);
        await foreach (StockAccount account in InvestmentAccounts.Where(x => x.Entries is not null && x.Entries.Count != 0 && x.Entries.First().Value >= 0))
        {
            if (account is null || account.Entries is null) return result;

            foreach (var ticker in account.GetStoredTickers())
            {
                var stockPrice = await stockRepository.GetThisOrNextOlder(ticker, end);
                decimal pricePerUnit = stockPrice is null ? 1 : await currencyExchangeService.GetPricePerUnit(stockPrice, currency, end);

                var latestEntry = account.Get(end).First(x => x.Ticker == ticker);

                var existingResult = result.FirstOrDefault(x => x.Name == account.Name);
                if (existingResult is null)
                {
                    result.Add(new NameValueResult()
                    {
                        Name = account.Name,
                        Value = latestEntry.Value * pricePerUnit
                    });
                }
                else
                {
                    existingResult.Value += latestEntry.Value * pricePerUnit;
                }
            }
        }

        return result;
    }
    public async Task<List<NameValueResult>> GetEndAssetsPerType(int userId, Currency currency, DateTime start, DateTime end)
    {
        List<NameValueResult> result = [];
        if (end > DateTime.UtcNow) end = DateTime.UtcNow;

        await foreach (BankAccount account in financialAccountRepository.GetAccounts<BankAccount>(userId, start, end))
        {
            if (account is null || account.Entries is null) return result;

            var entry = account.Entries.Any() ? account.Entries.FirstOrDefault() : account.NextOlderEntry;
            if (entry is null || entry.Value <= 0) continue;

            var existingResult = result.FirstOrDefault(x => x.Name == account.AccountType.ToString());
            if (existingResult is null)
            {
                result.Add(new NameValueResult()
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

        var InvestmentAccounts = financialAccountRepository.GetAccounts<StockAccount>(userId, start, end);
        await foreach (StockAccount account in InvestmentAccounts.Where(x => x.Entries is not null && x.Entries.Count != 0 && x.Entries.First().Value >= 0))
        {
            //var test = await account.GetDailyPrice(stockRepository.Get);
            if (account is null || account.Entries is null) return result;

            foreach (var ticker in account.GetStoredTickers())
            {
                var stockPrice = await stockRepository.Get(ticker, end);
                decimal pricePerUnit = stockPrice is null ? 1 : await currencyExchangeService.GetPricePerUnit(stockPrice, currency, end);

                var latestEntry = account.Entries.First(x => x.Ticker == ticker);

                var existingResult = result.FirstOrDefault(x => x.Name == latestEntry.InvestmentType.ToString());
                if (existingResult is null)
                {
                    result.Add(new NameValueResult()
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

        return result;
    }
    public async Task<List<TimeSeriesModel>> GetAssetsTimeSeries(int userId, Currency currency, DateTime start, DateTime end)
    {
        if (end > DateTime.UtcNow) end = DateTime.UtcNow;
        if (start == new DateTime()) return [];

        Dictionary<DateTime, decimal> prices = [];
        TimeSpan step = new(1, 0, 0, 0);

        await foreach (BankAccount account in financialAccountRepository.GetAccounts<BankAccount>(userId, start, end))
        {
            if (account is null || account.Entries is null) continue;

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

        var InvestmentAccounts = financialAccountRepository.GetAccounts<StockAccount>(userId, start, end);
        await foreach (StockAccount account in InvestmentAccounts.Where(x => x.Entries is not null && x.Entries.Count != 0 && x.Entries.First().Value >= 0))
        {
            if (account is null || account.Entries is null) continue;
            foreach (var date in prices.Keys)
            {
                var tickerEntries = account.Get(date).GroupBy(x => x.Ticker).ToList();
                foreach (var ticker in account.GetStoredTickers())
                {
                    var entry = account.GetThisOrNextOlder(date, ticker);
                    if (entry is null) continue;
                    var stockPrice = await stockRepository.Get(ticker, end);
                    decimal pricePerUnit = stockPrice is null ? 1 : await currencyExchangeService.GetPricePerUnit(stockPrice, currency, end);
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
        var BankAccounts = (financialAccountRepository.GetAccounts<BankAccount>(userId, start, end)).Where(x => x.AccountType.ToString() == investmentType.ToString());
        await foreach (BankAccount account in BankAccounts.Where(x => x.Entries is not null && x.Entries.Count != 0 && x.Entries.First().Value >= 0))
        {
            if (account is null || account.Entries is null) continue;

            assets.AddRange(account.Entries.GetAssets(start, end));
        }

        var InvestmentAccounts = financialAccountRepository.GetAccounts<StockAccount>(userId, start, end);
        await foreach (StockAccount account in InvestmentAccounts.Where(x => x.Entries is not null && x.Entries.Count != 0 && x.Entries.First().Value >= 0))
        {
            if (account is null || account.Entries is null) continue;

            assets.AddRange(await account.Entries.Where(x => x.InvestmentType == investmentType).ToList().GetAssets(start, end, stockRepository.Get));
        }


        List<TimeSeriesModel> result = [];
        for (DateTime i = end; i >= start; i = i.AddDays(-1))
        {
            var assetsToSum = assets.Where(x => x.Item1 == i);
            result.Add(new TimeSeriesModel
            {
                DateTime = i,
                Value = assetsToSum.Any() ? assetsToSum.Sum(x => x.Item2) : 0,
            });
        }
        return result;
    }
}
