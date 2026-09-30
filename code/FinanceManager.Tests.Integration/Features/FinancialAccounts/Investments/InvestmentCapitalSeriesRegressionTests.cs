using FinanceManager.Application.FinancialAccounts.Investments.Valuation;
using FinanceManager.Components.Features.MoneyFlow.HttpClients;
using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.Assets.Services;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Repositories;
using FinanceManager.Domain.FinancialAccounts.Currencies.Services;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Repositories;
using FinanceManager.Domain.FinancialAccounts.Shared.Dtos;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.MoneyFlow.Entities;
using FinanceManager.Domain.MoneyFlow.Services;
using FinanceManager.Infrastructure.Features.FinancialAccounts.Investments.Repositories;
using FinanceManager.Infrastructure.Persistence;
using FinanceManager.Tests.Integration.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using Xunit;

namespace FinanceManager.Tests.Integration.Features.FinancialAccounts.Investments;

[Trait("Category", "Integration")]
public sealed class InvestmentCapitalSeriesRegressionTests(OptionsProvider _optionsProvider) : ControllerTests(_optionsProvider), IDisposable
{
    private const int _userId = 92;
    private const int _accountId = 792;
    private const long _listingId = 5002;
    private static readonly DateOnly _fixtureStart = new(2025, 12, 4);
    private static readonly DateOnly _fixtureEnd = new(2026, 7, 28);

    // Expected costs and cumulative values are fixed from the issue's independent production reconstruction.
    private static readonly IReadOnlyList<CapitalTrade> _productionTrades =
    [
        new(new DateOnly(2025, 12, 4), 0.1867m, 725.16m, 135.387372m, 135.387372m, 541.549488m),
        new(new DateOnly(2026, 1, 12), 0.4053m, 725.16m, 293.907348m, 429.294720m, 1717.178880m),
        new(new DateOnly(2026, 3, 2), 1.0000m, 735.31m, 735.310000m, 1254.753726m, 5019.014904m),
        new(new DateOnly(2026, 3, 2), 0.1226m, 735.31m, 90.149006m, 1254.753726m, 5019.014904m),
        new(new DateOnly(2026, 4, 27), 0.3581m, 768.70m, 275.271470m, 1530.025196m, 6120.100784m),
        new(new DateOnly(2026, 5, 6), 0.7032m, 789.22m, 554.979504m, 2085.004700m, 8340.018800m),
        new(new DateOnly(2026, 5, 26), 0.7270m, 808.30m, 587.634100m, 2672.638800m, 10690.555200m),
        new(new DateOnly(2026, 6, 1), 0.8123m, 816.18m, 662.983014m, 3335.621814m, 13342.487256m),
        new(new DateOnly(2026, 6, 9), 0.8345m, 813.67m, 679.007615m, 4014.629429m, 16058.517716m),
        new(new DateOnly(2026, 6, 15), 0.6760m, 813.85m, 550.162600m, 4564.792029m, 18259.168116m),
        new(new DateOnly(2026, 7, 6), 0.8133m, 811.15m, 659.708295m, 5224.500324m, 20898.001296m),
        new(new DateOnly(2026, 7, 28), 1.0346m, 801.86m, 829.604356m, 6054.104680m, 24216.418720m)
    ];

    private TestDatabase? _testDatabase;
    private Mock<ICurrencyExchangeService>? _currencyExchangeService;
    private decimal _january12UsdToPlnRate = 4m;

    protected override bool ReuseApplicationHost => false;

    protected override void ConfigureServices(IServiceCollection services)
    {
        _testDatabase = new TestDatabase();
        services.RemoveAll<DbContextOptions<AppDbContext>>();
        services.AddSingleton(_testDatabase.Context);

        var currencyRepository = new Mock<ICurrencyRepository>();
        currencyRepository
            .Setup(x => x.GetCurrencies(It.IsAny<CancellationToken>()))
            .Returns(AsyncEnumerable.Range(0, 2).Select(index => index == 0 ? DefaultCurrency.USD : DefaultCurrency.PLN));
        services.RemoveAll<ICurrencyRepository>();
        services.AddSingleton(currencyRepository.Object);

        _currencyExchangeService = new Mock<ICurrencyExchangeService>();
        _currencyExchangeService
            .Setup(x => x.GetExchangeRateAsync(
                It.IsAny<Currency>(),
                It.IsAny<Currency>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>()))
            .Returns((Currency from, Currency to, DateTime start, DateTime end) =>
                Task.FromResult(CreateRates(from, to, start, end)));
        services.RemoveAll<ICurrencyExchangeService>();
        services.AddSingleton(_currencyExchangeService.Object);
    }

