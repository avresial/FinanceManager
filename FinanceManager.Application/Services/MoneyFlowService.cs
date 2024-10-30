using FinanceManager.Core.Entities.Accounts;
using FinanceManager.Core.Entities.MoneyFlowModels;
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
                var latestStock = account.Entries.First();
                var stockPrice = await _stockRepository.GetStockPrice(latestStock.Ticker, end);

                foreach (var ticker in account.GetStoredTickers())
                {
                    var latestEntry = account.Entries.First(x => x.Ticker == ticker);

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
                var latestStock = account.Entries.First();
                var stockPrice = await _stockRepository.GetStockPrice(latestStock.Ticker, end);

                foreach (var ticker in account.GetStoredTickers())
                {
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
    }
}
