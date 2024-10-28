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

        public async Task<List<AssetsPerAcountEntry>> GetEndAssetsPerAcount(DateTime start, DateTime end)
        {
            List<AssetsPerAcountEntry> result = new List<AssetsPerAcountEntry>();
            var BankAccounts = _bankAccountRepository.GetAccounts<BankAccount>(start, end);
            foreach (BankAccount account in BankAccounts.Where(x => x.Entries is not null && x.Entries.Any() && x.Entries.First().Value >= 0))
            {
                if (account is null || account.Entries is null) return result;

                result.Add(new AssetsPerAcountEntry()
                {
                    Cathegory = account.Name,
                    Value = account.Entries.First().Value
                });
            }

            var InvestmentAccounts = _bankAccountRepository.GetAccounts<InvestmentAccount>(start, end);
            foreach (InvestmentAccount account in InvestmentAccounts.Where(x => x.Entries is not null && x.Entries.Any() && x.Entries.First().Value >= 0))
            {
                if (account is null || account.Entries is null) return result;
                var latestStock = account.Entries.First();
                var stockPrice = await _stockRepository.GetStockPrice(latestStock.Ticker, end);
                result.Add(new AssetsPerAcountEntry()
                {
                    Cathegory = account.Name,
                    Value = account.Entries.First().Value * stockPrice.PricePerUnit
                });
            }

            return result;
        }

    }
}
