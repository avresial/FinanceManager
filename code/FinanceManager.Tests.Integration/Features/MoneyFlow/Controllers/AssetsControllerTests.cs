using FinanceManager.Components.Features.MoneyFlow.HttpClients;
using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Labels.Entities;
using FinanceManager.Domain.MoneyFlow.Entities;
using FinanceManager.Domain.Shared;
using FinanceManager.Domain.Transactions.Bonds.Entities;
using FinanceManager.Domain.Transactions.Cash.Entities;
using FinanceManager.Domain.Transactions.Investments.Entities;
using FinanceManager.Infrastructure.Persistence;
using FinanceManager.Tests.Integration.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace FinanceManager.Tests.Integration.Features.MoneyFlow.Controllers;

[Trait("Category", "Integration")]
public class AssetsControllerTests(OptionsProvider optionsProvider) : ControllerTests(optionsProvider), IDisposable
{
    private TestDatabase _testDatabase = null!;
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

        services.AddSingleton(_testDatabase.Context);
    }

    private async Task SeedCashEntries()
    {
        _value = 0;

        for (DateTime i = DateTime.UtcNow.AddMonths(-24).Date; i <= DateTime.UtcNow; i = i.AddDays(1))
            _testDatabase.Context.CurrencyEntries.Add(new CashEntry(null, 0, i, _value += _valueChange, _valueChange) { UserId = 1, CurrencyId = DefaultCurrency.PLN.Id });

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
        await SeedCashEntries();
        Authorize("TestUser", 1, UserRole.User);

        var result = await new AssetsHttpClient(Client).IsAnyAccountWithAssets(1);

        Assert.True(result);
    }

    [Fact]
    public async Task GetEndAssetsPerAccount_ReturnsList()
    {
        await SeedCashEntries();
        Authorize("TestUser", 1, UserRole.User);

        var result = await new AssetsHttpClient(Client).GetEndAssetsPerAccount(1, DefaultCurrency.USD, _nowUtc);

        Assert.NotNull(result);
        Assert.Single(result);
        // Cash is pooled per currency and named by it, not by the account the entries were booked in.
        Assert.Equal("PLN", result[0].Name);
        Assert.Equal(_value, result[0].Value);
    }

    [Fact]
    public async Task GetEndAssetsPerType_ReturnsList()
    {
        await SeedCashEntries();
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
        await SeedCashEntries();
        Authorize("TestUser", 1, UserRole.User);

        var result = await new AssetsHttpClient(Client).GetAssetsTimeSeries(1, DefaultCurrency.USD, _nowUtc.AddDays(-2), _nowUtc);

        Assert.NotNull(result);
        Assert.NotEmpty(result);

        Assert.All(result, item => Assert.True(item.Value > 0));
    }

    [Fact]
    public async Task GetMoneyWeightedReturn_WithoutInvestmentHistory_ReturnsInsufficientData()
    {
        await SeedCashEntries();
        Authorize("TestUser", 1, UserRole.User);

        var start = _nowUtc.AddDays(-30).Date;
        var response = await Client.GetAsync(
            $"api/Assets/GetMoneyWeightedReturn/1/{DefaultCurrency.PLN.Id}/{start:O}/{_nowUtc:O}",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<MoneyWeightedReturnResult>(TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.Equal(MoneyWeightedReturnStatus.InsufficientData, result.Status);
        Assert.Null(result.AnnualizedReturn);
    }

    [Fact]
    public async Task GetTimeWeightedReturn_WithoutInvestmentHistory_ReturnsInsufficientData()
    {
        await SeedCashEntries();
        Authorize("TestUser", 1, UserRole.User);

        var start = _nowUtc.AddDays(-30).Date;
        var response = await Client.GetAsync(
            $"api/Assets/GetTimeWeightedReturn/1/{DefaultCurrency.PLN.Id}/{start:O}/{_nowUtc:O}",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<TimeWeightedReturnResult>(TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.Equal(TimeWeightedReturnStatus.InsufficientData, result.Status);
        Assert.Null(result.TotalReturn);
    }

    [Fact]
    public async Task GetReturnAttribution_WithoutInvestmentHistory_ReturnsInsufficientData()
    {
        await SeedCashEntries();
        Authorize("TestUser", 1, UserRole.User);

        var start = _nowUtc.AddDays(-30).Date;
        var response = await Client.GetAsync(
            $"api/Assets/GetReturnAttribution/1/{DefaultCurrency.PLN.Id}/{start:O}/{_nowUtc:O}",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PortfolioReturnAttributionResult>(TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.Equal(PortfolioReturnAttributionStatus.InsufficientData, result.Status);
        Assert.Null(result.TotalChange);
    }

    [Fact]
    public async Task GetInvestmentPaycheckEstimate_ReturnsPartialSalaryHistoryMetadata()
    {
        var salary = new FinancialLabel { Name = "salary" };
        _testDatabase.Context.FinancialLabels.Add(salary);

        _testDatabase.Context.Bonds.Add(CreateBondDetails(1, _nowUtc));
        await _testDatabase.Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        _testDatabase.Context.CurrencyEntries.AddRange(
            new CashEntry(null, 1, _nowUtc.AddMonths(-2), 3000m, 3000m) { UserId = 1, CurrencyId = DefaultCurrency.PLN.Id, Labels = [salary] },
            new CashEntry(null, 2, _nowUtc, 4500m, 4500m) { UserId = 1, CurrencyId = DefaultCurrency.PLN.Id, Labels = [salary] });
        _testDatabase.Context.BondEntries.Add(new BondEntry(null, 1, _nowUtc.AddDays(-1), 12000m, 12000m, 1) { UserId = 1 });

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

    private async Task SeedInvestmentHoldings(
        int userId = 1,
        long listingId = 200,
        string ticker = "CSPX",
        decimal buyQuantity = 5m,
        decimal buyUnitPrice = 90m,
        decimal currentPrice = 100m)
    {
        var context = _testDatabase.Context;

        if (!await context.AssetListings.AnyAsync(x => x.Id == listingId, TestContext.Current.CancellationToken))
        {
            var asset = new Asset
            {
                Id = (int)listingId,
                Name = $"Asset {listingId}",
                Type = AssetType.ETF,
                TotalExpenseRatio = 0.005m,
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
            UserId = userId,

            AssetListingId = listingId,
            Type = InvestmentTransactionType.Buy,
            Quantity = buyQuantity,
            UnitPrice = buyUnitPrice,
            Currency = "USD",
            TradeDate = DateOnly.FromDateTime(_nowUtc.AddDays(-5))
        });

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    // Two lots of five and one of two cost 1200 in total and are worth 1440.
    private async Task SeedPooledInvestments()
    {
        await SeedInvestmentHoldings(userId: 7, listingId: 700, buyQuantity: 5m, buyUnitPrice: 90m, currentPrice: 120m);

        var context = _testDatabase.Context;

        InvestmentTransaction Buy(decimal quantity, decimal unitPrice) => new()
        {
            UserId = 7,

            AssetListingId = 700,
            Type = InvestmentTransactionType.Buy,
            Quantity = quantity,
            UnitPrice = unitPrice,
            Currency = "USD",
            TradeDate = DateOnly.FromDateTime(_nowUtc.AddDays(-4))
        };

        context.InvestmentTransactions.AddRange(Buy(5m, 110m), Buy(2m, 100m));
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task InvestmentGainLoss_UsesOwnerPoolAndChecksCurrencyAndIdentity()
    {
        await SeedPooledInvestments();
        var ct = TestContext.Current.CancellationToken;
        var client = new AssetsHttpClient(Client);
        Authorize("owner", 7, UserRole.User);
        var gain = await client.GetInvestmentGainLossAsync(7, DefaultCurrency.USD, _nowUtc, ct);
        Assert.NotNull(gain);
        Assert.Equal((1200m, 1440m), (gain.CostBasis, gain.CurrentValue));

        var route = $"api/Assets/GetInvestmentGainLoss/7";
        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync($"{route}/99999/{_nowUtc:O}", ct)).StatusCode);
        Authorize("guest", -17, UserRole.User);
        Assert.Equal(HttpStatusCode.Forbidden, (await Client.GetAsync($"{route}/{DefaultCurrency.USD.Id}/{_nowUtc:O}", ct)).StatusCode);
        Assert.Null(await client.GetInvestmentGainLossAsync(-17, DefaultCurrency.USD, _nowUtc, ct));
    }

    [Fact]
    public async Task GetUnrealizedGainLossPerAccount_PoolsLinkedAndUnlinkedTradesWithoutAccountId()
    {
        await SeedPooledInvestments();
        Authorize("TestUser", 7, UserRole.User);

        var response = await Client.GetAsync(
            $"api/Assets/GetUnrealizedGainLossPerAccount/7/{DefaultCurrency.USD.Id}/{_nowUtc:O}",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<List<UnrealizedGainLossResult>>(TestContext.Current.CancellationToken);
        var row = Assert.Single(result!);
        Assert.Equal("Investments", row.AccountName);
        Assert.Equal(1200m, row.CostBasis);
        Assert.Equal(1440m, row.CurrentValue);
        Assert.Equal(240m, row.UnrealizedGainLoss);
        Assert.Equal(20m, row.UnrealizedGainLossPercent);
        Assert.Equal(0, row.ExcludedInstrumentsCount);
    }

    [Fact]
    public async Task GetUnrealizedGainLossPerInstrument_PoolsOneRowPerListingAcrossAccounts()
    {
        await SeedPooledInvestments();
        Authorize("TestUser", 7, UserRole.User);

        var response = await Client.GetAsync(
            $"api/Assets/GetUnrealizedGainLossPerInstrument/7/{DefaultCurrency.USD.Id}/{_nowUtc:O}",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<List<UnrealizedGainLossInstrumentResult>>(TestContext.Current.CancellationToken);
        var row = Assert.Single(result!);
        Assert.Equal("Investments", row.AccountName);
        Assert.Equal("CSPX", row.InstrumentName);
        Assert.Equal(12m, row.Quantity);
        Assert.Equal(1200m, row.CostBasis);
        Assert.Equal(1440m, row.CurrentValue);
        Assert.Equal(240m, row.UnrealizedGainLoss);
        Assert.False(row.IsExcludedFromTotals);
    }

    [Fact]
    public async Task GetUnrealizedGainLoss_PoolsBondEntriesWithAndWithoutAccountIntoOneBondsRow()
    {
        var context = _testDatabase.Context;
        context.Bonds.Add(CreateBondDetails(8, _nowUtc));
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        context.BondEntries.AddRange(
            new BondEntry(null, 1, _nowUtc.AddDays(-1), 6000m, 6000m, 8) { UserId = 8 },
            new BondEntry(null, 2, _nowUtc.AddDays(-1), 12000m, 6000m, 8) { UserId = 8 });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        Authorize("TestUser", 8, UserRole.User);

        var perAccount = await Client.GetFromJsonAsync<List<UnrealizedGainLossResult>>(
            $"api/Assets/GetUnrealizedGainLossPerAccount/8/{DefaultCurrency.PLN.Id}/{_nowUtc:O}",
            TestContext.Current.CancellationToken);
        var perInstrument = await Client.GetFromJsonAsync<List<UnrealizedGainLossInstrumentResult>>(
            $"api/Assets/GetUnrealizedGainLossPerInstrument/8/{DefaultCurrency.PLN.Id}/{_nowUtc:O}",
            TestContext.Current.CancellationToken);

        var accountRow = Assert.Single(perAccount!);
        Assert.Equal("Bonds", accountRow.AccountName);
        Assert.Equal(12001.20m, accountRow.CurrentValue);
        Assert.Equal(0, accountRow.ExcludedInstrumentsCount);

        // Both entries hold the same bond, so they pool into one instrument.
        var instrumentRow = Assert.Single(perInstrument!);
        Assert.Equal(accountRow.CurrentValue, instrumentRow.CurrentValue);
    }

    [Fact]
    public async Task PortfolioReturns_IncludeUnlinkedTrades()
    {
        // The only trade has no account: performance is calculated from the owner's trades, not from accounts.
        await SeedInvestmentHoldings(userId: 1, listingId: 204, buyQuantity: 5m, buyUnitPrice: 90m, currentPrice: 100m);
        // A quote on the trade day prices every day of the holding period, so the time-weighted return is computable end to end.
        _testDatabase.Context.PriceQuotes.Add(new PriceQuote
        {
            AssetListingId = 204,
            Provider = MarketDataProvider.AlphaVantage,
            Price = 90m,
            Currency = "USD",
            PriceTime = new DateTimeOffset(_nowUtc.AddDays(-5).Date, TimeSpan.Zero),
            QuoteType = PriceQuoteType.EndOfDay,
            FetchedAt = DateTimeOffset.UtcNow
        });
        await _testDatabase.Context.SaveChangesAsync(TestContext.Current.CancellationToken);
        Authorize("TestUser", 1, UserRole.User);
        var start = _nowUtc.AddDays(-10).Date;

        var attribution = await Client.GetFromJsonAsync<PortfolioReturnAttributionResult>(
            $"api/Assets/GetReturnAttribution/1/{DefaultCurrency.USD.Id}/{start:O}/{_nowUtc:O}",
            TestContext.Current.CancellationToken);
        var moneyWeighted = await Client.GetFromJsonAsync<MoneyWeightedReturnResult>(
            $"api/Assets/GetMoneyWeightedReturn/1/{DefaultCurrency.USD.Id}/{start:O}/{_nowUtc:O}",
            TestContext.Current.CancellationToken);
        var timeWeighted = await Client.GetFromJsonAsync<TimeWeightedReturnResult>(
            $"api/Assets/GetTimeWeightedReturn/1/{DefaultCurrency.USD.Id}/{start:O}/{_nowUtc:O}",
            TestContext.Current.CancellationToken);

        Assert.NotNull(attribution);
        Assert.Equal(PortfolioReturnAttributionStatus.Available, attribution.Status);
        Assert.Equal(500m, attribution.TotalChange);
        Assert.Equal(450m, attribution.ExternalCashMovement);
        Assert.Equal(50m, attribution.MarketEffect);
        Assert.NotNull(moneyWeighted);
        Assert.Equal(MoneyWeightedReturnStatus.Available, moneyWeighted.Status);
        Assert.NotNull(timeWeighted);
        Assert.Equal(TimeWeightedReturnStatus.Available, timeWeighted.Status);
    }

    [Fact]
    public async Task PortfolioReturns_DoNotIncludeOtherUsersTrades()
    {
        await SeedInvestmentHoldings(userId: 2, listingId: 205);
        Authorize("TestUser", 1, UserRole.User);
        var start = _nowUtc.AddDays(-10).Date;

        var attribution = await Client.GetFromJsonAsync<PortfolioReturnAttributionResult>(
            $"api/Assets/GetReturnAttribution/1/{DefaultCurrency.USD.Id}/{start:O}/{_nowUtc:O}",
            TestContext.Current.CancellationToken);

        Assert.NotNull(attribution);
        Assert.Equal(PortfolioReturnAttributionStatus.InsufficientData, attribution.Status);
    }

    [Fact]
    public async Task GetFeeDragAnalysis_ReturnsCurrentEtfFeeCost()
    {
        await SeedInvestmentHoldings(userId: 1, listingId: 202, buyQuantity: 5m, buyUnitPrice: 90m, currentPrice: 100m);
        Authorize("TestUser", 1, UserRole.User);

        var result = await new AssetsHttpClient(Client).GetFeeDragAnalysis(
            1,
            DefaultCurrency.USD,
            _nowUtc,
            0.07m,
            TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(500m, result.TotalHoldingsValue);
        Assert.Equal(2.50m, result.AnnualFeeCost);
        Assert.Equal(1, result.TotalHoldingsCount);
        Assert.Equal(0, result.MissingTerCount);
        Assert.Equal([10, 20, 30], result.Projections.Select(projection => projection.Years).ToArray());
    }

    [Fact]
    public async Task GetFeeDragAnalysis_ForNegativeGuestUserIdReturnsEtfHoldings()
    {
        // Guest sandbox users are issued negative ids; the analysis must not treat them as invalid.
        const int guestUserId = -2;
        await SeedInvestmentHoldings(userId: guestUserId, listingId: 203, buyQuantity: 5m, buyUnitPrice: 90m, currentPrice: 100m);
        Authorize("TestUser", guestUserId, UserRole.User);

        var result = await new AssetsHttpClient(Client).GetFeeDragAnalysis(
            guestUserId,
            DefaultCurrency.USD,
            _nowUtc,
            0.07m,
            TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(1, result.TotalHoldingsCount);
        Assert.Equal(500m, result.TotalHoldingsValue);
        Assert.Equal(2.50m, result.AnnualFeeCost);
        Assert.Equal("CSPX", Assert.Single(result.Holdings).Ticker);
    }

    [Fact]
    public async Task GetFeeDragAnalysis_IncludesUnlinkedEtfTrades()
    {
        await SeedInvestmentHoldings(userId: 1, listingId: 206, buyQuantity: 5m, buyUnitPrice: 90m, currentPrice: 100m);
        Authorize("TestUser", 1, UserRole.User);

        var result = await new AssetsHttpClient(Client).GetFeeDragAnalysis(
            1,
            DefaultCurrency.USD,
            _nowUtc,
            0.07m,
            TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(500m, result.TotalHoldingsValue);
        Assert.Equal(1, result.TotalHoldingsCount);
    }

    [Fact]
    public async Task GetFeeDragAnalysis_WithInvalidReturnRateReturnsBadRequest()
    {
        Authorize("TestUser", 1, UserRole.User);

        var response = await Client.GetAsync(
            $"api/Assets/GetFeeDragAnalysis/1/{DefaultCurrency.USD.Id}/{_nowUtc:O}?assumedAnnualReturnRate=0.11",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    public static TheoryData<Func<DateTime, string>> OtherUserAssetEndpointUrls => new()
    {
        now => "api/Assets/IsAnyAccountWithAssets/2",
        now => $"api/Assets/GetEndAssetsPerAccount/2/{DefaultCurrency.USD.Id}/{now:O}",
        now => $"api/Assets/GetEndAssetsPerType/2/{DefaultCurrency.USD.Id}/{now:O}",
        now => $"api/Assets/GetAssetsTimeSeries/2/{DefaultCurrency.USD.Id}/{now.AddDays(-2):O}/{now:O}",
        now => $"api/Assets/GetAssetsTimeSeries/2/{DefaultCurrency.USD.Id}/{now.AddDays(-2):O}/{now:O}/{InvestmentType.Stock}",
        now => $"api/Assets/GetMoneyWeightedReturn/2/{DefaultCurrency.USD.Id}/{now.AddDays(-2):O}/{now:O}",
        now => $"api/Assets/GetTimeWeightedReturn/2/{DefaultCurrency.USD.Id}/{now.AddDays(-2):O}/{now:O}",
        now => $"api/Assets/GetReturnAttribution/2/{DefaultCurrency.USD.Id}/{now.AddDays(-2):O}/{now:O}",
        now => $"api/Assets/GetInvestmentPaycheckEstimate/2/{DefaultCurrency.USD.Id}/{now:O}",
        now => $"api/Assets/GetFeeDragAnalysis/2/{DefaultCurrency.USD.Id}/{now:O}",
        now => $"api/Assets/GetUnrealizedGainLossPerAccount/2/{DefaultCurrency.USD.Id}/{now:O}",
        now => $"api/Assets/GetUnrealizedGainLossPerInstrument/2/{DefaultCurrency.USD.Id}/{now:O}",
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
        _testDatabase = null!;
        GC.SuppressFinalize(this);
    }
}