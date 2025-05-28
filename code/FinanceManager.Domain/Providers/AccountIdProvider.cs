using FinanceManager.Domain.Entities.Accounts;
using FinanceManager.Domain.Repositories.Account;

namespace FinanceManager.Domain.Providers;

public class AccountIdProvider
{
    private readonly IAccountRepository<StockAccount> _stockAccountRepository;
    private readonly IBankAccountRepository<BankAccount> _bankAccountRepository;

    public AccountIdProvider(IAccountRepository<StockAccount> stockAccountRepository, IBankAccountRepository<BankAccount> bankAccountRepository)
    {
        this._stockAccountRepository = stockAccountRepository;
        this._bankAccountRepository = bankAccountRepository;
    }

    public async Task<int?> GetMaxId()
    {
        List<int> ids = [];
        int? stockAccountsLastId = null;
        int? bankAccountsLastId = null;

        stockAccountsLastId = await _stockAccountRepository.GetLastAccountId();
        bankAccountsLastId = await _bankAccountRepository.GetLastAccountId();

        if (stockAccountsLastId is not null)
            ids.Add(stockAccountsLastId.Value);

        if (bankAccountsLastId is not null)
            ids.Add(bankAccountsLastId.Value);

        return ids.Count != 0 ? ids.Max() : null;
    }
}
