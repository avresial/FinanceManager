using FinanceManager.Api.Features.Portfolio.Controllers;
using FinanceManager.Application.Identity.Users;
using FinanceManager.Components.Features.MoneyFlow.HttpClients;
using FinanceManager.Components.Features.Portfolio.HttpClients;
using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.Currencies.Entities;
using FinanceManager.Domain.Currencies.Services;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.MoneyFlow.Services;
using FinanceManager.Domain.Transactions.Investments.Entities;
using FinanceManager.Infrastructure.Persistence;
using FinanceManager.Tests.Integration.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace FinanceManager.Tests.Integration.Features.Portfolio.Controllers;

[Trait("Category", "Integration")]
public class InvestmentValuationControllerTests(OptionsProvider optionsProvider) : ControllerTests(optionsProvider), IDisposable
{
    private const int _testUserId = 92;
    private const int _otherUserId = _testUserId + 1;
    private const long _listingId = 5002;
    private const int _usdCurrencyId = 840;
    private static readonly DateTime _asOf = new(2024, 6, 10);
    private TestDatabase? _testDatabase;
    private static readonly Mock<ICurrencyExchangeService> _currencyExchangeService = new();
    private static readonly Mock<IInflationIndexProvider> _inflationIndexProvider = new();

    protected override bool ReuseApplicationHost => false;

    protected override void ConfigureServices(IServiceCollection services)
    {
        _inflationIndexProvider.Reset();
        _currencyExchangeService.Reset();
        services.RemoveAll<ICurrencyExchangeService>();
        services.AddSingleton(_currencyExchangeService.Object);

        var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
        if (descriptor != null)
            services.Remove(descriptor);

        _testDatabase = new TestDatabase();
        services.AddSingleton(_testDatabase.Context);

        var planVerifierMock = new Mock<IUserPlanVerifier>();
        planVerifierMock.Setup(x => x.CanAddMoreEntries(_testUserId, It.IsAny<int>())).ReturnsAsync(true);
        services.AddSingleton(planVerifierMock.Object);
        services.RemoveAll<IInflationIndexProvider>();
        services.AddSingleton(_inflationIndexProvider.Object);
    }

    // Net holding 3 (buy 5, sell 2) on a USD listing priced at 100 USD, so the pooled value is
    // 300 USD. Another user's 100 units of the same listing must never leak in.
    private async Task SeedHoldingsWithPrice()
    {
        var context = _testDatabase!.Context;

        context.Currencies.Add(new Currency(_usdCurrencyId, "USD", "$"));
        context.Assets.Add(new Asset
        {
            Id = 1,
            Name = "iShares Core S&P 500 UCITS ETF",
            Type = AssetType.ETF
        });
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
            new InvestmentTransaction { UserId = _testUserId, AssetListingId = _listingId, Type = InvestmentTransactionType.Buy, Quantity = 5m, UnitPrice = 90m, Currency = "USD", TradeDate = new DateOnly(2024, 6, 1) },
            new InvestmentTransaction { UserId = _testUserId, AssetListingId = _listingId, Type = InvestmentTransactionType.Sell, Quantity = 2m, UnitPrice = 95m, Currency = "USD", TradeDate = new DateOnly(2024, 6, 5) },
            new InvestmentTransaction { UserId = _otherUserId, AssetListingId = _listingId, Type = InvestmentTransactionType.Buy, Quantity = 100m, UnitPrice = 90m, Currency = "USD", TradeDate = new DateOnly(2024, 6, 1) });

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task CapitalSeries_TracksOwnerInvestmentCostAsPositionsAreSold()
    {
        await SeedHoldingsWithPrice();
        Authorize("testuser", _testUserId, UserRole.User);
        var client = new InvestmentValuationHttpClient(Client);
        var capital = await client.GetCapitalSeriesAsync(_usdCurrencyId, _asOf.AddDays(-8), _asOf);
        Assert.Equal(450m, capital[_asOf.AddDays(-8)]);
        Assert.Equal(270m, capital[_asOf]);
    }

