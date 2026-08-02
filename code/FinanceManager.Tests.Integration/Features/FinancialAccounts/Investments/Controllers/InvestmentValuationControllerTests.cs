using FinanceManager.Application.Identity.Users;
using FinanceManager.Components.Features.FinancialAccounts.HttpClients;
using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Dtos;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.MoneyFlow.Services;
using FinanceManager.Infrastructure.Persistence;
using FinanceManager.Tests.Integration.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using System.Net;
using Xunit;

namespace FinanceManager.Tests.Integration.Features.FinancialAccounts.Investments.Controllers;

[Trait("Category", "Integration")]
public class InvestmentValuationControllerTests(OptionsProvider optionsProvider) : ControllerTests(optionsProvider), IDisposable
{
    private const int _testUserId = 92;
    private const int _testAccountId = 792;
    private const long _listingId = 5002;
    private const int _usdCurrencyId = 840;
    private static readonly DateTime _asOf = new(2024, 6, 10);
    private TestDatabase? _testDatabase;
    private static readonly Mock<IInflationIndexProvider> _inflationIndexProvider = new();

    protected override void ConfigureServices(IServiceCollection services)
    {
        _inflationIndexProvider.Reset();

        var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
        if (descriptor != null)
            services.Remove(descriptor);

        _testDatabase = new TestDatabase();
        services.AddSingleton(_testDatabase.Context);

        var planVerifierMock = new Mock<IUserPlanVerifier>();
        planVerifierMock.Setup(x => x.CanAddMoreAccounts(_testUserId)).ReturnsAsync(true);
        planVerifierMock.Setup(x => x.CanAddMoreEntries(_testUserId, It.IsAny<int>())).ReturnsAsync(true);
        services.AddSingleton(planVerifierMock.Object);
        services.RemoveAll<IInflationIndexProvider>();
        services.AddSingleton(_inflationIndexProvider.Object);
    }

