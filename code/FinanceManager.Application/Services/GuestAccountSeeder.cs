using FinanceManager.Domain.Entities.Accounts;
using FinanceManager.Domain.Entities.Accounts.Entries;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Providers;
using FinanceManager.Domain.Repositories.Account;

namespace FinanceManager.Application.Services;

public class GuestAccountSeeder(IFinancalAccountRepository accountRepository, AccountIdProvider accountIdProvider)
{
    private readonly int _guestUserId = 0;
    private Random _random = new Random();
    private readonly IFinancalAccountRepository _accountRepository = accountRepository;
    private readonly AccountIdProvider _accountIdProvider = accountIdProvider;

    public void SeedNewData(DateTime start, DateTime end)
    {
        var availableAccounts = _accountRepository.GetAvailableAccounts(_guestUserId);

        if (availableAccounts.Count != 0) return;

        AddBankAccount(start, end);
        AddLoanAccount(start, end);

        try
        {
            AddStockAccount(start, end);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error adding stock account: {ex.Message}");
        }
    }

    private void AddStockAccount(DateTime start, DateTime end)
    {
        StockAccount stockAccount = GetNewStockAccount("Cash 1", AccountType.Cash);
        int entryId = 0;
        for (DateTime date = start; date <= end; date = date.AddDays(1))
            stockAccount.Add(GetNewStockAccountEntry(_guestUserId, entryId++, date, -90, 100, "RandomTicker"));
        _accountRepository.AddAccount(stockAccount);
    }

    private void AddBankAccount(DateTime start, DateTime end)
    {
        BankAccount bankAccount = GetNewBankAccount("Cash 1", AccountType.Cash);
        for (DateTime date = start; date <= end; date = date.AddDays(1))
            bankAccount.AddEntry(GetNewBankAccountEntry(date, -90, 100));
        _accountRepository.AddAccount(bankAccount);
    }

    private void AddLoanAccount(DateTime start, DateTime end)
    {
        BankAccount loanAccount = GetNewBankAccount("Loan 1", AccountType.Loan);
        var days = (int)((end - start).TotalDays);
        loanAccount.AddEntry(GetNewBankAccountEntry(start, days * -100 - 1000, days * -100));
        for (DateTime date = start.AddDays(1); date <= end; date = date.AddDays(1))
            loanAccount.AddEntry(GetNewBankAccountEntry(date, 10, 100, ExpenseType.DebtRepayment));
        _accountRepository.AddAccount(loanAccount);
    }
    public StockAccount GetNewStockAccount(string accountName, AccountType accountType)
    {
        var accountId = _accountIdProvider.GetMaxId() + 1;
        StockAccount bankAccount = new(_guestUserId, accountId is null ? 0 : accountId.Value, accountName);
        return bankAccount;
    }
    public StockAccountEntry GetNewStockAccountEntry(int accountId, int entryId, DateTime date, int minValue, int maxValue, string ticker, InvestmentType investmentType = InvestmentType.Stock)
    {
        return new StockAccountEntry(accountId, entryId, date, 0, _random.Next(minValue, maxValue), ticker, investmentType);
    }
    public BankAccount GetNewBankAccount(string accountName, AccountType accountType)
    {
        var accountId = _accountIdProvider.GetMaxId() + 1;
        BankAccount bankAccount = new BankAccount(_guestUserId, accountId is null ? 0 : accountId.Value, accountName, accountType);
        return bankAccount;
    }
    public AddBankEntryDto GetNewBankAccountEntry(DateTime date, int minValue, int maxValue, ExpenseType expenseType = ExpenseType.Other, string description = "")
    {
        return new AddBankEntryDto(date, _random.Next(minValue, maxValue), expenseType, description);
    }


}
