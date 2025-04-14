using FinanceManager.Domain.Entities.Accounts;
using FinanceManager.Domain.Repositories.Account;

namespace FinanceManager.Domain.Providers;

public class AccountIdProvider
{
    private object _lockObject = new object();
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
        int? stockAccountsLastId = null;
        int? bankAccountsLastId = null;
        lock (_lockObject)
        {
            stockAccountsLastId = stockAccountRepository.GetLastAccountId();
            bankAccountsLastId = bankAccountRepository.GetLastAccountId();
        }

        if (stockAccountsLastId is not null)
            ids.Add(stockAccountsLastId.Value);

        if (bankAccountsLastId is not null)
            ids.Add(bankAccountsLastId.Value);

        return ids.Count != 0 ? ids.Max() : null;
    }
}
