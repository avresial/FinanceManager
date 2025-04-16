using FinanceManager.Domain.Entities.Accounts;
using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Extensions;
using FinanceManager.Domain.Repositories;
using FinanceManager.Domain.Repositories.Account;
using FinanceManager.Domain.Services;

namespace FinanceManager.Application.Services;

public class MoneyFlowService(IFinancalAccountRepository bankAccountRepository, IStockRepository stockRepository) : IMoneyFlowService
{
    private readonly IFinancalAccountRepository _financialAccountService = bankAccountRepository;
    private readonly IStockRepository _stockRepository = stockRepository;

    public async Task<List<AssetEntry>> GetEndAssetsPerAcount(int userId, DateTime start, DateTime end)
    {
        List<AssetEntry> result = [];
        var BankAccounts = _financialAccountService.GetAccounts<BankAccount>(userId, start, end);
        foreach (BankAccount account in BankAccounts.Where(x => x.Entries is not null && x.Entries.Count != 0 && x.Entries.First().Value >= 0))
        {
            if (account is null || account.Entries is null) return result;

            result.Add(new AssetEntry()
            {
                Name = account.Name,
                Value = account.Entries.First().Value
            });
        }

        var InvestmentAccounts = _financialAccountService.GetAccounts<StockAccount>(userId, start, end);
        foreach (StockAccount account in InvestmentAccounts.Where(x => x.Entries is not null && x.Entries.Count != 0 && x.Entries.First().Value >= 0))
        {
            if (account is null || account.Entries is null) return result;

            foreach (var ticker in account.GetStoredTickers())
            {
                var stockPrice = await _stockRepository.GetStockPrice(ticker, end);
                var latestEntry = account.Get(end).First(x => x.Ticker == ticker);

                var existingResult = result.FirstOrDefault(x => x.Name == account.Name);
                if (existingResult is null)
                {
                    result.Add(new AssetEntry()
                    {
                        Name = account.Name,
                        Value = latestEntry.Value * stockPrice.PricePerUnit
                    });
                }
                else
                {
                    existingResult.Value += latestEntry.Value * stockPrice.PricePerUnit;
                }
            }
        }

        return result;
    }
    public async Task<List<AssetEntry>> GetEndAssetsPerType(int userId, DateTime start, DateTime end)
    {
        List<AssetEntry> result = [];
        var BankAccounts = _financialAccountService.GetAccounts<BankAccount>(userId, start, end);
        foreach (BankAccount account in BankAccounts.Where(x => x.Entries is not null && x.Entries.Count != 0 && x.Entries.First().Value >= 0))
        {
            if (account is null || account.Entries is null) return result;
            var existingResult = result.FirstOrDefault(x => x.Name == account.AccountType.ToString());
            if (existingResult is null)
            {
                result.Add(new AssetEntry()
                {
                    Name = account.AccountType.ToString(),
                    Value = account.Entries.First().Value
                });
            }
            else
            {
                existingResult.Value += account.Entries.First().Value;
            }
        }

        var InvestmentAccounts = _financialAccountService.GetAccounts<StockAccount>(userId, start, end);
        foreach (StockAccount account in InvestmentAccounts.Where(x => x.Entries is not null && x.Entries.Count != 0 && x.Entries.First().Value >= 0))
        {
            if (account is null || account.Entries is null) return result;

            foreach (var ticker in account.GetStoredTickers())
            {
                var stockPrice = await _stockRepository.GetStockPrice(ticker, end);
                var latestEntry = account.Entries.First(x => x.Ticker == ticker);

                var existingResult = result.FirstOrDefault(x => x.Name == latestEntry.InvestmentType.ToString());
                if (existingResult is null)
                {
                    result.Add(new AssetEntry()
                    {
                        Name = latestEntry.InvestmentType.ToString(),
                        Value = latestEntry.Value * stockPrice.PricePerUnit
                    });
                }
                else
                {
                    existingResult.Value += latestEntry.Value * stockPrice.PricePerUnit;
                }
            }
        }

        return result;
    }
    public async Task<List<TimeSeriesModel>> GetAssetsTimeSeries(int userId, DateTime start, DateTime end)
    {
        if (start == new DateTime()) return [];

        Dictionary<DateTime, decimal> prices = [];
        TimeSpan step = new TimeSpan(1, 0, 0, 0);

        var BankAccounts = _financialAccountService.GetAccounts<BankAccount>(userId, start, end);
        foreach (BankAccount account in BankAccounts.Where(x => x.Entries is not null && x.Entries.Count != 0 && x.Entries.First().Value >= 0))
        {
            if (account is null || account.Entries is null) continue;
            decimal previousValue = account.Entries.Last().Value - account.Entries.Last().ValueChange;

            for (DateTime date = start; date <= end; date = date.Add(step))
            {
                if (!prices.ContainsKey(date)) prices.Add(date, 0);

                var newestEntry = account.Get(date).OrderByDescending(x => x.PostingDate).FirstOrDefault();
                if (newestEntry is null)
                {
                    prices[date] += previousValue;
                    continue;
                }

                prices[date] += newestEntry.Value;
                previousValue = prices[date];
            }
        }

        var InvestmentAccounts = _financialAccountService.GetAccounts<StockAccount>(userId, start, end);
        foreach (StockAccount account in InvestmentAccounts.Where(x => x.Entries is not null && x.Entries.Count != 0 && x.Entries.First().Value >= 0))
        {
            if (account is null || account.Entries is null) continue;
            foreach (var date in prices.Keys)
            {
                var tickerEntries = account.Get(date).GroupBy(x => x.Ticker);

                foreach (var group in tickerEntries)
                {
                    var newestEntry = group.OrderByDescending(x => x.PostingDate).FirstOrDefault();
                    if (newestEntry is null)
                        continue;
                    var price = await _stockRepository.GetStockPrice(newestEntry.Ticker, date);

                    prices[date] += newestEntry.Value * price.PricePerUnit;
                }
            }
        }

        return prices.Select(x => new TimeSeriesModel() { DateTime = x.Key, Value = x.Value })
                    .OrderByDescending(x => x.DateTime)
                    .ToList();
    }
    public async Task<List<TimeSeriesModel>> GetAssetsTimeSeries(int userId, DateTime start, DateTime end, InvestmentType investmentType)
    {
        List<(DateTime, decimal)> assets = [];
        var BankAccounts = _financialAccountService.GetAccounts<BankAccount>(userId, start, end).Where(x => x.AccountType.ToString() == investmentType.ToString());
        foreach (BankAccount account in BankAccounts.Where(x => x.Entries is not null && x.Entries.Count != 0 && x.Entries.First().Value >= 0))
        {
            if (account is null || account.Entries is null) continue;

            assets.AddRange(account.Entries.GetAssets(start, end));
        }

        var InvestmentAccounts = _financialAccountService.GetAccounts<StockAccount>(userId, start, end);
        foreach (StockAccount account in InvestmentAccounts.Where(x => x.Entries is not null && x.Entries.Count != 0 && x.Entries.First().Value >= 0))
        {
            if (account is null || account.Entries is null) continue;

            assets.AddRange(await account.Entries.Where(x => x.InvestmentType == investmentType).ToList().GetAssets(start, end, _stockRepository.GetStockPrice));
        }


        List<TimeSeriesModel> result = [];
        for (DateTime i = end; i >= start; i = i.AddDays(-1))
        {
            var assetsToSum = assets.Where(x => x.Item1 == i);
            if (!assetsToSum.Any())
            {
                result.Add(new TimeSeriesModel()
                {
                    DateTime = i,
                    Value = 0,
                });
            }

            result.Add(new TimeSeriesModel()
            {
                DateTime = i,
                Value = assetsToSum.Sum(x => x.Item2),
            });
        }
        return result;
    }
    public async Task<decimal?> GetNetWorth(int userId, DateTime date)
    {
        decimal result = 0;

        var BankAccounts = _financialAccountService.GetAccounts<BankAccount>(userId, date.Date, date).ToList();
        foreach (var bankAccount in BankAccounts)
        {
            if (bankAccount.OlderThenLoadedEntry is null) continue;
            if (bankAccount.Entries is null) continue;

            var newBankAccount = _financialAccountService.GetAccount<BankAccount>(userId, bankAccount.AccountId, bankAccount.OlderThenLoadedEntry.Value, bankAccount.OlderThenLoadedEntry.Value.AddSeconds(1));
            if (newBankAccount is not null && newBankAccount.Entries is not null)
                bankAccount.Add(newBankAccount.Entries, false);
        }

        var InvestmentAccounts = _financialAccountService.GetAccounts<StockAccount>(userId, date.Date, date);
        foreach (var investmentAccount in InvestmentAccounts)
        {
            foreach (var item in investmentAccount.OlderThenLoadedEntry)
            {
                if (investmentAccount.Entries is null) continue;
                if (investmentAccount.Entries.Any(x => x.Ticker == item.Key)) continue;

                var newInvestmentAccount = _financialAccountService.GetAccount<StockAccount>(userId, investmentAccount.AccountId, item.Value, item.Value.AddSeconds(1));
                if (newInvestmentAccount is not null && newInvestmentAccount.Entries is not null)
                    investmentAccount.Add(newInvestmentAccount.Entries, false);
            }
        }

        foreach (BankAccount account in BankAccounts.Where(x => x.Entries is not null && x.Entries.Count != 0))
        {
            if (account is null || account.Entries is null) continue;

            var newestEntry = account.Get(date).OrderByDescending(x => x.PostingDate).FirstOrDefault();
            if (newestEntry is null) continue;

            result += newestEntry.Value;
        }

        foreach (StockAccount account in InvestmentAccounts.Where(x => x.Entries is not null && x.Entries.Count != 0))
        {
            if (account is null || account.Entries is null) continue;

            foreach (var tickerGroup in account.Get(date).GroupBy(x => x.Ticker))
            {
                var newestEntry = tickerGroup.OrderByDescending(x => x.PostingDate).FirstOrDefault();
                if (newestEntry is null) continue;

                var price = await _stockRepository.GetStockPrice(newestEntry.Ticker, date);
                result += newestEntry.Value * price.PricePerUnit;
            }
        }

        return result;
    }
    public async Task<Dictionary<DateTime, decimal>> GetNetWorth(int userId, DateTime start, DateTime end)
    {
        if (start == new DateTime()) return [];

        Dictionary<DateTime, decimal> result = [];

        for (DateTime date = end; date >= start; date = date.AddDays(-1))
        {
            var netWorth = await GetNetWorth(userId, date);
            if (netWorth is null) continue;
            result.Add(date, netWorth.Value);
        }
        return result;
    }

