using FinanceManager.Domain.Entities.Accounts;
using FinanceManager.Domain.Entities.Accounts.Entries;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Providers;
using FinanceManager.Domain.Repositories;
using FinanceManager.Domain.Repositories.Account;

namespace FinanceManager.Application.Services;

public class GuestAccountSeeder(IFinancalAccountRepository accountRepository, AccountIdProvider accountIdProvider, IFinancialLabelsRepository financialLabelsRepository)
{
    private readonly int _guestUserId = 1;
    private readonly Random _random = new Random();
    private readonly IFinancalAccountRepository _accountRepository = accountRepository;
    private readonly AccountIdProvider _accountIdProvider = accountIdProvider;

    public async Task SeedNewData(DateTime start, DateTime end)
    {
        var availableAccounts = await _accountRepository.GetAvailableAccounts(_guestUserId);

        if (availableAccounts.Count != 0) return;

        await AddBankAccount(start, end);
        await AddLoanAccount(start, end);

        try
        {
            await AddStockAccount(start, end);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error adding stock account: {ex.Message}");
        }
    }

    private async Task AddStockAccount(DateTime start, DateTime end)
    {
        StockAccount stockAccount = await GetNewStockAccount("Stock 1", AccountLabel.Stock);
        int entryId = 0;

        for (DateTime date = start; date <= end; date = date.AddDays(1))
            stockAccount.Add(GetNewStockAccountEntry(_guestUserId, entryId++, date, -90, 100, "RandomTicker"));
        await _accountRepository.AddAccount(stockAccount);
    }

    private async Task AddBankAccount(DateTime start, DateTime end)
    {
        var labels = await financialLabelsRepository.GetLabels();
        BankAccount bankAccount = await GetNewBankAccount("Cash 1", AccountLabel.Cash);
        for (DateTime date = start; date <= end; date = date.AddDays(1))
            bankAccount.AddEntry(GetNewBankAccountEntry(date, -90, 100, labels));

        await _accountRepository.AddAccount(bankAccount);
    }

    private async Task AddLoanAccount(DateTime start, DateTime end)
    {
        var labels = await financialLabelsRepository.GetLabels();
        BankAccount loanAccount = await GetNewBankAccount("Loan 1", AccountLabel.Loan);
        var days = (int)((end - start).TotalDays);
        loanAccount.AddEntry(GetNewBankAccountEntry(start, days * -100 - 1000, days * -100, labels));
        for (DateTime date = start.AddDays(1); date <= end; date = date.AddDays(1))
            loanAccount.AddEntry(GetNewBankAccountEntry(date, 10, 100, labels, ExpenseType.DebtRepayment));
        await _accountRepository.AddAccount(loanAccount);
    }
    public async Task<StockAccount> GetNewStockAccount(string accountName, AccountLabel accountType)
    {
        var accountId = (await _accountIdProvider.GetMaxId()) + 1;
        StockAccount stockAccount = new(_guestUserId, accountId is null ? 0 : accountId.Value, accountName);
        return stockAccount;
    }
    public StockAccountEntry GetNewStockAccountEntry(int accountId, int entryId, DateTime date, int minValue, int maxValue, string ticker, InvestmentType investmentType = InvestmentType.Stock)
    {
        return new StockAccountEntry(accountId, entryId, date, 0, _random.Next(minValue, maxValue), ticker, investmentType);
    }
    public async Task<BankAccount> GetNewBankAccount(string accountName, AccountLabel accountType)
    {
        var accountId = (await _accountIdProvider.GetMaxId()) + 1;
        BankAccount bankAccount = new BankAccount(_guestUserId, accountId is null ? 0 : accountId.Value, accountName, accountType);
        return bankAccount;
    }
    public AddBankEntryDto GetNewBankAccountEntry(DateTime date, int minValue, int maxValue, List<FinancialLabel> labels,
        ExpenseType expenseType = ExpenseType.Other, string description = "")
    {
        return new AddBankEntryDto(date, _random.Next(minValue, maxValue), expenseType, description, labels);
    }


}
