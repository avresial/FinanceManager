using FinanceManager.Application.FinancialAccounts.Bond.Seeders;
using FinanceManager.Application.FinancialAccounts.Currencies.Seeders;
using FinanceManager.Application.FinancialAccounts.Investments.Seeders;
using FinanceManager.Application.Identity.Users;
using FinanceManager.Application.Insights.Seeders;
using FinanceManager.Application.Labels.Seeders;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Identity.Repositories;
using FinanceManager.Domain.Shared.Services;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Application.Identity.Seeders;

public class GuestAccountSeeder(
    IUserRepository userRepository,
    FinancialLabelSeeder financialLabelSeeder,
    BondDetailsSeeder bondDetailsSeeder,
    CurrencyAccountSeeder currencyAccountSeeder,
    InvestmentAccountSeeder investmentAccountSeeder,
    BondAccountSeeder bondAccountSeeder,
    FinancialInsightsSeeder financialInsightsSeeder,
    IDateTimeProvider dateTimeProvider,
    ILogger<GuestAccountSeeder> logger)
{
    public async Task SeedForGuest(int guestUserId, CancellationToken cancellationToken = default)
    {
        // Capture the date once: all data derives from it, so the result only depends on the UTC calendar date.
        var demoSeed = new GuestDemoSeed(DateOnly.FromDateTime(dateTimeProvider.UtcNow));
        var end = demoSeed.Date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var start = end.AddMonths(-6);

        await EnsureGuestUserRecord(guestUserId);
        // The per-session in-memory DB starts empty, so the label and bond-details seeders need to run before
        // account seeding populates entries with randomly-sampled labels and references to bond details.
        await financialLabelSeeder.Seed(cancellationToken);
        await bondDetailsSeeder.Seed(cancellationToken);

        logger.LogTrace("Seeding guest demo data for user {UserId}.", guestUserId);
        await currencyAccountSeeder.Seed(guestUserId, start, end, demoSeed.CreateRandom(GuestDemoSeed.CashSalt), cancellationToken);
        await investmentAccountSeeder.Seed(guestUserId, start, end, demoSeed.CreateRandom(GuestDemoSeed.InvestmentPricesSalt), cancellationToken);
        await bondAccountSeeder.Seed(guestUserId, start, end, cancellationToken);
        await financialInsightsSeeder.SeedForGuest(guestUserId, end, cancellationToken);
        logger.LogTrace("Seeding finished.");
    }

    // Pricing-tier checks (UserPlanVerifier etc.) look up the user by id. The guest's per-session in-memory DB
    // starts empty, so we materialise a Basic-tier shell user that lives only for the lifetime of the sandbox.
    private async Task EnsureGuestUserRecord(int guestUserId)
    {
        if (await userRepository.GetUser(guestUserId) is not null) return;
        await userRepository.AddUserWithId(guestUserId, login: $"guest-{guestUserId}", password: string.Empty, PricingLevel.Basic, UserRole.User, firstName: "Demo");
    }
}