    public async Task<List<TimeSeriesModel>> GetIncome(int userId, DateTime start, DateTime end, TimeSpan? step = null)
    {
        TimeSpan timeSeriesStep = step ?? new TimeSpan(1, 0, 0, 0);
        IEnumerable<BankAccount> bankAccounts = [];

        try
        {
            bankAccounts = _financialAccountService.GetAccounts<BankAccount>(userId, start, end);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
        }

        Dictionary<DateTime, decimal> result = [];

        for (var date = end; date >= start; date = date.Add(-timeSeriesStep))
        {
            result.Add(date, 0);

            foreach (var account in bankAccounts)
            {
                if (account.Entries is null) continue;
                var entries = account.Get(date);

                foreach (var entry in entries.Where(x => x.ValueChange > 0 && x.PostingDate.Date == date.Date).Select(x => x as FinancialEntryBase))
                    result[date] += entry.ValueChange;
            }
        }

        return result.Select(x => new TimeSeriesModel() { DateTime = x.Key, Value = x.Value }).ToList();
    }

    public async Task<List<TimeSeriesModel>> GetSpending(int userId, DateTime start, DateTime end, TimeSpan? step = null)
    {
        TimeSpan timeSeriesStep = step ?? new TimeSpan(1, 0, 0, 0);
        IEnumerable<BankAccount> bankAccounts = [];

        try
        {
            bankAccounts = _financialAccountService.GetAccounts<BankAccount>(userId, start, end);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
        }

        Dictionary<DateTime, decimal> result = [];

        for (var date = end; date >= start; date = date.Add(-timeSeriesStep))
        {
            result.Add(date, 0);

            foreach (var account in bankAccounts)
            {
                if (account.Entries is null) continue;
                var entries = account.Get(date);

                foreach (var entry in entries.Where(x => x.ValueChange < 0 && x.PostingDate.Date == date.Date).Select(x => x as FinancialEntryBase))
                    result[date] += entry.ValueChange;
            }
        }

        return result.Select(x => new TimeSeriesModel() { DateTime = x.Key, Value = x.Value }).ToList();
    }

