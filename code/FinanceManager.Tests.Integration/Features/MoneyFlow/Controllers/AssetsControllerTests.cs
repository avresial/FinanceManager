using FinanceManager.Components.Features.MoneyFlow.HttpClients;
using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Dtos;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.MoneyFlow.Entities;
using FinanceManager.Domain.Shared;
using FinanceManager.Infrastructure.Persistence;
using FinanceManager.Tests.Integration.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using Xunit;

namespace FinanceManager.Tests.Integration.Features.MoneyFlow.Controllers;

[Trait("Category", "Integration")]
public class AssetsControllerTests(OptionsProvider optionsProvider) : ControllerTests(optionsProvider), IDisposable
{
    private TestDatabase? _testDatabase;
    private DateTime _nowUtc;
    private int _valueChange = 10;
    private int _value;

    protected override void ConfigureServices(IServiceCollection services)
    {
        // Legacy individual seeder hosted services removed; no need to unregister.

        _nowUtc = DateTime.UtcNow;
        _testDatabase = new TestDatabase();

        // remove any registration for AppDbContext
        var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
        if (descriptor != null)
            services.Remove(descriptor);

        services.AddSingleton(_testDatabase!.Context);

    }

    private async Task SeedWithTestCurrencyAccount(string accountName = "Test Currency Account")
    {

        if (await _testDatabase!.Context.Accounts.AnyAsync(x => x.Name == accountName, TestContext.Current.CancellationToken))
            return;

        _value = _valueChange;

        var test = new FinancialAccountBaseDto
        {
            UserId = 1,
            AccountId = 1,
            Name = accountName,
            AccountLabel = AccountLabel.Cash,
            AccountType = AccountType.Currency
        };

        _testDatabase!.Context.Accounts.Add(test);
        await _testDatabase.Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        for (DateTime i = DateTime.UtcNow.AddMonths(-24).Date; i <= DateTime.UtcNow; i = i.AddDays(1))
            _testDatabase!.Context.CurrencyEntries.Add(new CurrencyAccountEntry(test.AccountId, 0, i, _value += _valueChange, _valueChange));

        await _testDatabase.Context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static BondDetails CreateBondDetails(int id, DateTime nowUtc, decimal unitValue = 1m)
    {
        var calculationMethod = new BondCalculationMethod
        {
            Id = id,
            DateOperator = DateOperator.UntilDate,
            DateValue = DateOnly.FromDateTime(nowUtc.AddYears(1)).ToString("yyyy-MM-dd"),
            Rate = 0.0365m
        };

        return new BondDetails(
            $"Bond {id}",
            "Test Issuer",
            DateOnly.FromDateTime(nowUtc.AddYears(-1)),
            DateOnly.FromDateTime(nowUtc.AddYears(5)),
            [calculationMethod],
            unitValue: unitValue)
        { Id = id };
    }

    [Fact]
    public async Task IsAnyAccountWithAssets_ReturnsTrue()
    {
        await SeedWithTestCurrencyAccount();
        Authorize("TestUser", 1, UserRole.User);

        var result = await new AssetsHttpClient(Client).IsAnyAccountWithAssets(1);

        Assert.True(result);
    }

    [Fact]
    public async Task GetEndAssetsPerAccount_ReturnsList()
    {
        await SeedWithTestCurrencyAccount();
        Authorize("TestUser", 1, UserRole.User);

        var result = await new AssetsHttpClient(Client).GetEndAssetsPerAccount(1, DefaultCurrency.USD, _nowUtc);

        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal("Test Currency Account", result[0].Name);
        Assert.Equal(_value, result[0].Value);
    }

    [Fact]
    public async Task GetEndAssetsPerType_ReturnsList()
    {
        await SeedWithTestCurrencyAccount();
        Authorize("TestUser", 1, UserRole.User);

        var result = await new AssetsHttpClient(Client).GetEndAssetsPerType(1, DefaultCurrency.USD, _nowUtc);

        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal(AccountLabel.Cash.ToString(), result[0].Name);
        Assert.Equal(_value, result[0].Value);
    }

    [Fact]
    public async Task GetAssetsTimeSeries_ReturnsList()
    {
        await SeedWithTestCurrencyAccount();
        Authorize("TestUser", 1, UserRole.User);

        var result = await new AssetsHttpClient(Client).GetAssetsTimeSeries(1, DefaultCurrency.USD, _nowUtc.AddDays(-2), _nowUtc);

        Assert.NotNull(result);
        Assert.NotEmpty(result);

        Assert.All(result, item => Assert.True(item.Value > 0));
    }

    [Fact]
    public async Task GetInvestmentPaycheckEstimate_ReturnsPartialSalaryHistoryMetadata()
    {
        var salary = new FinancialLabel { Name = "salary" };
        _testDatabase!.Context.FinancialLabels.Add(salary);

        var salaryAccount = new FinancialAccountBaseDto
        {
            UserId = 1,
            AccountId = 10,
            Name = "Salary Account",
            AccountLabel = AccountLabel.Cash,
            AccountType = AccountType.Currency
        };

        var bondAccount = new FinancialAccountBaseDto
        {
            UserId = 1,
            AccountId = 11,
            Name = "Bond Portfolio",
            AccountLabel = AccountLabel.Other,
            AccountType = AccountType.Bond
        };

        _testDatabase.Context.Accounts.AddRange(salaryAccount, bondAccount);
        _testDatabase.Context.Bonds.Add(CreateBondDetails(1, _nowUtc));
        await _testDatabase.Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        _testDatabase.Context.CurrencyEntries.AddRange(
            new CurrencyAccountEntry(salaryAccount.AccountId, 1, _nowUtc.AddMonths(-2), 3000m, 3000m) { Labels = [salary] },
            new CurrencyAccountEntry(salaryAccount.AccountId, 2, _nowUtc, 4500m, 4500m) { Labels = [salary] });
        _testDatabase.Context.BondEntries.Add(new BondAccountEntry(bondAccount.AccountId, 1, _nowUtc.AddDays(-1), 12000m, 12000m, 1));

        await _testDatabase.Context.SaveChangesAsync(TestContext.Current.CancellationToken);
        Authorize("TestUser", 1, UserRole.User);

        var result = await new AssetsHttpClient(Client).GetInvestmentPaycheckEstimate(1, DefaultCurrency.USD, _nowUtc, 0.05m, 3);

        Assert.NotNull(result);
        Assert.Equal(12001.20m, result.InvestableAssetsValue);
        Assert.Equal(50m, result.SustainableMonthlyPaycheck);
        Assert.Equal(3, result.SalaryMonthsRequested);
        Assert.Equal(1, result.SalaryMonthsUsed);
        Assert.Equal(3000m, result.AverageMonthlySalary);
        Assert.True(result.HasPartialSalaryHistory);
    }

    private async Task SeedInvestmentAccountWithHoldings(
        int userId = 1,
        int accountId = 20,
        long listingId = 200,
        string ticker = "CSPX",
        decimal buyQuantity = 5m,
        decimal buyUnitPrice = 90m,
        decimal currentPrice = 100m)
    {
        var context = _testDatabase!.Context;

        if (await context.Accounts.AnyAsync(x => x.AccountId == accountId, TestContext.Current.CancellationToken))
            return;

        var account = new FinancialAccountBaseDto
        {
            UserId = userId,
            AccountId = accountId,
            Name = $"Investment Account {accountId}",
            AccountLabel = AccountLabel.Stock,
            AccountType = AccountType.Stock
        };
        context.Accounts.Add(account);

        if (!await context.AssetListings.AnyAsync(x => x.Id == listingId, TestContext.Current.CancellationToken))
        {
            var asset = new Asset
            {
                Id = (int)listingId,
                Name = $"Asset {listingId}",
                Type = AssetType.ETF
            };
            context.Assets.Add(asset);

            var listing = new AssetListing
            {
                Id = listingId,
                AssetId = asset.Id,
                Ticker = ticker,
                ExchangeMic = "XNAS",
                ExchangeName = "United States",
                TradingCurrency = "USD",
                PriceMultiplier = 1m,
                IsActive = true
            };
            context.AssetListings.Add(listing);

            context.PriceQuotes.Add(new PriceQuote
            {
                AssetListingId = listingId,
                Provider = MarketDataProvider.AlphaVantage,
                Price = currentPrice,
                Currency = "USD",
                PriceTime = new DateTimeOffset(MarketCalendar.LastMarketDay(_nowUtc), TimeSpan.Zero),
                QuoteType = PriceQuoteType.EndOfDay,
                FetchedAt = DateTimeOffset.UtcNow
            });
        }

        context.InvestmentTransactions.Add(new InvestmentTransaction
        {
            AccountId = accountId,
            AssetListingId = listingId,
            Type = InvestmentTransactionType.Buy,
            Quantity = buyQuantity,
            UnitPrice = buyUnitPrice,
            Currency = "USD",
            TradeDate = DateOnly.FromDateTime(_nowUtc.AddDays(-5))
        });

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task GetUnrealizedGainLossForAccount_ReturnsAccountAppreciation()
    {
        await SeedInvestmentAccountWithHoldings(userId: 1, accountId: 20, listingId: 200, buyQuantity: 5m, buyUnitPrice: 90m, currentPrice: 100m);
        Authorize("TestUser", 1, UserRole.User);

        var result = await new AssetsHttpClient(Client).GetUnrealizedGainLossForAccount(1, 20, DefaultCurrency.USD, _nowUtc, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(20, result.AccountId);
        Assert.Equal("Investment Account 20", result.AccountName);
        Assert.Equal(450m, result.CostBasis);
        Assert.Equal(500m, result.CurrentValue);
        Assert.Equal(50m, result.UnrealizedGainLoss);
        Assert.Equal(50m / 450m * 100m, result.UnrealizedGainLossPercent);

        var directResponse = await Client.GetAsync(
            $"api/Assets/GetUnrealizedGainLossForAccount/1/20/{DefaultCurrency.USD.Id}/{_nowUtc:O}",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, directResponse.StatusCode);
    }

    [Fact]
    public async Task GetUnrealizedGainLossForAccount_ForOtherUsersAccount_ReturnsNotFound()
    {
        await SeedInvestmentAccountWithHoldings(userId: 2, accountId: 21, listingId: 201);
        Authorize("TestUser", 1, UserRole.User);

        var response = await Client.GetAsync(
            $"api/Assets/GetUnrealizedGainLossForAccount/1/21/{DefaultCurrency.USD.Id}/{_nowUtc:O}",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var result = await new AssetsHttpClient(Client).GetUnrealizedGainLossForAccount(
            1,
            21,
            DefaultCurrency.USD,
            _nowUtc,
            TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetUnrealizedGainLossForAccount_ForUnknownAccount_ReturnsNotFound()
    {
        Authorize("TestUser", 1, UserRole.User);

        var response = await Client.GetAsync(
            $"api/Assets/GetUnrealizedGainLossForAccount/1/999999/{DefaultCurrency.USD.Id}/{_nowUtc:O}",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var clientResult = await new AssetsHttpClient(Client).GetUnrealizedGainLossForAccount(1, 999999, DefaultCurrency.USD, _nowUtc, TestContext.Current.CancellationToken);
        Assert.Null(clientResult);
    }

    public static TheoryData<Func<DateTime, string>> OtherUserAssetEndpointUrls => new()
    {
        now => "api/Assets/IsAnyAccountWithAssets/2",
        now => $"api/Assets/GetEndAssetsPerAccount/2/{DefaultCurrency.USD.Id}/{now:O}",
        now => $"api/Assets/GetEndAssetsPerType/2/{DefaultCurrency.USD.Id}/{now:O}",
        now => $"api/Assets/GetAssetsTimeSeries/2/{DefaultCurrency.USD.Id}/{now.AddDays(-2):O}/{now:O}",
        now => $"api/Assets/GetAssetsTimeSeries/2/{DefaultCurrency.USD.Id}/{now.AddDays(-2):O}/{now:O}/{InvestmentType.Stock}",
        now => $"api/Assets/GetInvestmentPaycheckEstimate/2/{DefaultCurrency.USD.Id}/{now:O}",
        now => $"api/Assets/GetUnrealizedGainLossPerAccount/2/{DefaultCurrency.USD.Id}/{now:O}",
        now => $"api/Assets/GetUnrealizedGainLossPerInstrument/2/{DefaultCurrency.USD.Id}/{now:O}",
        now => $"api/Assets/GetUnrealizedGainLossForAccount/2/20/{DefaultCurrency.USD.Id}/{now:O}",
    };

    [Theory]
    [MemberData(nameof(OtherUserAssetEndpointUrls))]
    public async Task UserScopedEndpoints_ForOtherUser_ReturnForbidden(Func<DateTime, string> endpointUrl)
    {
        Authorize("TestUser", 1, UserRole.User);

        var response = await Client.GetAsync(endpointUrl(_nowUtc), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    public override void Dispose()
    {
        base.Dispose();
        if (_testDatabase is null)
            return;

        _testDatabase.Dispose();
        _testDatabase = null;
        GC.SuppressFinalize(this);
    }
}