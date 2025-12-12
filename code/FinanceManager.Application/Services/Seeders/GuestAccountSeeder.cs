using FinanceManager.Domain.Entities.Cash;
using FinanceManager.Domain.Entities.Shared.Accounts;
using FinanceManager.Domain.Entities.Stocks;
using FinanceManager.Domain.Entities.Users;
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
    public async Task Seed(CancellationToken cancellationToken = default)
    {
        var start = DateTime.UtcNow.AddMonths(-6);
        var end = DateTime.UtcNow;

        var guestUser = await userRepository.GetUser(configuration["DefaultUser:Login"]!);
        guestUser ??= await AddGuestUser();

        await SeedNewData(guestUser, start, end);
    }

    public async Task<User> AddGuestUser()
    {
        if (configuration is null) throw new Exception("Configuration is null, user can not be created.");
        logger.LogTrace("Creating new guest user.");

        await userRepository.AddUser(configuration["DefaultUser:Login"]!, configuration["DefaultUser:Password"]!, PricingLevel.Basic, UserRole.User);
        var guestUser = await userRepository.GetUser(configuration["DefaultUser:Login"]!) ?? throw new Exception("Failed to create guest user");
        logger.LogTrace("New guest user was created with id {Id}", guestUser.UserId);

        return guestUser;
    }


    public async Task SeedNewData(User user, DateTime start, DateTime end)
    {
        logger.LogTrace("Seeding data.");
        if (!await bankAccountRepository.GetAvailableAccounts(user.UserId).AnyAsync())
        {
            logger.LogTrace("Seeding bank accounts.");
            await AddBankAccount(user.UserId, start, end);
            logger.LogTrace("Seeding loan accounts.");
            await AddLoanAccount(user.UserId, start, end);
        }

        logger.LogTrace("Seeding stock accounts.");
        if (!await stockAccountRepository.GetAvailableAccounts(user.UserId).AnyAsync())
            await AddStockAccount(user.UserId, start, end);

        logger.LogTrace("Seeding finished.");
    }
    private async Task AddBankAccount(int userId, DateTime start, DateTime end)
    {
        var labels = await GetRandomLabels().ToListAsync();

        var newAccount = await GetNewBankAccount(userId, "Cash 1", AccountLabel.Cash);
        for (var date = start; date <= end; date = date.AddDays(1))
            newAccount.AddEntry(GetNewBankAccountEntry(date, -90, 100, labels), false);
        newAccount.RecalculateEntryValues(newAccount.Entries.Count - 1);
        await accountRepository.AddAccount(newAccount);
    }

    private async Task AddLoanAccount(int userId, DateTime start, DateTime end)
    {
        var labels = await GetRandomLabels().ToListAsync();

        var newAccount = await GetNewBankAccount(userId, "Loan 1", AccountLabel.Loan);
        var days = (int)((end - start).TotalDays);
        newAccount.AddEntry(GetNewBankAccountEntry(start, days * -100 - 1000, days * -100, labels), false);
        for (DateTime date = start.AddDays(1); date <= end; date = date.AddDays(1))
            newAccount.AddEntry(GetNewBankAccountEntry(date, 10, 100, labels), false);
        newAccount.RecalculateEntryValues(newAccount.Entries.Count - 1);
        await accountRepository.AddAccount(newAccount);
    }

    private async Task AddStockAccount(int userId, DateTime start, DateTime end)
    {
        var newAccount = await GetNewStockAccount(userId, "Stock 1", AccountLabel.Stock);

        for (var date = start; date <= end; date = date.AddDays(1))
            newAccount.Add(GetNewStockAccountEntry(userId, 0, date, -90, 100, "RandomTicker"), false);
        newAccount.RecalculateEntryValues(newAccount.Entries.Count - 1);
        await accountRepository.AddAccount(newAccount);
    }

    public async Task<StockAccount> GetNewStockAccount(int userId, string accountName, AccountLabel accountType)
    {
        var accountId = (await GetMaxId()) + 1;
        return new(userId, accountId is null ? 0 : accountId.Value, accountName);
    }
    public static StockAccountEntry GetNewStockAccountEntry(int accountId, int entryId, DateTime date, int minValue, int maxValue,
        string ticker, InvestmentType investmentType = InvestmentType.Stock) =>
         new(accountId, entryId, date, 0, Random.Shared.Next(minValue, maxValue), ticker, investmentType);

    public async Task<BankAccount> GetNewBankAccount(int userId, string accountName, AccountLabel accountType)
    {
        var accountId = (await GetMaxId()) + 1;
        return new(userId, accountId is null ? 0 : accountId.Value, accountName, accountType);
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