    private async Task SeedAccount()
    {
        if (_testDatabase is null) return;
        if (await _testDatabase.Context.Accounts.AnyAsync(a => a.AccountId == _testAccountId, TestContext.Current.CancellationToken)) return;
        _testDatabase.Context.Accounts.Add(new FinancialAccountBaseDto
        {
            AccountId = _testAccountId,
            UserId = _testUserId,
            Name = "Test Valuation Account",
            AccountLabel = AccountLabel.Stock,
            AccountType = AccountType.Stock
        });
        await _testDatabase.Context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    // Net holding 3 (buy 5, sell 2) on a USD listing priced at 100 USD, so account value is 300 USD.
    private async Task SeedHoldingsWithPrice()
    {
        await SeedAccount();
        var context = _testDatabase!.Context;

        context.Currencies.Add(new Currency(_usdCurrencyId, "USD", "$"));
        context.AssetListings.Add(new AssetListing
        {
            Id = _listingId,
            AssetId = 1,
            Ticker = "CSPX",
            ExchangeMic = "XNAS",
            ExchangeName = "United States",
            TradingCurrency = "USD",
            PriceMultiplier = 1m,
            IsActive = true
        });
        context.PriceQuotes.Add(new PriceQuote
        {
            AssetListingId = _listingId,
            Provider = MarketDataProvider.AlphaVantage,
            Price = 100m,
            Currency = "USD",
            PriceTime = new DateTimeOffset(_asOf, TimeSpan.Zero),
            QuoteType = PriceQuoteType.EndOfDay,
            FetchedAt = DateTimeOffset.UtcNow
        });
        context.InvestmentTransactions.AddRange(
            new InvestmentTransaction { UserId = _testUserId, AccountId = _testAccountId, AssetListingId = _listingId, Type = InvestmentTransactionType.Buy, Quantity = 5m, UnitPrice = 90m, Currency = "USD", TradeDate = new DateOnly(2024, 6, 1) },
            new InvestmentTransaction { UserId = _testUserId, AccountId = _testAccountId, AssetListingId = _listingId, Type = InvestmentTransactionType.Sell, Quantity = 2m, UnitPrice = 95m, Currency = "USD", TradeDate = new DateOnly(2024, 6, 5) });

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task GetHoldings_ReturnsNetHoldingPerListing()
    {
        await SeedHoldingsWithPrice();
        Authorize("testuser", _testUserId, UserRole.User);
        var client = new InvestmentValuationHttpClient(Client);

        var holdings = await client.GetHoldingsAsync(_testAccountId, _asOf);

        Assert.Equal(3m, Assert.Contains(_listingId, holdings));
    }

    [Fact]
    public async Task GetValue_ValuesHoldingsAtQuotePrice()
    {
        await SeedHoldingsWithPrice();
        Authorize("testuser", _testUserId, UserRole.User);
        var client = new InvestmentValuationHttpClient(Client);

        var value = await client.GetValueAsync(_testAccountId, _usdCurrencyId, _asOf);

        Assert.Equal(300m, value); // 3 units * 100 USD
    }

    [Fact]
    public async Task GetValueSeries_ReturnsDailyValues()
    {
        await SeedHoldingsWithPrice();
        Authorize("testuser", _testUserId, UserRole.User);
        var client = new InvestmentValuationHttpClient(Client);

        var series = await client.GetValueSeriesAsync(_testAccountId, _usdCurrencyId, _asOf.AddDays(-3), _asOf);

        Assert.NotEmpty(series);
        Assert.Equal(300m, series[_asOf]); // 3 units * 100 USD on the priced day
    }

    [Fact]
    public async Task GetValueSeries_WithInvalidDateRange_ReturnsBadRequest()
    {
        await SeedHoldingsWithPrice();
        Authorize("testuser", _testUserId, UserRole.User);

        var response = await Client.GetAsync(
            $"api/InvestmentValuation/ValueSeries/{_testAccountId}/{_usdCurrencyId}/{_asOf:yyyy-MM-dd}/{_asOf.AddDays(-1):yyyy-MM-dd}",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetBenchmarkSeries_DefaultsToInflationAndMatchesTheAccountsContributions()
    {
        await SeedHoldingsWithPrice();
        Authorize("testuser", _testUserId, UserRole.User);
        var start = _asOf.AddMonths(-1);
        _inflationIndexProvider
            .Setup(x => x.GetIndexSeriesAsync(start, _asOf, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<DateTime, decimal>
            {
                [start] = 100m,
                [_asOf] = 105m
            });
        var client = new InvestmentValuationHttpClient(Client);

        var series = await client.GetBenchmarkSeriesAsync(
            _testAccountId,
            _usdCurrencyId,
            start,
            _asOf,
            null);

        // 5 units bought at 100 on 1 Jun, 2 sold back out on 5 Jun: 500 in, then 200 out, all while
        // the index sits at 100, leaving 300 riding it up to the 105 print on the as-of date.
        Assert.Equal(500m, series[new DateTime(2024, 6, 1)]);
        Assert.Equal(300m, series[new DateTime(2024, 6, 5)]);
        Assert.Equal(315m, series[_asOf]);
    }

    [Fact]
    public async Task GetBenchmarkSeries_ForOtherUsersAccount_ReturnsForbidden()
    {
        await SeedHoldingsWithPrice();
        Authorize("otheruser", _testUserId + 1, UserRole.User);

        var response = await Client.GetAsync(
            $"api/InvestmentValuation/BenchmarkSeries/{_testAccountId}/{_usdCurrencyId}/{_asOf.AddMonths(-1):yyyy-MM-dd}/{_asOf:yyyy-MM-dd}",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetValue_ForOtherUsersAccount_ReturnsForbidden()
    {
        await SeedHoldingsWithPrice();
        Authorize("otheruser", _testUserId + 1, UserRole.User);

        var response = await Client.GetAsync(
            $"api/InvestmentValuation/Value/{_testAccountId}/{_usdCurrencyId}/{_asOf:yyyy-MM-dd}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetTransactionValuations_ReturnsBuyPerformance()
    {
        await SeedHoldingsWithPrice();
        Authorize("testuser", _testUserId, UserRole.User);
        var client = new InvestmentValuationHttpClient(Client);

        var valuations = await client.GetTransactionValuationsAsync(_testAccountId, _usdCurrencyId);

        // Only the Buy (5 @ 90 USD) is valued; latest quote is 100 USD.
        var valuation = Assert.Single(valuations);
        Assert.True(valuation.IsConverted);
        Assert.Equal("USD", valuation.Currency);
        Assert.True(valuation.HasCurrentPrice);
        Assert.Equal(90m, valuation.PurchaseUnitPrice); // 90 per unit
        Assert.Equal(450m, valuation.PurchaseValue);    // 5 * 90
        Assert.Equal(100m, valuation.CurrentPrice);
        Assert.Equal(500m, valuation.CurrentValuation); // 5 * 100
        Assert.Equal(50m, valuation.GainLoss);
    }

    [Fact]
    public async Task GetTransactionValuations_ForOtherUsersAccount_ReturnsForbidden()
    {
        await SeedHoldingsWithPrice();
        Authorize("otheruser", _testUserId + 1, UserRole.User);

        var response = await Client.GetAsync(
            $"api/InvestmentValuation/TransactionValuations/{_testAccountId}/{_usdCurrencyId}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetHoldings_ForUnknownAccount_ReturnsNotFound()
    {
        await SeedAccount();
        Authorize("testuser", _testUserId, UserRole.User);

        var response = await Client.GetAsync(
            $"api/InvestmentValuation/Holdings/999999/{_asOf:yyyy-MM-dd}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    public override void Dispose()
    {
        base.Dispose();
        _testDatabase?.Dispose();
        _testDatabase = null;
        GC.SuppressFinalize(this);
    }
}