    [Fact]
    public async Task GetCapitalFlowInputs_ProjectsEveryPersistedProductionTradeExactlyOnce()
    {
        await SeedProductionFixture();
        var repository = new InvestmentTransactionRepository(_testDatabase!.Context);

        var flows = await repository.GetCapitalFlowInputs([_accountId], _fixtureEnd, TestContext.Current.CancellationToken);
        var persisted = await _testDatabase.Context.InvestmentTransactions
            .AsNoTracking()
            .OrderBy(x => x.TradeDate)
            .ThenBy(x => x.Id)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(_productionTrades.Count, flows.Count);
        Assert.Equal(_productionTrades.Count, flows.Select(x => x.TransactionId).Distinct().Count());
        Assert.Equal(persisted.Select(x => x.Id), flows.Select(x => x.TransactionId));

        for (var index = 0; index < _productionTrades.Count; index++)
        {
            var expected = _productionTrades[index];
            var actual = flows[index];

            Assert.True(actual.TransactionId > 0);
            Assert.Equal(_accountId, actual.AccountId);
            Assert.Equal(_listingId, actual.AssetListingId);
            Assert.Equal(expected.TradeDate, actual.TradeDate);
            Assert.Equal(InvestmentTransactionType.Buy, actual.Type);
            Assert.Equal(expected.Quantity, actual.Quantity);
            Assert.Equal(expected.UnitPrice, actual.UnitPrice);
            Assert.Null(actual.Fee);
            Assert.Equal("USD", actual.Currency);
            Assert.Equal(1m, actual.ListingPriceMultiplier);
        }
    }

    [Fact]
    public async Task InvestmentValuationService_GetCapitalSeries_MatchesEveryProductionDailyValueInUsdAndPln()
    {
        await SeedProductionFixture();
        var start = _fixtureStart.ToDateTime(TimeOnly.MinValue);
        var end = _fixtureEnd.ToDateTime(TimeOnly.MinValue);
        var valuationService = CreateValuationService();

        var usd = await valuationService.GetCapitalSeriesAsync(
            [_accountId], DefaultCurrency.USD, start, end, TestContext.Current.CancellationToken);
        var pln = await valuationService.GetCapitalSeriesAsync(
            [_accountId], DefaultCurrency.PLN, start, end, TestContext.Current.CancellationToken);
        var usdDaily = usd[_accountId];
        var plnDaily = pln[_accountId];

        Assert.Equal((end - start).Days + 1, usdDaily.Count);
        Assert.Equal(237, usdDaily.Count);
        Assert.Equal(usdDaily.Keys, plnDaily.Keys);

        decimal expectedCapital = 0m;
        var previousCapital = 0m;
        var previousPlnCapital = 0m;
        var projectedFlows = await new InvestmentTransactionRepository(_testDatabase!.Context)
            .GetCapitalFlowInputs([_accountId], _fixtureEnd, TestContext.Current.CancellationToken);
        for (var date = start; date <= end; date = date.AddDays(1))
        {
            var flowsForDay = projectedFlows.Where(flow => flow.TradeDate.ToDateTime(TimeOnly.MinValue) == date).ToList();
            var dayCost = _productionTrades
                .Where(trade => trade.TradeDate.ToDateTime(TimeOnly.MinValue) == date.Date)
                .Sum(trade => trade.ExpectedRawUsd);
            expectedCapital += dayCost;
            var transactionDetails = flowsForDay.Count == 0
                ? "no transaction; opening capital carries forward"
                : string.Join("; ", flowsForDay.Select(flow =>
                    $"id={flow.TransactionId}, date={flow.TradeDate:yyyy-MM-dd}, quantity={flow.Quantity}, unitPrice={flow.UnitPrice}, multiplier={flow.ListingPriceMultiplier}, currency={flow.Currency}, fee={flow.Fee}, FX USD/PLN=4, expected raw cost={_productionTrades.Single(trade => trade.TradeDate == flow.TradeDate && trade.Quantity == flow.Quantity).ExpectedRawUsd}, expected PLN cost={_productionTrades.Single(trade => trade.TradeDate == flow.TradeDate && trade.Quantity == flow.Quantity).ExpectedRawUsd * 4m}"));
            var context = $"{date:yyyy-MM-dd}: {transactionDetails}";

            Assert.True(usdDaily.TryGetValue(date, out var usdValue), $"Missing USD valuation for {context}");
            Assert.True(plnDaily.TryGetValue(date, out var plnValue), $"Missing PLN valuation for {context}");
            Assert.True(
                usdValue == expectedCapital,
                $"{context}; expected cumulative USD={expectedCapital}, actual={usdValue}, expected daily delta={dayCost}, actual daily delta={usdValue - previousCapital}");
            Assert.True(
                plnValue == expectedCapital * 4m,
                $"{context}; expected cumulative PLN={expectedCapital * 4m}, actual={plnValue}, expected daily delta={dayCost * 4m}, actual daily delta={plnValue - previousPlnCapital}");
            Assert.True(usdValue >= previousCapital, $"Buy-only capital decreased on {context}");
            if (dayCost == 0m)
                Assert.Equal(previousCapital, usdValue);

            previousCapital = usdValue;
            previousPlnCapital = plnValue;
        }

        foreach (var trade in _productionTrades.GroupBy(x => x.TradeDate).Select(x => x.Last()))
        {
            var date = trade.TradeDate.ToDateTime(TimeOnly.MinValue);
            Assert.Equal(trade.ExpectedCumulativeUsd, usdDaily[date]);
            Assert.Equal(trade.ExpectedCumulativePln, plnDaily[date]);
        }

        Assert.Equal(825.459006m, _productionTrades
            .Where(trade => trade.TradeDate == new DateOnly(2026, 3, 2))
            .Sum(trade => trade.ExpectedRawUsd));
    }

