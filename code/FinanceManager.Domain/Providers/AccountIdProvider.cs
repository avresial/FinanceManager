using FinanceManager.Domain.Entities.Accounts;
using FinanceManager.Domain.Repositories.Account;

namespace FinanceManager.Domain.Providers
{
    public class AccountIdProvider
    {
        private readonly IAccountRepository<StockAccount> stockAccountRepository;
        private readonly IAccountRepository<BankAccount> bankAccountRepository;

        public AccountIdProvider(IAccountRepository<StockAccount> stockAccountRepository, IAccountRepository<BankAccount> bankAccountRepository)
        {
            this.stockAccountRepository = stockAccountRepository;
            this.bankAccountRepository = bankAccountRepository;
        }

        public int? GetMaxId(int userId)
        {
            List<int> ids = [];
            var stockAccounts = stockAccountRepository.GetAvailableAccounts(userId);
            if (stockAccounts.Any()) ids.Add(stockAccounts.Max(x => x.AccountId));
            var bankAccounts = bankAccountRepository.GetAvailableAccounts(userId);
            if (bankAccounts.Any()) ids.Add(bankAccounts.Max(x => x.AccountId));
            return ids.Any() ? ids.Max() : null;
        }
    }
}
