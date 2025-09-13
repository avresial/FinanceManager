using FinanceManager.Domain.Entities.Accounts;
using FinanceManager.Domain.Entities.Accounts.Entries;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Providers;
using FinanceManager.Domain.Repositories;
using FinanceManager.Domain.Repositories.Account;

namespace FinanceManager.Application.Services;

public class GuestAccountSeeder(IFinancialAccountRepository accountRepository, AccountIdProvider accountIdProvider, IFinancialLabelsRepository financialLabelsRepository)
{
    private readonly int _guestUserId = 1;

    public async Task SeedNewData(DateTime start, DateTime end)
    {
        var availableAccounts = await accountRepository.GetAvailableAccounts(_guestUserId);

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
        await accountRepository.AddAccount(stockAccount);
    }

    private async Task AddBankAccount(DateTime start, DateTime end)
    {
        var labels = new List<FinancialLabel>();
        await foreach (var label in financialLabelsRepository.GetLabels())
        {
            labels.Add(label);
        }
        BankAccount bankAccount = await GetNewBankAccount("Cash 1", AccountLabel.Cash);
        for (DateTime date = start; date <= end; date = date.AddDays(1))
            bankAccount.AddEntry(GetNewBankAccountEntry(date, -90, 100, labels));

        await accountRepository.AddAccount(bankAccount);
    }

    private async Task AddLoanAccount(DateTime start, DateTime end)
    {
        var labels = new List<FinancialLabel>();
        await foreach (var label in financialLabelsRepository.GetLabels())
            labels.Add(label);

        BankAccount loanAccount = await GetNewBankAccount("Loan 1", AccountLabel.Loan);
        var days = (int)((end - start).TotalDays);
        loanAccount.AddEntry(GetNewBankAccountEntry(start, days * -100 - 1000, days * -100, labels));
        for (DateTime date = start.AddDays(1); date <= end; date = date.AddDays(1))
            loanAccount.AddEntry(GetNewBankAccountEntry(date, 10, 100, labels));
        await accountRepository.AddAccount(loanAccount);
    }
    public async Task<StockAccount> GetNewStockAccount(string accountName, AccountLabel accountType)
    {
        var accountId = (await accountIdProvider.GetMaxId()) + 1;
        StockAccount stockAccount = new(_guestUserId, accountId is null ? 0 : accountId.Value, accountName);
        return stockAccount;
    }
    public StockAccountEntry GetNewStockAccountEntry(int accountId, int entryId, DateTime date, int minValue, int maxValue, string ticker, InvestmentType investmentType = InvestmentType.Stock)
    {
        return new StockAccountEntry(accountId, entryId, date, 0, Random.Shared.Next(minValue, maxValue), ticker, investmentType);
    }
    public async Task<BankAccount> GetNewBankAccount(string accountName, AccountLabel accountType)
    {
        var accountId = (await accountIdProvider.GetMaxId()) + 1;
        BankAccount bankAccount = new BankAccount(_guestUserId, accountId is null ? 0 : accountId.Value, accountName, accountType);
        return bankAccount;
    }
    public AddBankEntryDto GetNewBankAccountEntry(DateTime date, int minValue, int maxValue, List<FinancialLabel> labels,
        string description = "")
    {
        return new AddBankEntryDto(date, Random.Shared.Next(minValue, maxValue), description, labels);
    }


}