    [Fact]
    public async Task MoneyFlowApi_GetCapital_DailySeriesIsMonotonicAndChangesOnlyOnBuyDates()
    {
        await SeedProductionFixture();
        Authorize("testuser", _userId, UserRole.User);
        var client = new MoneyFlowHttpClient(Client);
        var start = new DateTime(2026, 1, 1);
        var end = new DateTime(2026, 1, 31);

        var usd = await client.GetCapital(_userId, DefaultCurrency.USD, start, end, [_accountId]);
        var pln = await client.GetCapital(_userId, DefaultCurrency.PLN, start, end, [_accountId]);

        AssertDailyCapitalInvariant(
            usd,
            start,
            end,
            135.387372m,
            new DateOnly(2026, 1, 12),
            _productionTrades.Single(trade => trade.TradeDate == new DateOnly(2026, 1, 12)).ExpectedRawUsd);
        AssertDailyCapitalInvariant(pln, start, end, 541.549488m, new DateOnly(2026, 1, 12), 1175.629392m);
    }

    [Fact]
    public async Task MoneyFlowApi_GetCapital_CombinesSameDayFractionalBuysWithoutDuplication()
    {
        await SeedProductionFixture();
        Authorize("testuser", _userId, UserRole.User);
        var client = new MoneyFlowHttpClient(Client);
        var start = new DateTime(2026, 3, 1);
        var end = new DateTime(2026, 3, 31);

        var series = await client.GetCapital(_userId, DefaultCurrency.USD, start, end, [_accountId]);
        var values = series.ToDictionary(x => x.DateTime.Date, x => x.Value);

        Assert.Equal(1254.753726m, values[new DateTime(2026, 3, 2)]);
        Assert.Equal(825.459006m, values[new DateTime(2026, 3, 2)] - values[new DateTime(2026, 3, 1)]);
        Assert.Equal(values[new DateTime(2026, 3, 2)], values[new DateTime(2026, 3, 31)]);
    }

    [Fact]
    public async Task MoneyFlowApi_GetCapital_CarriesOpeningCapitalIntoJanuaryAndLaterRanges()
    {
        await SeedProductionFixture();
        Authorize("testuser", _userId, UserRole.User);
        var client = new MoneyFlowHttpClient(Client);

        var january = await client.GetCapital(_userId, DefaultCurrency.USD, new DateTime(2026, 1, 1), new DateTime(2026, 1, 31), [_accountId]);
        var february = await client.GetCapital(_userId, DefaultCurrency.USD, new DateTime(2026, 2, 1), new DateTime(2026, 2, 28), [_accountId]);

        Assert.Equal(135.387372m, january[0].Value);
        Assert.Equal(429.294720m, january[^1].Value);
        Assert.All(february, day => Assert.Equal(429.294720m, day.Value));
    }