    [Fact]
    public async Task CapitalSeries_RejectsInvertedRange_UnknownCurrency_AndForbidsUserZero()
    {
        await SeedHoldingsWithPrice();
        var ct = TestContext.Current.CancellationToken;
        Authorize("testuser", _testUserId, UserRole.User);
        var route = "api/InvestmentValuation/CapitalSeries";

        Assert.Equal(HttpStatusCode.BadRequest, (await Client.GetAsync($"{route}/{_usdCurrencyId}/2024-06-10/2024-06-01", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync($"{route}/99999/2024-06-01/2024-06-10", ct)).StatusCode);
        Authorize("nobody", 0, UserRole.User);
        Assert.Equal(HttpStatusCode.Forbidden, (await Client.GetAsync($"{route}/{_usdCurrencyId}/2024-06-01/2024-06-10", ct)).StatusCode);
    }

    [Fact]
    public async Task CapitalSeries_ForAGuestWithANegativeId_UsesOnlyThatGuestsTrades()
    {
        await SeedHoldingsWithPrice();
        var context = _testDatabase!.Context;
        context.InvestmentTransactions.Add(new InvestmentTransaction { UserId = -7, AssetListingId = _listingId, Type = InvestmentTransactionType.Buy, Quantity = 1m, UnitPrice = 80m, Currency = "USD", TradeDate = new DateOnly(2024, 6, 2) });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var client = new InvestmentValuationHttpClient(Client);

        Authorize("guest", -7, UserRole.User);
        var guest = await client.GetCapitalSeriesAsync(_usdCurrencyId, _asOf.AddDays(-8), _asOf);
        Assert.Equal(80m, guest[_asOf]);

        Authorize("stranger", -8, UserRole.User);
        Assert.Empty(await client.GetCapitalSeriesAsync(_usdCurrencyId, _asOf.AddDays(-8), _asOf));
    }

    [Fact]
    public async Task GetValue_ValuesHoldingsAtQuotePrice()
    {
        await SeedHoldingsWithPrice();
        Authorize("testuser", _testUserId, UserRole.User);

        var value = await Client.GetFromJsonAsync<decimal>($"api/InvestmentValuation/Value/{_usdCurrencyId}/{_asOf:yyyy-MM-dd}", TestContext.Current.CancellationToken);

        Assert.Equal(300m, value); // 3 units * 100 USD
    }

    [Fact]
    public async Task GetValueSeries_ReturnsDailyValues()
    {
        await SeedHoldingsWithPrice();
        Authorize("testuser", _testUserId, UserRole.User);
        var client = new InvestmentValuationHttpClient(Client);

        var series = await client.GetValueSeriesAsync(_usdCurrencyId, _asOf.AddDays(-3), _asOf);

        Assert.NotEmpty(series);
        Assert.Equal(300m, series[_asOf]); // 3 units * 100 USD on the priced day
    }

    [Theory]
    [InlineData(7, false)]
    [InlineData(8, false)]
    [InlineData(9, false)]
    [InlineData(10, false)]
    [InlineData(10, true)]
    public async Task GetValueSeries_ClosingValueMatchesPointAndAssetsDistribution(int endDay, bool pending)
    {
        await SeedHoldingsWithPrice();
        var context = _testDatabase!.Context;
        context.Currencies.Add(new Currency(985, "PLN", "PLN"));
        var friday = new DateTime(2024, 6, 7);
        var end = new DateTime(2024, 6, endDay);
        foreach (var (date, price) in new[] { (friday, 100m), (friday.AddDays(1), 200m), (friday.AddDays(2), 300m) })
            context.PriceQuotes.Add(new PriceQuote
            {
                AssetListingId = _listingId,
                Price = price,
                Currency = "USD",
                PriceTime = new DateTimeOffset(date, TimeSpan.Zero),
                QuoteType = PriceQuoteType.EndOfDay
            });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        _currencyExchangeService
            .Setup(x => x.GetExchangeRateAsync(It.IsAny<Currency>(), It.IsAny<Currency>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync([(friday, 3m)]);
        _currencyExchangeService
            .Setup(x => x.GetExchangeRateResultAsync(It.IsAny<Currency>(), It.IsAny<Currency>(), end))
            .ReturnsAsync(pending
                ? new CurrencyExchangeRateResult.NotYetPublished(end, end.AddDays(1))
                : new CurrencyExchangeRateResult.Success(4m));
        Authorize("testuser", _testUserId, UserRole.User);
        var client = new InvestmentValuationHttpClient(Client);
        var point = await Client.GetFromJsonAsync<decimal>($"api/InvestmentValuation/Value/985/{end:yyyy-MM-dd}", TestContext.Current.CancellationToken);
        var series = await client.GetValueSeriesAsync(985, friday, end);
        var assets = new AssetsHttpClient(Client);
        var currency = new Currency(985, "PLN", "PLN");
        var distribution = await assets.GetEndAssetsPerType(_testUserId, currency, end);
        var history = await assets.GetAssetsTimeSeries(_testUserId, currency, friday, end);

        Assert.Equal(pending ? 0m : 1200m, point);
        Assert.Equal(point, series[end]);
        Assert.Equal(point, distribution.Sum(x => x.Value));
        Assert.Equal(point, history.MaxBy(x => x.DateTime)!.Value);
    }

    [Fact]
    public async Task GetValueSeries_WithInvalidDateRange_ReturnsBadRequest()
    {
        await SeedHoldingsWithPrice();
        Authorize("testuser", _testUserId, UserRole.User);

        var response = await Client.GetAsync(
            $"api/InvestmentValuation/ValueSeries/{_usdCurrencyId}/{_asOf:yyyy-MM-dd}/{_asOf.AddDays(-1):yyyy-MM-dd}",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetBenchmarkSeries_DefaultsToInflationAndMatchesThePooledContributions()
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
    public async Task OtherUsers_SeeOnlyTheirOwnHoldingsAndValues()
    {
        await SeedHoldingsWithPrice();
        Authorize("otheruser", _otherUserId, UserRole.User);
        var client = new InvestmentValuationHttpClient(Client);

        var value = await Client.GetFromJsonAsync<decimal>($"api/InvestmentValuation/Value/{_usdCurrencyId}/{_asOf:yyyy-MM-dd}", TestContext.Current.CancellationToken);

        // The other user only holds their own 100 units, never the first user's 3.
        Assert.Equal(10_000m, value);
    }

    [Fact]
    public async Task GuestUsers_WithNegativeIds_AreServedTheirOwnPooledHoldings()
    {
        await SeedHoldingsWithPrice();
        _testDatabase!.Context.InvestmentTransactions.Add(new InvestmentTransaction
        {
            UserId = -7,
            AssetListingId = _listingId,
            Type = InvestmentTransactionType.Buy,
            Quantity = 4m,
            UnitPrice = 90m,
            Currency = "USD",
            TradeDate = new DateOnly(2024, 6, 1)
        });
        await _testDatabase.Context.SaveChangesAsync(TestContext.Current.CancellationToken);
        Authorize("guest", -7, UserRole.User);

        var value = await Client.GetFromJsonAsync<decimal>($"api/InvestmentValuation/Value/{_usdCurrencyId}/{_asOf:yyyy-MM-dd}", TestContext.Current.CancellationToken);

        Assert.Equal(400m, value);
    }

    [Fact]
    public async Task GetBenchmarkSeries_ForUnknownBenchmarkListing_ReturnsNotFound()
    {
        await SeedHoldingsWithPrice();
        Authorize("testuser", _testUserId, UserRole.User);

        var response = await Client.GetAsync(
            $"api/InvestmentValuation/BenchmarkSeries/{_usdCurrencyId}/{_asOf.AddMonths(-1):yyyy-MM-dd}/{_asOf:yyyy-MM-dd}?listingId=999999",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetBenchmarkSeries_WithInvalidDateRange_ReturnsBadRequest()
    {
        await SeedHoldingsWithPrice();
        Authorize("testuser", _testUserId, UserRole.User);

        var response = await Client.GetAsync(
            $"api/InvestmentValuation/BenchmarkSeries/{_usdCurrencyId}/{_asOf:yyyy-MM-dd}/{_asOf.AddDays(-1):yyyy-MM-dd}",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetValue_ForUnknownCurrency_ReturnsNotFound()
    {
        await SeedHoldingsWithPrice();
        Authorize("testuser", _testUserId, UserRole.User);

        var response = await Client.GetAsync($"api/InvestmentValuation/Value/999999/{_asOf:yyyy-MM-dd}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<long> BuyId() => await _testDatabase!.Context.InvestmentTransactions
        .Where(x => x.UserId == _testUserId && x.Type == InvestmentTransactionType.Buy && x.Quantity == 5m)
        .Select(x => x.EntryId)
        .SingleAsync(TestContext.Current.CancellationToken);

    [Fact]
    public async Task GetTransactionValuations_ReturnsBuyPerformance()
    {
        await SeedHoldingsWithPrice();
        Authorize("testuser", _testUserId, UserRole.User);
        var client = new InvestmentValuationHttpClient(Client);
        var buyId = await BuyId();
        var sellId = await _testDatabase!.Context.InvestmentTransactions
            .Where(x => x.UserId == _testUserId && x.Type == InvestmentTransactionType.Sell)
            .Select(x => x.EntryId)
            .SingleAsync(TestContext.Current.CancellationToken);

        var valuations = await client.GetTransactionValuationsAsync(
            _usdCurrencyId, [buyId, sellId], TestContext.Current.CancellationToken);

        // Only the Buy (5 @ 90 USD) is valued; latest quote is 100 USD.
        var valuation = Assert.Single(valuations);
        Assert.Equal(buyId, valuation.TransactionId);
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
    public async Task GetTransactionValuations_ValuesAnUnlinkedPurchase()
    {
        await SeedHoldingsWithPrice();
        var context = _testDatabase!.Context;
        var unlinked = context.InvestmentTransactions.Add(new InvestmentTransaction
        {
            UserId = _testUserId,
            AssetListingId = _listingId,
            Type = InvestmentTransactionType.Buy,
            Quantity = 1m,
            UnitPrice = 80m,
            Currency = "USD",
            TradeDate = new DateOnly(2024, 6, 2)
        });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        Authorize("testuser", _testUserId, UserRole.User);
        var client = new InvestmentValuationHttpClient(Client);

        var valuations = await client.GetTransactionValuationsAsync(
            _usdCurrencyId, [unlinked.Entity.EntryId, await BuyId()], TestContext.Current.CancellationToken);

        Assert.Equal(2, valuations.Count);
        Assert.Equal(20m, valuations.Single(x => x.TransactionId == unlinked.Entity.EntryId).GainLoss); // 1 * (100 - 80)
    }

    [Fact]
    public async Task GetTransactionValuations_NeverReturnsAnotherUsersTrades()
    {
        await SeedHoldingsWithPrice();
        var buyId = await BuyId();
        Authorize("otheruser", _otherUserId, UserRole.User);
        var client = new InvestmentValuationHttpClient(Client);

        var valuations = await client.GetTransactionValuationsAsync(_usdCurrencyId, [buyId], TestContext.Current.CancellationToken);

        Assert.Empty(valuations);
    }

    [Theory]
    [InlineData("")]
    [InlineData("?transactionIds=")]
    public async Task GetTransactionValuations_WithoutIds_ReturnsBadRequest(string query)
    {
        await SeedHoldingsWithPrice();
        Authorize("testuser", _testUserId, UserRole.User);

        var response = await Client.GetAsync($"api/InvestmentValuation/TransactionValuations/{_usdCurrencyId}{query}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetTransactionValuations_AboveTheIdCap_ReturnsBadRequest()
    {
        await SeedHoldingsWithPrice();
        Authorize("testuser", _testUserId, UserRole.User);
        var ids = string.Join("&", Enumerable.Range(1, InvestmentValuationController.MaxTransactionValuationIds + 1).Select(id => $"transactionIds={id}"));

        var response = await Client.GetAsync($"api/InvestmentValuation/TransactionValuations/{_usdCurrencyId}?{ids}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    public override void Dispose()
    {
        base.Dispose();
        _testDatabase?.Dispose();
        _testDatabase = null;
        GC.SuppressFinalize(this);
    }
}