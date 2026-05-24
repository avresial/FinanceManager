using FinanceManager.Domain.Entities.Bonds;
using FinanceManager.Domain.Entities.FinancialAccounts.Currencies;
using FinanceManager.Domain.Entities.Stocks;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Repositories;
using FinanceManager.Domain.Repositories.Account;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Application.Services.Seeders;

public class GuestAccountSeeder(IFinancialAccountRepository accountRepository, IFinancialLabelsRepository financialLabelsRepository,
    IAccountRepository<StockAccount> stockAccountRepository, ICurrencyAccountRepository<CurrencyAccount> currencyAccountRepository,
    IAccountRepository<BondAccount> bondAccountRepository,
    IUserRepository userRepository, FinancialLabelSeeder financialLabelSeeder,
    ILogger<GuestAccountSeeder> logger)
{
    public async Task SeedForGuest(int guestUserId, CancellationToken cancellationToken = default)
    {
        var start = DateTime.UtcNow.AddMonths(-6);
        var end = DateTime.UtcNow;

        await EnsureGuestUserRecord(guestUserId);
        // The per-session in-memory DB starts empty, so the label seeder needs to run before account seeding
        // populates entries with randomly-sampled labels.
        await financialLabelSeeder.Seed(cancellationToken);
        await SeedNewData(guestUserId, start, end);
    }

    // Pricing-tier checks (UserPlanVerifier etc.) look up the user by id. The guest's per-session in-memory DB
    // starts empty, so we materialise a Basic-tier shell user that lives only for the lifetime of the sandbox.
    private async Task EnsureGuestUserRecord(int guestUserId)
    {
        if (await userRepository.GetUser(guestUserId) is not null) return;
        await userRepository.AddUserWithId(guestUserId, login: $"guest-{guestUserId}", password: string.Empty, PricingLevel.Basic, UserRole.User);
    }

    private async Task SeedNewData(int userId, DateTime start, DateTime end)
    {
        logger.LogTrace("Seeding guest demo data for user {UserId}.", userId);
        if (!await currencyAccountRepository.GetAvailableAccounts(userId).AnyAsync())
        {
            var labels = await CurrencyAccountSeeder.GetRandomLabels(financialLabelsRepository).ToListAsync();
            logger.LogTrace("Seeding currency accounts.");
            await accountRepository.AddAccount(userId, AccountLabel.Cash, labels, start, end);

            logger.LogTrace("Seeding loan accounts.");
            await accountRepository.AddAccount(userId, AccountLabel.Loan, labels, start, end);
        }

        logger.LogTrace("Seeding stock accounts.");
        if (!await stockAccountRepository.GetAvailableAccounts(userId).AnyAsync())
            await accountRepository.AddStockAccount(userId, start, end);

        logger.LogTrace("Seeding bond accounts.");
        if (!await bondAccountRepository.GetAvailableAccounts(userId).AnyAsync())
            await accountRepository.AddBondAccount(userId, start, end);

        logger.LogTrace("Seeding finished.");
    }
}
