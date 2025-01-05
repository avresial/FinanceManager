using FinanceManager.Core.Entities.Accounts;
using FinanceManager.Core.Entities.MoneyFlowModels;
using FinanceManager.Core.Enums;
using FinanceManager.Core.Extensions;
using FinanceManager.Core.Repositories;
using FinanceManager.Core.Services;

namespace FinanceManager.Application.Services
{
    public class MoneyFlowService(IFinancalAccountRepository bankAccountRepository, IStockRepository stockRepository) : IMoneyFlowService
    {
        private readonly IFinancalAccountRepository _bankAccountRepository = bankAccountRepository;
        private readonly IStockRepository _stockRepository = stockRepository;

        public async Task<List<AssetEntry>> GetEndAssetsPerAcount(DateTime start, DateTime end)
        {
            List<AssetEntry> result = [];
            var BankAccounts = _bankAccountRepository.GetAccounts<BankAccount>(start, end);
            foreach (BankAccount account in BankAccounts.Where(x => x.Entries is not null && x.Entries.Count != 0 && x.Entries.First().Value >= 0))
            {
                if (account is null || account.Entries is null) return result;

                result.Add(new AssetEntry()
                {
                    Name = account.Name,
                    Value = account.Entries.First().Value
                });
            }

            var InvestmentAccounts = _bankAccountRepository.GetAccounts<InvestmentAccount>(start, end);
            foreach (InvestmentAccount account in InvestmentAccounts.Where(x => x.Entries is not null && x.Entries.Count != 0 && x.Entries.First().Value >= 0))
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
        public async Task<List<AssetEntry>> GetEndAssetsPerType(DateTime start, DateTime end)
        {
            List<AssetEntry> result = [];
            var BankAccounts = _bankAccountRepository.GetAccounts<BankAccount>(start, end);
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

            var InvestmentAccounts = _bankAccountRepository.GetAccounts<InvestmentAccount>(start, end);
            foreach (InvestmentAccount account in InvestmentAccounts.Where(x => x.Entries is not null && x.Entries.Count != 0 && x.Entries.First().Value >= 0))
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
        public async Task<List<TimeSeriesModel>> GetAssetsTimeSeries(DateTime start, DateTime end)
        {
            if (start == new DateTime()) return [];

            Dictionary<DateTime, decimal> prices = [];

            List<DateTime> allDates = [];
            try
            {
                for (DateTime i = end; i >= start; i = i.AddDays(-1)) allDates.Add(i);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
            var BankAccounts = _bankAccountRepository.GetAccounts<BankAccount>(start, end);
            foreach (BankAccount account in BankAccounts.Where(x => x.Entries is not null && x.Entries.Count != 0 && x.Entries.First().Value >= 0))
            {
                if (account is null || account.Entries is null) continue;

                foreach (var date in allDates)
                {
                    var newestEntry = account.Get(date).OrderByDescending(x => x.PostingDate).FirstOrDefault();
                    if (newestEntry is null) continue;

                    if (!prices.ContainsKey(date))
                    {
                        prices.Add(date, newestEntry.Value);
                    }
                    else
                    {
                        prices[date] += newestEntry.Value;
                    }
                }
            }

            var InvestmentAccounts = _bankAccountRepository.GetAccounts<InvestmentAccount>(start, end);
            foreach (InvestmentAccount account in InvestmentAccounts.Where(x => x.Entries is not null && x.Entries.Count != 0 && x.Entries.First().Value >= 0))
            {
                if (account is null || account.Entries is null) continue;

                foreach (var date in allDates)
                {
                    var tickerEntries = account.Get(date).GroupBy(x => x.Ticker);

                    foreach (var group in tickerEntries)
                    {
                        var newestEntry = group.OrderByDescending(x => x.PostingDate).FirstOrDefault();
                        if (newestEntry is null) continue;
                        var price = await _stockRepository.GetStockPrice(newestEntry.Ticker, date);
                        if (!prices.ContainsKey(date))
                        {
                            prices.Add(date, newestEntry.Value * price.PricePerUnit);
                        }
                        else
                        {
                            prices[date] += newestEntry.Value * price.PricePerUnit;
                        }
                    }
                }
            }



            return prices.Select(x => new TimeSeriesModel() { DateTime = x.Key, Value = x.Value })
                        .OrderByDescending(x => x.DateTime)
                        .ToList();
        }
        public async Task<List<TimeSeriesModel>> GetAssetsTimeSeries(DateTime start, DateTime end, InvestmentType investmentType)
        {
            List<(DateTime, decimal)> assets = [];
            var BankAccounts = _bankAccountRepository.GetAccounts<BankAccount>(start, end).Where(x => x.AccountType.ToString() == investmentType.ToString());
            foreach (BankAccount account in BankAccounts.Where(x => x.Entries is not null && x.Entries.Count != 0 && x.Entries.First().Value >= 0))
            {
                if (account is null || account.Entries is null) continue;

                assets.AddRange(account.Entries.GetAssets(start, end));
            }

            var InvestmentAccounts = _bankAccountRepository.GetAccounts<InvestmentAccount>(start, end);
            foreach (InvestmentAccount account in InvestmentAccounts.Where(x => x.Entries is not null && x.Entries.Count != 0 && x.Entries.First().Value >= 0))
            {
                if (account is null || account.Entries is null) continue;

                assets.AddRange(await account.Entries.Where(x => x.InvestmentType == investmentType).ToList().GetAssets(start, end, _stockRepository.GetStockPrice));
            }


            List<TimeSeriesModel> result = [];
            for (DateTime i = end; i >= start; i = i.AddDays(-1))
            {
                var assetsToSum = assets.Where(x => x.Item1 == i);
                if (!assetsToSum.Any()) continue;

                result.Add(new TimeSeriesModel()
                {
                    DateTime = i,
                    Value = assetsToSum.Sum(x => x.Item2),
                });
            }
            return result;
        }
        public async Task<decimal?> GetNetWorth(DateTime date)
        {
            decimal result = 0;

            var BankAccounts = _bankAccountRepository.GetAccounts<BankAccount>(date.Date, date);
            foreach (var bankAccount in BankAccounts)
            {
                if (bankAccount.OlderThenLoadedEntry is null) continue;
                if (bankAccount.Entries is null) continue;

                var newBankAccount = _bankAccountRepository.GetAccount<BankAccount>(bankAccount.Id, bankAccount.OlderThenLoadedEntry.Value, bankAccount.OlderThenLoadedEntry.Value.AddSeconds(1));
                if (newBankAccount is not null && newBankAccount.Entries is not null)
                    bankAccount.Add(newBankAccount.Entries, false);
            }

            var InvestmentAccounts = _bankAccountRepository.GetAccounts<InvestmentAccount>(date.Date, date);
            foreach (var investmentAccount in InvestmentAccounts)
            {
                foreach (var item in investmentAccount.OlderThenLoadedEntry)
                {
                    if (investmentAccount.Entries is null) continue;
                    if (investmentAccount.Entries.Any(x => x.Ticker == item.Key)) continue;

                    var newInvestmentAccount = _bankAccountRepository.GetAccount<InvestmentAccount>(investmentAccount.Id, item.Value, item.Value.AddSeconds(1));
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

            foreach (InvestmentAccount account in InvestmentAccounts.Where(x => x.Entries is not null && x.Entries.Count != 0))
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
        public async Task<Dictionary<DateTime, decimal>> GetNetWorth(DateTime start, DateTime end)
        {
            if (start == new DateTime()) return [];

            Dictionary<DateTime, decimal> result = [];

            for (DateTime date = end; date >= start; date = date.AddDays(-1))
            {
                var netWorth = await GetNetWorth(date);
                if (netWorth is null) continue;
                result.Add(date, netWorth.Value);
            }
            return result;
        }
    }
}
