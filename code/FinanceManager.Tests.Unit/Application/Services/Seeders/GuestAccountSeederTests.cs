using FinanceManager.Application.Identity.Seeders;
using FinanceManager.Application.Insights.Seeders;
using FinanceManager.Application.Labels.Seeders;
using FinanceManager.Application.Transactions.Bonds.Seeders;
using FinanceManager.Application.Transactions.Cash.Seeders;
using FinanceManager.Application.Transactions.Investments.Seeders;
using FinanceManager.Domain.Assets.Repositories;
using FinanceManager.Domain.Currencies.Entities;
using FinanceManager.Domain.Currencies.Repositories;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Identity.Repositories;
using FinanceManager.Domain.Insights.Repositories;
using FinanceManager.Domain.Labels.Entities;
using FinanceManager.Domain.Labels.Repositories;
using FinanceManager.Domain.Shared.Services;
using FinanceManager.Domain.Transactions.Bonds.Entities;
using FinanceManager.Domain.Transactions.Bonds.Repositories;
using FinanceManager.Domain.Transactions.Cash.Entities;
using FinanceManager.Domain.Transactions.Cash.Repositories;
using FinanceManager.Domain.Transactions.Investments.Entities;
using FinanceManager.Domain.Transactions.Investments.Repositories;
using FinanceManager.Domain.Transactions.Shared.Repositories;
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

    [Fact]
    public async Task SeedForGuest_SeedsASmallUsdCardDebt_SoTheLiabilitiesPageIsNotEmpty()
    {
        var saved = new List<CashEntry>();

        await SeedCashEntries(new DateTime(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc), saved);

        var usd = saved.Where(e => e.CurrencyId == DefaultCurrency.USD.Id).ToList();
        Assert.NotEmpty(usd);
        Assert.All(usd, e => Assert.True(e.ValueChange < 0m));
        Assert.True(usd.Sum(e => e.ValueChange) is < 0m and > -1_000m);
        Assert.True(saved.Where(e => e.CurrencyId == DefaultCurrency.PLN.Id).Sum(e => e.ValueChange) > 0m);
    }

    private static async Task<List<(DateTime PostingDate, decimal ValueChange, string Description)>> SeedCashEntries(DateTime utcNow, List<CashEntry>? saved = null)
    {
        saved ??= [];
        var cashReader = new Mock<ICashTransactionReader>();
        cashReader.Setup(r => r.HasAnyEntries(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var cashEntries = new Mock<ITransactionEntryRepository<CashEntry>>();
        cashEntries.Setup(r => r.Add(It.IsAny<IEnumerable<CashEntry>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<CashEntry>, CancellationToken>((entries, _) => saved.AddRange(entries))
            .ReturnsAsync(true);
        var labelsRepository = new Mock<IFinancialLabelsRepository>();
        labelsRepository.Setup(r => r.GetLabels(It.IsAny<CancellationToken>())).Returns(AsyncEnumerable.Empty<FinancialLabel>());

        var bondReader = new Mock<IBondTransactionReader>();
        bondReader.Setup(r => r.HasAnyEntries(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
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
            new CashEntrySeeder(cashReader.Object, cashEntries.Object, labelsRepository.Object, NullLogger<CashEntrySeeder>.Instance),
            new InvestmentTransactionSeeder(Mock.Of<IAssetRepository>(), Mock.Of<IAssetListingRepository>(),
                Mock.Of<IMarketDataSymbolRepository>(), Mock.Of<IPriceQuoteRepository>(), investmentTransactions.Object, NullLogger<InvestmentTransactionSeeder>.Instance),
            new BondEntrySeeder(bondReader.Object, Mock.Of<ITransactionEntryRepository<BondEntry>>(), bondDetailsRepository.Object, NullLogger<BondEntrySeeder>.Instance),
            new FinancialInsightsSeeder(insightsRepository.Object, userRepository.Object, new ConfigurationBuilder().Build(), NullLogger<FinancialInsightsSeeder>.Instance),
            dateTimeProvider.Object,
            NullLogger<GuestAccountSeeder>.Instance);

        await seeder.SeedForGuest(1, TestContext.Current.CancellationToken);

        return saved.Select(e => (e.PostingDate, e.ValueChange, e.Description)).ToList();
    }
}