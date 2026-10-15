using FinanceManager.Application.FinancialAccounts.Bond.Seeders;
using FinanceManager.Application.FinancialAccounts.Currencies.Seeders;
using FinanceManager.Application.FinancialAccounts.Investments.Seeders;
using FinanceManager.Application.Identity.Seeders;
using FinanceManager.Application.Insights.Seeders;
using FinanceManager.Application.Labels.Seeders;
using FinanceManager.Domain.Assets.Repositories;
using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Bond.Repositories;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Repositories;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Repositories;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
using FinanceManager.Domain.FinancialAccounts.Shared.ValueObjects;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Identity.Repositories;
using FinanceManager.Domain.Insights.Repositories;
using FinanceManager.Domain.Labels.Repositories;
using FinanceManager.Domain.Shared.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FinanceManager.Tests.Unit.Application.Services.Seeders;

[Collection("Application")]
[Trait("Category", "Unit")]
public class GuestAccountSeederTests
{
    [Fact]
    public async Task SeedForGuest_AtDifferentTimesOfTheSameUtcDay_SeedsIdenticalCashData()
    {
        var morning = await SeedCashEntries(new DateTime(2026, 10, 14, 0, 0, 1, DateTimeKind.Utc));
        var evening = await SeedCashEntries(new DateTime(2026, 10, 14, 23, 59, 59, DateTimeKind.Utc));

        Assert.Equal(morning, evening);
        // Window is [midnight - 6 months, midnight], with today included.
        Assert.Equal(new DateTime(2026, 4, 14, 0, 0, 0, DateTimeKind.Utc), morning.Min(e => e.PostingDate));
        Assert.Equal(new DateTime(2026, 10, 14, 0, 0, 0, DateTimeKind.Utc), morning.Max(e => e.PostingDate));
    }

    [Fact]
    public async Task SeedForGuest_OnDifferentUtcDays_SeedsDifferentCashData()
    {
        var first = await SeedCashEntries(new DateTime(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc));
        var second = await SeedCashEntries(new DateTime(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc));

        Assert.NotEqual(first, second);
    }

    private static async Task<List<(DateTime PostingDate, decimal ValueChange, string Description)>> SeedCashEntries(DateTime utcNow)
    {
        var saved = new List<CurrencyAccount>();
        var accountRepository = new Mock<IFinancialAccountRepository>();
        accountRepository.Setup(r => r.AddAccount(It.IsAny<CurrencyAccount>()))
            .Callback<CurrencyAccount>(saved.Add)
            .ReturnsAsync(0);
        var currencyRepository = new Mock<ICurrencyAccountRepository<CurrencyAccount>>();
        currencyRepository.Setup(r => r.GetAvailableAccounts(It.IsAny<int>())).Returns(AsyncEnumerable.Empty<AvailableAccount>());
        var labelsRepository = new Mock<IFinancialLabelsRepository>();
        labelsRepository.Setup(r => r.GetLabels(It.IsAny<CancellationToken>())).Returns(AsyncEnumerable.Empty<FinancialLabel>());

        var bondRepository = new Mock<IAccountRepository<BondAccount>>();
        bondRepository.Setup(r => r.GetAvailableAccounts(It.IsAny<int>())).Returns(AsyncEnumerable.Empty<AvailableAccount>());
        var bondDetailsRepository = new Mock<IBondDetailsRepository>();
        bondDetailsRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).Returns(AsyncEnumerable.Empty<BondDetails>());

        // An existing investment transaction makes the investment seeder skip, keeping this test about the window and cash data.
        var investmentTransactions = new Mock<IInvestmentTransactionRepository>();
        investmentTransactions
            .Setup(r => r.GetByUser(It.IsAny<long>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new InvestmentTransaction()]);

        var userRepository = new Mock<IUserRepository>();
        userRepository.Setup(r => r.GetUser(It.IsAny<int>())).ReturnsAsync(new User { Login = "guest-1", CreationDate = DateTime.UtcNow });

        var insightsRepository = new Mock<IFinancialInsightsRepository>();
        insightsRepository.Setup(r => r.GetCountByUser(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var dateTimeProvider = new Mock<IDateTimeProvider>();
        dateTimeProvider.SetupGet(p => p.UtcNow).Returns(utcNow);

        var seeder = new GuestAccountSeeder(
            userRepository.Object,
            new FinancialLabelSeeder(Mock.Of<IFinancialLabelsRepository>(), NullLogger<FinancialLabelSeeder>.Instance),
            new BondDetailsSeeder(bondDetailsRepository.Object, Mock.Of<ICurrencyRepository>(), NullLogger<BondDetailsSeeder>.Instance),
            new CurrencyAccountSeeder(accountRepository.Object, currencyRepository.Object, labelsRepository.Object, NullLogger<CurrencyAccountSeeder>.Instance),
            new InvestmentAccountSeeder(Mock.Of<IAccountRepository<InvestmentAccount>>(), Mock.Of<IAssetRepository>(), Mock.Of<IAssetListingRepository>(),
                Mock.Of<IMarketDataSymbolRepository>(), Mock.Of<IPriceQuoteRepository>(), investmentTransactions.Object, NullLogger<InvestmentAccountSeeder>.Instance),
            new BondAccountSeeder(accountRepository.Object, bondRepository.Object, bondDetailsRepository.Object, NullLogger<BondAccountSeeder>.Instance),
            new FinancialInsightsSeeder(insightsRepository.Object, userRepository.Object, new ConfigurationBuilder().Build(), NullLogger<FinancialInsightsSeeder>.Instance),
            dateTimeProvider.Object,
            NullLogger<GuestAccountSeeder>.Instance);

        await seeder.SeedForGuest(1, TestContext.Current.CancellationToken);

        return saved.Single(a => a.AccountType == AccountLabel.Cash).Entries
            .Select(e => (e.PostingDate, e.ValueChange, e.Description))
            .ToList();
    }
}