    [Fact]
    public async Task InvestmentValuationService_GetCapitalSeries_UsesOnlyTheHistoricalRateForEachTradeDate()
    {
        await SeedProductionFixture();
        var start = _fixtureStart.ToDateTime(TimeOnly.MinValue);
        var end = _fixtureEnd.ToDateTime(TimeOnly.MinValue);
        var valuationService = CreateValuationService();

        _january12UsdToPlnRate = 4m;
        var baseline = await valuationService.GetCapitalSeriesAsync(
            [_accountId], DefaultCurrency.PLN, start, end, TestContext.Current.CancellationToken);
        _january12UsdToPlnRate = 5m;
        var changed = await valuationService.GetCapitalSeriesAsync(
            [_accountId], DefaultCurrency.PLN, start, end, TestContext.Current.CancellationToken);
        var baselineDaily = baseline[_accountId];
        var changedDaily = changed[_accountId];

        foreach (var date in baselineDaily.Keys)
        {
            var difference = changedDaily[date] - baselineDaily[date];
            Assert.Equal(date < new DateTime(2026, 1, 12) ? 0m : 293.907348m, difference);
        }
    }

    [Fact]
    public async Task MoneyFlowApi_GetCapital_FullRangeReturnsExpectedMonthlyClosingValues()
    {
        await SeedProductionFixture();
        Authorize("testuser", _userId, UserRole.User);
        var client = new MoneyFlowHttpClient(Client);
        var start = _fixtureStart.ToDateTime(TimeOnly.MinValue);
        var end = _fixtureEnd.ToDateTime(TimeOnly.MinValue);

        var usd = await client.GetCapital(_userId, DefaultCurrency.USD, start, end, [_accountId]);
        var pln = await client.GetCapital(_userId, DefaultCurrency.PLN, start, end, [_accountId]);
        decimal[] expectedMonthlyUsd =
        [
            135.387372m,
            429.294720m,
            429.294720m,
            1254.753726m,
            1530.025196m,
            2672.638800m,
            4564.792029m,
            6054.104680m
        ];
        decimal[] expectedMonthlyPln =
        [
            541.549488m,
            1717.178880m,
            1717.178880m,
            5019.014904m,
            6120.100784m,
            10690.555200m,
            18259.168116m,
            24216.418720m
        ];

        DateTime[] expectedMonthlyDates =
        [
            new(2025, 12, 4),
            new(2026, 1, 1),
            new(2026, 2, 1),
            new(2026, 3, 1),
            new(2026, 4, 1),
            new(2026, 5, 1),
            new(2026, 6, 1),
            new(2026, 7, 1)
        ];

        Assert.Equal(expectedMonthlyUsd, usd.Select(x => x.Value));
        Assert.Equal(expectedMonthlyPln, pln.Select(x => x.Value));
        Assert.Equal(expectedMonthlyDates, usd.Select(x => x.DateTime.Date));
        Assert.Equal(expectedMonthlyDates, pln.Select(x => x.DateTime.Date));
    }

    [Fact]
    public async Task MoneyFlowApi_GetCapital_AppliesFeeOnceAndMinorQuoteMultiplier()
    {
        await SeedAccountAndListings();
        var context = _testDatabase!.Context;
        context.InvestmentTransactions.AddRange(
            new InvestmentTransaction
            {
                UserId = _userId,
                AccountId = _accountId,
                AssetListingId = _listingId,
                Type = InvestmentTransactionType.Buy,
                Quantity = 2m,
                UnitPrice = 10m,
                Fee = 5m,
                Currency = "USD",
                TradeDate = new DateOnly(2026, 1, 12)
            },
            new InvestmentTransaction
            {
                UserId = _userId,
                AccountId = _accountId,
                AssetListingId = _listingId + 1,
                Type = InvestmentTransactionType.Buy,
                Quantity = 2m,
                UnitPrice = 100m,
                Currency = "GBX",
                TradeDate = new DateOnly(2026, 1, 12)
            });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        Authorize("testuser", _userId, UserRole.User);

        var day = new DateTime(2026, 1, 12);
        var series = await new MoneyFlowHttpClient(Client)
            .GetCapital(_userId, DefaultCurrency.PLN, day, day, [_accountId]);

        // (2 * $10 + $5 fee) * 4 PLN/USD + (2 * 100 GBX * 0.01) * 5 PLN/GBP.
        Assert.Equal(110m, Assert.Single(series).Value);
    }

