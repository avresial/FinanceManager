using FinanceManager.Domain.Entities.Cash;
using FinanceManager.Domain.Entities.Shared.Accounts;
using FinanceManager.Domain.Entities.Stocks;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Repositories;
using FinanceManager.Domain.Repositories.Account;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Application.Services.Seeders;

public class GuestAccountSeeder(IFinancialAccountRepository accountRepository, IFinancialLabelsRepository financialLabelsRepository,
    IAccountRepository<StockAccount> stockAccountRepository, IBankAccountRepository<BankAccount> bankAccountRepository, IUserRepository userRepository, IConfiguration configuration,
    ILogger<GuestAccountSeeder> logger) : ISeeder
{
    private int _guestUserId = 1;

    public async Task Seed(CancellationToken cancellationToken = default)
    {
        var start = DateTime.Now.AddMonths(-6);
        var end = DateTime.Now;
        await AddGuestUser();
        await SeedNewData(start, end);
    }

    public async Task AddGuestUser()
    {
        if (configuration is null) return;

        await userRepository.AddUser(configuration["DefaultUser:Login"]!, configuration["DefaultUser:Password"]!, PricingLevel.Basic, UserRole.User);
    }


    public async Task SeedNewData(DateTime start, DateTime end)
    {
        var guestUser = await userRepository.GetUser(configuration["DefaultUser:Login"]!);

        if (guestUser is null)
        {
            await AddGuestUser();
            guestUser = await userRepository.GetUser(configuration["DefaultUser:Login"]!);

            if (guestUser is null) throw new Exception("Failed to create guest user");

            logger.LogInformation("New guest user was created with id {Id}", guestUser.UserId);
        }

        if (guestUser is null) throw new Exception("Guest account does not exist");

        _guestUserId = guestUser.UserId;

        try
        {
            await AddBankAccount(start, end);
            await AddLoanAccount(start, end);
            await AddStockAccount(start, end);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, ex.Message);
        }
    }

    private async Task AddStockAccount(DateTime start, DateTime end)
    {
        var stockAccount = await GetNewStockAccount("Stock 1", AccountLabel.Stock);

        for (var date = start; date <= end; date = date.AddDays(1))
            stockAccount.Add(GetNewStockAccountEntry(_guestUserId, 0, date, -90, 100, "RandomTicker"), false);
        stockAccount.RecalculateEntryValues(0);
        await accountRepository.AddAccount(stockAccount);
    }

    private async Task AddBankAccount(DateTime start, DateTime end)
    {
        var labels = await GetRandomLabels().ToListAsync();

        var bankAccount = await GetNewBankAccount("Cash 1", AccountLabel.Cash);
        for (var date = start; date <= end; date = date.AddDays(1))
            bankAccount.AddEntry(GetNewBankAccountEntry(date, -90, 100, labels), false);
        bankAccount.RecalculateEntryValues(0);
        await accountRepository.AddAccount(bankAccount);
    }

    private async Task AddLoanAccount(DateTime start, DateTime end)
    {
        var labels = await GetRandomLabels().ToListAsync();

        var loanAccount = await GetNewBankAccount("Loan 1", AccountLabel.Loan);
        var days = (int)((end - start).TotalDays);
        loanAccount.AddEntry(GetNewBankAccountEntry(start, days * -100 - 1000, days * -100, labels), false);
        for (DateTime date = start.AddDays(1); date <= end; date = date.AddDays(1))
            loanAccount.AddEntry(GetNewBankAccountEntry(date, 10, 100, labels), false);
        loanAccount.RecalculateEntryValues(0);
        await accountRepository.AddAccount(loanAccount);
    }
    public async Task<StockAccount> GetNewStockAccount(string accountName, AccountLabel accountType)
    {
        var accountId = (await GetMaxId()) + 1;
        return new(_guestUserId, accountId is null ? 0 : accountId.Value, accountName);
    }
    public static StockAccountEntry GetNewStockAccountEntry(int accountId, int entryId, DateTime date, int minValue, int maxValue,
        string ticker, InvestmentType investmentType = InvestmentType.Stock) =>
         new(accountId, entryId, date, 0, Random.Shared.Next(minValue, maxValue), ticker, investmentType);

    public async Task<BankAccount> GetNewBankAccount(string accountName, AccountLabel accountType)
    {
        var accountId = (await GetMaxId()) + 1;
        return new(_guestUserId, accountId is null ? 0 : accountId.Value, accountName, accountType);
    }
    public AddBankEntryDto GetNewBankAccountEntry(DateTime date, int minValue, int maxValue, List<FinancialLabel> labels,
        string description = "") =>
     new(date, Random.Shared.Next(minValue, maxValue), description, labels);

    private async IAsyncEnumerable<FinancialLabel> GetRandomLabels()
    {
        await foreach (var label in financialLabelsRepository.GetLabels())
            if (Random.Shared.Next(0, 100) < 40)
                yield return label;
    }
    public async Task<int?> GetMaxId() // helper method
    {
        var stockAccountsLastId = await stockAccountRepository.GetLastAccountId();
        var bankAccountsLastId = await bankAccountRepository.GetLastAccountId();

        List<int> ids = [];
        if (stockAccountsLastId is not null)
            ids.Add(stockAccountsLastId.Value);

        if (bankAccountsLastId is not null)
            ids.Add(bankAccountsLastId.Value);

        return ids.Count != 0 ? ids.Max() : null;
    }
}