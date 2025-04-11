using FinanceManager.Domain.Entities.Accounts;
using FinanceManager.Domain.Repositories.Account;

namespace FinanceManager.Domain.Providers
{
    public class AccountIdProvider
    {
        private readonly IAccountRepository<StockAccount> stockAccountRepository;
        private readonly IBankAccountRepository<BankAccount> bankAccountRepository;

        public AccountIdProvider(IAccountRepository<StockAccount> stockAccountRepository, IBankAccountRepository<BankAccount> bankAccountRepository)
        {
            this.stockAccountRepository = stockAccountRepository;
            this.bankAccountRepository = bankAccountRepository;
        }

        public int? GetMaxId()
        {
            List<int> ids = [];
            var stockAccountsLastId = stockAccountRepository.GetLastAccountId();
            if (stockAccountsLastId is not null)
                ids.Add(stockAccountsLastId.Value);

            var bankAccountsLastId = bankAccountRepository.GetLastAccountId();
            if (bankAccountsLastId is not null)
                ids.Add(bankAccountsLastId.Value);

            return ids.Any() ? ids.Max() : null;
        }
    }
}
