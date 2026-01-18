using FinanceManager.Application.Providers;
using FinanceManager.Domain.Entities.Bonds;
using FinanceManager.Domain.Entities.FinancialAccounts.Currencies;
using FinanceManager.Domain.Entities.Stocks;
using FinanceManager.Domain.Entities.Users;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Repositories;
using FinanceManager.Domain.Repositories.Account;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Application.Services.Seeders;

public class GuestAccountSeeder(IFinancialAccountRepository accountRepository, IFinancialLabelsRepository financialLabelsRepository,
    IAccountRepository<StockAccount> stockAccountRepository, ICurrencyAccountRepository<CurrencyAccount> currencyAccountRepository,
    IAccountRepository<BondAccount> bondAccountRepository,
    IUserRepository userRepository, IConfiguration configuration,
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

        await userRepository.AddUser(configuration["DefaultUser:Login"]!, PasswordEncryptionProvider.EncryptPassword(configuration["DefaultUser:Password"]!), PricingLevel.Basic, UserRole.User);
        var guestUser = await userRepository.GetUser(configuration["DefaultUser:Login"]!) ?? throw new Exception("Failed to create guest user");
        logger.LogTrace("New guest user was created with id {Id}", guestUser.UserId);

        return guestUser;
    }


    public async Task SeedNewData(User user, DateTime start, DateTime end)
    {
        logger.LogTrace("Seeding data.");
        if (!await currencyAccountRepository.GetAvailableAccounts(user.UserId).AnyAsync())
        {
            var labels = await CurrencyAccountSeeder.GetRandomLabels(financialLabelsRepository).ToListAsync();
            logger.LogTrace("Seeding currency accounts.");
            await accountRepository.AddAccount(user.UserId, AccountLabel.Cash, labels, start, end);

            logger.LogTrace("Seeding loan accounts.");
            await accountRepository.AddAccount(user.UserId, AccountLabel.Loan, labels, start, end);
        }

        logger.LogTrace("Seeding stock accounts.");
        if (!await stockAccountRepository.GetAvailableAccounts(user.UserId).AnyAsync())
            await accountRepository.AddStockAccount(user.UserId, start, end);

        logger.LogTrace("Seeding bond accounts.");
        if (!await bondAccountRepository.GetAvailableAccounts(user.UserId).AnyAsync())
            await accountRepository.AddBondAccount(user.UserId, start, end);

        logger.LogTrace("Seeding finished.");
    }
}