    private async Task SeedProductionFixture()
    {
        await SeedAccountAndListings();
        _testDatabase!.Context.InvestmentTransactions.AddRange(_productionTrades.Select(trade => new InvestmentTransaction
        {
            UserId = _userId,
            AccountId = _accountId,
            AssetListingId = _listingId,
            Type = InvestmentTransactionType.Buy,
            Quantity = trade.Quantity,
            UnitPrice = trade.UnitPrice,
            Currency = "USD",
            TradeDate = trade.TradeDate
        }));
        await _testDatabase.Context.SaveChangesAsync(TestContext.Current.CancellationToken);
        Authorize("testuser", _userId, UserRole.User);
    }

    private InvestmentValuationService CreateValuationService() => new(
        new InvestmentTransactionRepository(_testDatabase!.Context),
        Mock.Of<IInvestmentPriceProvider>(),
        Mock.Of<IInflationIndexProvider>(),
        _currencyExchangeService!.Object);

    private async Task SeedAccountAndListings()
    {
        var context = _testDatabase!.Context;
        context.Accounts.Add(new FinancialAccountBaseDto
        {
            AccountId = _accountId,
            UserId = _userId,
            Name = "XTB Investment Account",
            AccountLabel = AccountLabel.Stock,
            AccountType = AccountType.Stock
        });
        context.Assets.Add(new Asset { Id = 1, Name = "iShares Core S&P 500 UCITS ETF", Type = AssetType.ETF });
        context.AssetListings.AddRange(
            new AssetListing
            {
                Id = _listingId,
                AssetId = 1,
                Ticker = "CSPX",
                ExchangeMic = "XLON",
                ExchangeName = "London Stock Exchange",
                TradingCurrency = "USD",
                PriceMultiplier = 1m,
                IsActive = true
            },
            new AssetListing
            {
                Id = _listingId + 1,
                AssetId = 1,
                Ticker = "CSPX",
                ExchangeMic = "XLON",
                ExchangeName = "London Stock Exchange",
                TradingCurrency = "GBX",
                PriceMultiplier = 0.01m,
                IsActive = true
            });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static void AssertDailyCapitalInvariant(
        IReadOnlyList<TimeSeriesModel> series,
        DateTime start,
        DateTime end,
        decimal openingCapital,
        DateOnly transactionDate,
        decimal transactionCost)
    {
        Assert.Equal((end - start).Days + 1, series.Count);
        var previousValue = openingCapital;
        for (var index = 0; index < series.Count; index++)
        {
            var date = start.AddDays(index);
            var expected = date < transactionDate.ToDateTime(TimeOnly.MinValue)
                ? openingCapital
                : openingCapital + transactionCost;

            Assert.Equal(date, series[index].DateTime.Date);
            Assert.True(series[index].Value >= previousValue);
            Assert.Equal(expected, series[index].Value);
            previousValue = series[index].Value;
        }
    }

    private List<(DateTime Date, decimal? Value)> CreateRates(Currency from, Currency to, DateTime start, DateTime end)
    {
        var rate = (from.ShortName, to.ShortName) switch
        {
            ("USD", "PLN") => 4m,
            ("GBP", "PLN") => 5m,
            _ => 1m
        };
        List<(DateTime Date, decimal? Value)> rates = [];
        for (var date = start.Date; date <= end.Date; date = date.AddDays(1))
            rates.Add((date, from.ShortName == "USD" && to.ShortName == "PLN" && date == new DateTime(2026, 1, 12)
                ? _january12UsdToPlnRate
                : rate));
        return rates;
    }

    public override void Dispose()
    {
        base.Dispose();
        _testDatabase?.Dispose();
    }

    private sealed record CapitalTrade(
        DateOnly TradeDate,
        decimal Quantity,
        decimal UnitPrice,
        decimal ExpectedRawUsd,
        decimal ExpectedCumulativeUsd,
        decimal ExpectedCumulativePln);
}