using FinanceManager.Domain.Entities.Accounts;
using FinanceManager.Domain.Repositories.Account;

namespace FinanceManager.Domain.Providers;

public class AccountIdProvider(IAccountRepository<StockAccount> stockAccountRepository, IBankAccountRepository<BankAccount> bankAccountRepository)
{
    public async Task<int?> GetMaxId()
    {
        List<int> ids = [];
        int? stockAccountsLastId = null;
        int? bankAccountsLastId = null;

        stockAccountsLastId = await stockAccountRepository.GetLastAccountId();
        bankAccountsLastId = await bankAccountRepository.GetLastAccountId();

        if (stockAccountsLastId is not null)
            ids.Add(stockAccountsLastId.Value);

        if (bankAccountsLastId is not null)
            ids.Add(bankAccountsLastId.Value);

        return ids.Count != 0 ? ids.Max() : null;
    }
}