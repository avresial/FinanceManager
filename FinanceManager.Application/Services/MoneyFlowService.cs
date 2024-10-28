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

        public List<AssetsPerAcountEntry> GetAssetsPerAcount(DateTime start, DateTime end)
        {
            return new List<AssetsPerAcountEntry>();
        }

        public List<AssetsPerAcountEntry> GetAssetsPerAcount(BankAccount bankAccount, DateTime start, DateTime end)
        {
            return new List<AssetsPerAcountEntry>();
        }
    }
}