    public async Task<bool> IsAnyAccountWithAssets(int userId)
    {
        var start = DateTime.UtcNow.AddDays(-1);
        var end = start.AddDays(1);

        var bankAccounts = _financialAccountService.GetAccounts<BankAccount>(userId, start, end);
        foreach (var bankAccount in bankAccounts)
        {
            if (bankAccount.Entries is not null && bankAccount.Entries.Count > 0)
            {
                var youngestEntry = bankAccount.Entries.FirstOrDefault();
                if (youngestEntry is not null && youngestEntry.Value > 0)
                    return true;
            }

            if (bankAccount.OlderThenLoadedEntry is not null)
            {
                var newBankAccount = _financialAccountService.GetAccount<BankAccount>(userId, bankAccount.AccountId, bankAccount.OlderThenLoadedEntry.Value, bankAccount.OlderThenLoadedEntry.Value.AddSeconds(1));
                if (newBankAccount is null || newBankAccount.Entries is null) continue;
                var youngestEntry = newBankAccount.Entries.FirstOrDefault();
                if (youngestEntry is not null && youngestEntry.Value > 0)
                    return true;
            }
        }

        return await Task.FromResult(false);
    }

    public async Task<bool> IsAnyAccountWithLiabilities(int userId)
    {
        var BankAccounts = _financialAccountService.GetAccounts<BankAccount>(userId, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow).ToList();
        foreach (var bankAccount in BankAccounts)
        {
            if (bankAccount.Entries is not null && bankAccount.Entries.Count > 0)
            {
                var youngestEntry = bankAccount.Entries.FirstOrDefault();
                if (youngestEntry is not null && youngestEntry.Value < 0)
                    return true;
            }

            if (bankAccount.OlderThenLoadedEntry is not null)
            {
                var newBankAccount = _financialAccountService.GetAccount<BankAccount>(userId, bankAccount.AccountId, bankAccount.OlderThenLoadedEntry.Value, bankAccount.YoungerThenLoadedEntry.Value.AddSeconds(1));
                if (newBankAccount is null || newBankAccount.Entries is null) continue;
                var youngestEntry = newBankAccount.Entries.FirstOrDefault();
                if (youngestEntry is not null && youngestEntry.Value < 0)
                    return true;
            }
        }

        return await Task.FromResult(false);
    }
}
