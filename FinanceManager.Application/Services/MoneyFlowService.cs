using FinanceManager.Core.Entities.Accounts;
using FinanceManager.Core.Entities.MoneyFlowModels;
using FinanceManager.Core.Enums;
using FinanceManager.Core.Repositories;
using FinanceManager.Core.Services;

namespace FinanceManager.Application.Services
{
    public class MoneyFlowService : IMoneyFlowService
    {
        private IFinancalAccountRepository _bankAccountRepository;
        private ISettingsService _settingsService;
        private IStockRepository _stockRepository;

        public MoneyFlowService(IFinancalAccountRepository bankAccountRepository, IStockRepository stockRepository, ISettingsService settingsService)
        {
            _bankAccountRepository = bankAccountRepository;
            _stockRepository = stockRepository;
            _settingsService = settingsService;
        }

        public async Task<List<AssetEntry>> GetEndAssetsPerAcount(DateTime start, DateTime end)
        {
            List<AssetEntry> result = new List<AssetEntry>();
            var BankAccounts = _bankAccountRepository.GetAccounts<BankAccount>(start, end);
            foreach (BankAccount account in BankAccounts.Where(x => x.Entries is not null && x.Entries.Any() && x.Entries.First().Value >= 0))
            {
                if (account is null || account.Entries is null) return result;

                result.Add(new AssetEntry()
                {
                    Name = account.Name,
                    Value = account.Entries.First().Value
                });
            }

            var InvestmentAccounts = _bankAccountRepository.GetAccounts<InvestmentAccount>(start, end);
            foreach (InvestmentAccount account in InvestmentAccounts.Where(x => x.Entries is not null && x.Entries.Any() && x.Entries.First().Value >= 0))
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
            List<AssetEntry> result = new List<AssetEntry>();
            var BankAccounts = _bankAccountRepository.GetAccounts<BankAccount>(start, end);
            foreach (BankAccount account in BankAccounts.Where(x => x.Entries is not null && x.Entries.Any() && x.Entries.First().Value >= 0))
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
            foreach (InvestmentAccount account in InvestmentAccounts.Where(x => x.Entries is not null && x.Entries.Any() && x.Entries.First().Value >= 0))
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
            if (start == new DateTime()) return new List<TimeSeriesModel>();

            Dictionary<DateTime, decimal> prices = new Dictionary<DateTime, decimal>();

            List<DateTime> allDates = new List<DateTime>();
            try
            {
                for (DateTime i = end; i >= start; i = i.AddDays(-1)) allDates.Add(i);

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
            var BankAccounts = _bankAccountRepository.GetAccounts<BankAccount>(start, end);
            foreach (BankAccount account in BankAccounts.Where(x => x.Entries is not null && x.Entries.Any() && x.Entries.First().Value >= 0))
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
            foreach (InvestmentAccount account in InvestmentAccounts.Where(x => x.Entries is not null && x.Entries.Any() && x.Entries.First().Value >= 0))
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

        public async Task<List<TimeSeriesModel>> GetAssetsPerTypeTimeSeries(DateTime start, DateTime end, InvestmentType investmentType)
        {
            throw new NotImplementedException();
        }
    }
}
