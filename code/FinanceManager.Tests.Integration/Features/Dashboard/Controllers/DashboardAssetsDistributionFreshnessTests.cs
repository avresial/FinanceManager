using FinanceManager.Components.Features.Dashboard.HttpClients;
using FinanceManager.Components.Features.FinancialAccounts.HttpClients;
using FinanceManager.Components.Features.MoneyFlow.HttpClients;
using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.Assets.Services;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Dtos;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Services;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.MoneyFlow.Entities;
using FinanceManager.Domain.MoneyFlow.Services;
using FinanceManager.Infrastructure.Persistence;
using FinanceManager.Tests.Integration.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using Xunit;

namespace FinanceManager.Tests.Integration.Features.Dashboard.Controllers;

[Trait("Category", "Integration")]
public class DashboardAssetsDistributionFreshnessTests(OptionsProvider optionsProvider) : ControllerTests(optionsProvider)
{
    private const int _userId = 7949;
    private const int _accountId = 8949;
    private const long _listingId = 949;
    private const int _currencyId = 840;
    private static readonly DateTime _end = new(2024, 6, 10, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _start = _end.Date.AddDays(-7);
    private decimal _price = 100m;
    private TestDatabase? _database;
    private readonly Mock<INetWorthService> _netWorth = new();

    protected override bool ReuseApplicationHost => false;

    protected override void ConfigureServices(IServiceCollection services)
    {
        services.RemoveAll<DbContextOptions<AppDbContext>>();
        _database = new TestDatabase();
        services.AddSingleton(_database.Context);

        // Keep production Dashboard caching, AssetsService and InvestmentValuationService.
        // Only external prices and unrelated overview surfaces are deterministic.
        var prices = new Mock<IInvestmentPriceProvider>();
        prices.Setup(x => x.GetPricePerUnitAsync(_listingId, It.IsAny<Currency>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _price);
        services.AddSingleton(prices.Object);
        _netWorth.Setup(x => x.GetNetWorth(_userId, It.IsAny<Currency>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(new Dictionary<DateTime, decimal>());
        services.AddSingleton(_netWorth.Object);
        services.AddSingleton(Mock.Of<IBalanceService>());
        var liabilities = new Mock<ILiabilitiesService>();
        liabilities.Setup(x => x.GetEndLiabilitiesPerType(_userId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(Array.Empty<NameValueResult>().ToAsyncEnumerable());
        liabilities.Setup(x => x.GetEndLiabilitiesPerAccount(_userId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(Array.Empty<NameValueResult>().ToAsyncEnumerable());
        services.AddSingleton(liabilities.Object);
        services.AddSingleton(Mock.Of<ILabelsValueService>());
        services.AddSingleton(Mock.Of<IExpenseDistributionService>());
    }

    [Theory]
    [InlineData(140)]
    [InlineData(0)]
    public async Task Overview_AfterPriceChangesWithinTtl_AgreesWithDirectDistributionAndAccountValue(decimal price)
    {
        await Seed();
        var dashboard = new DashboardHttpClient(Client);
        var initial = await dashboard.GetOverview(_userId, _currencyId, _start, _end);
        Assert.Equal(300m, Assert.Single(initial!.AssetsPerType).Value);

        // A provider/cache refresh can change valuation without a user transaction mutation.
        _price = price;
        var currency = new Currency { Id = _currencyId };
        var assets = new AssetsHttpClient(Client);
        var types = await assets.GetEndAssetsPerType(_userId, currency, _end);
        var accounts = await assets.GetEndAssetsPerAccount(_userId, currency, _end);
        var value = await new InvestmentValuationHttpClient(Client).GetValueAsync(_accountId, _currencyId, _end);
        var refreshed = await dashboard.GetOverview(_userId, _currencyId, _start, _end);

        Assert.Equal(3m * price, value);
        Assert.Equal(value, types.Sum(x => x.Value));
        Assert.Equal(value, accounts.Sum(x => x.Value));
        Assert.Equal(value, refreshed!.AssetsPerType.Sum(x => x.Value));
        Assert.Equal(value, refreshed.AssetsPerAccount.Sum(x => x.Value));
        _netWorth.Verify(x => x.GetNetWorth(_userId, It.IsAny<Currency>(), _start, _end), Times.Once);
    }

    [Fact]
    public async Task Overview_WithDifferentIntradayBounds_ReturnsRequestedScope()
    {
        await Seed();
        var dashboard = new DashboardHttpClient(Client);
        await dashboard.GetOverview(_userId, _currencyId, _start, _end);
        var nextEnd = _end.AddHours(1);
        var result = await dashboard.GetOverview(_userId, _currencyId, _start, nextEnd);

        Assert.Equal(nextEnd, result!.EndDate);
        _netWorth.Verify(x => x.GetNetWorth(_userId, It.IsAny<Currency>(), _start, nextEnd), Times.Once);
    }

    private async Task Seed()
    {
        var context = _database!.Context;
        context.Currencies.Add(new Currency(_currencyId, "USD", "$"));
        context.Accounts.Add(new FinancialAccountBaseDto
        {
            AccountId = _accountId,
            UserId = _userId,
            Name = "Investment",
            AccountLabel = AccountLabel.Stock,
            AccountType = AccountType.Stock
        });
        context.Assets.Add(new Asset { Id = 949, Name = "Test equity", Type = AssetType.Stock });
        context.AssetListings.Add(new AssetListing
        {
            Id = _listingId,
            AssetId = 949,
            Ticker = "TEST",
            ExchangeMic = "XNAS",
            ExchangeName = "Test",
            TradingCurrency = "USD",
            PriceMultiplier = 1m,
            IsActive = true
        });
        context.InvestmentTransactions.Add(new InvestmentTransaction
        {
            UserId = _userId,
            AccountId = _accountId,
            AssetListingId = _listingId,
            Type = InvestmentTransactionType.Buy,
            Quantity = 3m,
            UnitPrice = 100m,
            Currency = "USD",
            TradeDate = new DateOnly(2024, 6, 1)
        });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        Authorize("testuser", _userId, UserRole.User);
    }

    public override void Dispose()
    {
        base.Dispose();
        _database?.Dispose();
        GC.SuppressFinalize(this);
    }
}