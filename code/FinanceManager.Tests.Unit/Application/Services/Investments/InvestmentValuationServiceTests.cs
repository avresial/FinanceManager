using FinanceManager.Application.Portfolio.Valuation;
using FinanceManager.Domain.Assets.Services;
using FinanceManager.Domain.Currencies.Entities;
using FinanceManager.Domain.Currencies.Services;
using FinanceManager.Domain.MoneyFlow.Services;
using FinanceManager.Domain.Transactions.Investments.Entities;
using FinanceManager.Domain.Transactions.Investments.Repositories;
using Moq;

namespace FinanceManager.Tests.Unit.Application.Services.Investments;

[Trait("Category", "Unit")]
public class InvestmentValuationServiceTests
{
    private static readonly Currency _usd = new(1, "USD", "$");
    private const int _accountId = 7;
    private const int _userId = 7;

    private readonly Mock<IInvestmentTransactionRepository> _transactionRepository = new();
    private readonly Mock<IInvestmentPriceProvider> _priceProvider = new();
    private readonly Mock<IInflationIndexProvider> _inflationIndexProvider = new();
    private readonly Mock<ICurrencyExchangeService> _currencyExchangeService = new();

    private InvestmentValuationService CreateSut() =>
        new(_transactionRepository.Object, _priceProvider.Object);

    private InvestmentCapitalSeriesService CreateCapitalSut() =>
        new(_transactionRepository.Object, _currencyExchangeService.Object);

    private InvestmentBenchmarkService CreateBenchmarkSut() =>
        new(_transactionRepository.Object, _priceProvider.Object, _inflationIndexProvider.Object);

    private static InvestmentTransaction Tx(
        long listingId,
        InvestmentTransactionType type,
        decimal qty,
        DateOnly tradeDate,
        int? accountId = _accountId,
        decimal unitPrice = 1m,
        decimal? fee = null,
        long id = 0,
        int userId = _userId) =>
        new()
        {
            EntryId = id,
            UserId = userId,
            AssetListingId = listingId,
            Type = type,
            Quantity = qty,
            UnitPrice = unitPrice,
            Currency = "USD",
            TradeDate = tradeDate,
            Fee = fee
        };

    [Fact]
    public async Task GetUserValueByListing_PricesPooledHoldingsPerListing_AndOmitsUnpricedAndClosedOnes()
    {
        var asOf = new DateTime(2024, 6, 30, 12, 0, 0);
        _transactionRepository
            .Setup(x => x.GetHoldingsByUserAsOf(7, DateOnly.FromDateTime(asOf), It.IsAny<CancellationToken>()))
            .ReturnsAsync([Holding(10, 5m), Holding(20, -2m), Holding(30, 4m)]);
        _priceProvider.Setup(x => x.GetPricePerUnitAsync(10, _usd, asOf, It.IsAny<CancellationToken>())).ReturnsAsync(3m);
        _priceProvider.Setup(x => x.GetPricePerUnitAsync(20, _usd, asOf, It.IsAny<CancellationToken>())).ReturnsAsync(10m);
        _priceProvider.Setup(x => x.GetPricePerUnitAsync(30, _usd, asOf, It.IsAny<CancellationToken>())).ReturnsAsync(0m);

        var values = await CreateSut().GetUserValueByListingAsync(7, _usd, asOf, TestContext.Current.CancellationToken);

        Assert.Equal(new Dictionary<long, decimal> { [10] = 15m, [20] = -20m }, values);
    }

    [Fact]
    public async Task GetUserValueSeries_PoolsTradesAcrossAccountsAndUnlinkedTrades_WithAnExplicitClosingZero()
    {
        var start = new DateTime(2024, 1, 1);
        var end = new DateTime(2024, 1, 4);
        _transactionRepository
            .Setup(x => x.GetValuationInputsByUser(7, DateOnly.FromDateTime(start), DateOnly.FromDateTime(end), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IInvestmentTransactionRepository.UserValuationInputs(
                [new(10, 2m)],
                [new(10, new DateOnly(2024, 1, 2), 3m), new(10, new DateOnly(2024, 1, 4), -5m)]));
        _priceProvider
            .Setup(x => x.GetPricePerUnitSeriesAsync(10, _usd, start, end, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<DateTime, decimal>
            {
                [new DateTime(2024, 1, 1)] = 10m,
                [new DateTime(2024, 1, 2)] = 10m,
                [new DateTime(2024, 1, 3)] = 10m,
                [new DateTime(2024, 1, 4)] = 10m
            });

        var series = await CreateSut().GetUserValueSeriesAsync(7, _usd, start, end, TestContext.Current.CancellationToken);

        Assert.Equal([20m, 50m, 50m, 0m], series.OrderBy(x => x.Key).Select(x => x.Value));
    }

    [Fact]
    public async Task GetUserValueSeries_SkipsPositionsClosedBeforeTheWindow_AndReturnsEmptyWithoutTrades()
    {
        var start = new DateTime(2024, 2, 1);
        var end = new DateTime(2024, 2, 3);
        _transactionRepository
            .Setup(x => x.GetValuationInputsByUser(It.IsAny<long>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IInvestmentTransactionRepository.UserValuationInputs([], []));

        Assert.Empty(await CreateSut().GetUserValueSeriesAsync(-5, _usd, start, end, TestContext.Current.CancellationToken));
        _priceProvider.Verify(x => x.GetPricePerUnitSeriesAsync(It.IsAny<long>(), It.IsAny<Currency>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Empty(await CreateSut().GetUserValueSeriesAsync(7, _usd, end, start, TestContext.Current.CancellationToken));
    }

    private static FinanceManager.Domain.Transactions.Investments.Dtos.InvestmentHolding Holding(long listingId, decimal units) =>
        new(listingId, $"T{listingId}", "XLON", FinanceManager.Domain.Assets.Entities.AssetType.ETF, units);

    [Fact]
    public async Task GetUserValueSeries_CarriesHoldingsForward_AndPricesPerDay()
    {
        var start = new DateTime(2024, 1, 1); // Monday
        var end = new DateTime(2024, 1, 4);

        // Buy 2 on Jan 1, buy 1 more on Jan 3 → holding is 2 on Jan 1-2, then 3 on Jan 3-4.
        SetupTransactions(
            Tx(10, InvestmentTransactionType.Buy, 2m, new DateOnly(2024, 1, 1)),
            Tx(10, InvestmentTransactionType.Buy, 1m, new DateOnly(2024, 1, 3)));

        // Flat price of 10 every day in the window.
        _priceProvider
            .Setup(x => x.GetPricePerUnitSeriesAsync(10, _usd, start, end, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<DateTime, decimal>
            {
                [new DateTime(2024, 1, 1)] = 10m,
                [new DateTime(2024, 1, 2)] = 10m,
                [new DateTime(2024, 1, 3)] = 10m,
                [new DateTime(2024, 1, 4)] = 10m
            });

        var series = await CreateSut().GetUserValueSeriesAsync(_userId, _usd, start, end, TestContext.Current.CancellationToken);

        Assert.Equal(20m, series[new DateTime(2024, 1, 1)]); // 2 * 10
        Assert.Equal(20m, series[new DateTime(2024, 1, 2)]); // holding carried forward
        Assert.Equal(30m, series[new DateTime(2024, 1, 3)]); // 3 * 10
        Assert.Equal(30m, series[new DateTime(2024, 1, 4)]);
    }

    [Fact]
    public async Task GetUserValueSeries_SeedsHoldingsFromBeforeWindow()
    {
        var start = new DateTime(2024, 2, 1);
        var end = new DateTime(2024, 2, 2);

        // The only purchase happened before the window; the holding must be carried into it.
        SetupTransactions(
            Tx(10, InvestmentTransactionType.Buy, 5m, new DateOnly(2024, 1, 15)));
        _priceProvider
            .Setup(x => x.GetPricePerUnitSeriesAsync(10, _usd, start, end, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<DateTime, decimal>
            {
                [new DateTime(2024, 2, 1)] = 8m,
                [new DateTime(2024, 2, 2)] = 9m
            });

        var series = await CreateSut().GetUserValueSeriesAsync(_userId, _usd, start, end, TestContext.Current.CancellationToken);

        Assert.Equal(40m, series[new DateTime(2024, 2, 1)]); // 5 * 8
        Assert.Equal(45m, series[new DateTime(2024, 2, 2)]); // 5 * 9
    }

    [Fact]
    public async Task GetUserValueSeries_ReturnsEmpty_WhenNoTransactions()
    {
        SetupTransactions();

        var series = await CreateSut().GetUserValueSeriesAsync(
            _userId, _usd, new DateTime(2024, 1, 1), new DateTime(2024, 1, 31), TestContext.Current.CancellationToken);

        Assert.Empty(series);
    }

    [Fact]
    public async Task GetUserValueSeries_SkipsPriceHistory_ForPositionClosedBeforeValuationWindow()
    {
        var start = new DateTime(2024, 2, 1);
        var end = new DateTime(2024, 2, 2);

        // Position was bought and fully sold before the valuation window opened.
        SetupTransactions(
            Tx(10, InvestmentTransactionType.Buy, 5m, new DateOnly(2024, 1, 10)),
            Tx(10, InvestmentTransactionType.Sell, 5m, new DateOnly(2024, 1, 20)));

        var series = await CreateSut().GetUserValueSeriesAsync(_userId, _usd, start, end, TestContext.Current.CancellationToken);

        Assert.Empty(series);
        _priceProvider.Verify(
            x => x.GetPricePerUnitSeriesAsync(10, It.IsAny<Currency>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetUserValueSeries_OnlyFetchesPriceHistoryForActiveListings_WhenAccountHasClosedPosition()
    {
        var start = new DateTime(2024, 2, 1);
        var end = new DateTime(2024, 2, 2);

        // Listing 10 was fully closed before the window; Listing 20 is still held.
        SetupTransactions(
            Tx(10, InvestmentTransactionType.Buy, 5m, new DateOnly(2024, 1, 10)),
            Tx(10, InvestmentTransactionType.Sell, 5m, new DateOnly(2024, 1, 20)),
            Tx(20, InvestmentTransactionType.Buy, 2m, new DateOnly(2024, 1, 15)));

        _priceProvider
            .Setup(x => x.GetPricePerUnitSeriesAsync(20, _usd, start, end, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<DateTime, decimal>
            {
                [start] = 50m,
                [end] = 55m
            });

        var series = await CreateSut().GetUserValueSeriesAsync(_userId, _usd, start, end, TestContext.Current.CancellationToken);

        Assert.Equal(100m, series[start]); // 2 * 50
        Assert.Equal(110m, series[end]);   // 2 * 55

        _priceProvider.Verify(
            x => x.GetPricePerUnitSeriesAsync(10, It.IsAny<Currency>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _priceProvider.Verify(
            x => x.GetPricePerUnitSeriesAsync(20, _usd, start, end, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetUserValueSeries_PreservesInWindowOpenAndClose()
    {
        var start = new DateTime(2024, 2, 1);
        var end = new DateTime(2024, 2, 3);

        // Position opens on Day 2 and closes on Day 3 (both in-window). Opening holding is 0.
        SetupTransactions(
            Tx(10, InvestmentTransactionType.Buy, 3m, new DateOnly(2024, 2, 2)),
            Tx(10, InvestmentTransactionType.Sell, 3m, new DateOnly(2024, 2, 3)));

        _priceProvider
            .Setup(x => x.GetPricePerUnitSeriesAsync(10, _usd, start, end, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<DateTime, decimal>
            {
                [start] = 10m,
                [start.AddDays(1)] = 15m,
                [end] = 20m
            });

        var series = await CreateSut().GetUserValueSeriesAsync(_userId, _usd, start, end, TestContext.Current.CancellationToken);

        Assert.DoesNotContain(start, series.Keys);
        Assert.Equal(45m, series[start.AddDays(1)]); // 3 * 15
        Assert.Equal(0m, series[end]); // explicit closing zero after sell

        _priceProvider.Verify(
            x => x.GetPricePerUnitSeriesAsync(10, _usd, start, end, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetCapitalSeries_TracksRemainingCostBasis_CarriesOpeningCapital_AndCombinesSameDayFlows()
    {
        var start = new DateTime(2024, 1, 2);
        var end = new DateTime(2024, 1, 4);
        SetupTransactions(
            Tx(1, InvestmentTransactionType.Buy, 10m, new DateOnly(2024, 1, 1), unitPrice: 10m, fee: 2m),
            Tx(1, InvestmentTransactionType.Buy, 2m, new DateOnly(2024, 1, 2), unitPrice: 10m, fee: 1m),
            Tx(1, InvestmentTransactionType.Buy, 1m, new DateOnly(2024, 1, 2), unitPrice: 5m),
            Tx(1, InvestmentTransactionType.Sell, 1m, new DateOnly(2024, 1, 3), unitPrice: 15m, fee: 1m));

        var series = await CreateCapitalSut().GetCapitalSeriesAsync(
            _userId, _usd, start, end, TestContext.Current.CancellationToken);

        Assert.Equal(128m, series[start]);
        Assert.Equal(128m * 12m / 13m, series[start.AddDays(1)]);
        Assert.Equal(128m * 12m / 13m, series[end]);
        Assert.Equal(128m * 12m / 13m, series[start.AddDays(2)]);
    }

    [Fact]
    public async Task GetCapitalSeries_ConvertsEachCashFlowUsingOneHistoricalRateRange()
    {
        var start = new DateTime(2024, 1, 2);
        var end = new DateTime(2024, 1, 3);
        var eur = new Currency(2, "EUR", "€");
        var transactions = new[]
        {
            Tx(1, InvestmentTransactionType.Buy, 2m, new DateOnly(2024, 1, 2), unitPrice: 100m),
            Tx(1, InvestmentTransactionType.Sell, 1m, new DateOnly(2024, 1, 3), unitPrice: 50m)
        };
        foreach (var transaction in transactions)
            transaction.Currency = eur.ShortName;
        SetupTransactions(transactions);

        _currencyExchangeService
            .Setup(x => x.GetExchangeRateAsync(
                It.Is<Currency>(currency => currency.ShortName == "EUR"),
                It.Is<Currency>(currency => currency.ShortName == "PLN"),
                start,
                end))
            .ReturnsAsync([
                (start, (decimal?)2m),
                (end, (decimal?)3m)
            ]);

        var series = await CreateCapitalSut().GetCapitalSeriesAsync(
            _userId, DefaultCurrency.PLN, start, end, TestContext.Current.CancellationToken);

        Assert.Equal(400m, series[start]);
        Assert.Equal(200m, series[end]);
        _currencyExchangeService.Verify(
            x => x.GetExchangeRateAsync(
                It.Is<Currency>(currency => currency.ShortName == "EUR"),
                It.Is<Currency>(currency => currency.ShortName == "PLN"),
                start,
                end),
            Times.Once);
    }

    [Theory]
    [InlineData("GBX", "GBP")]
    [InlineData("GBP.", "GBP")]
    [InlineData("ZAC", "ZAR")]
    [InlineData("ILA", "ILS")]
    public async Task GetCapitalSeries_NormalizesMinorTransactionCurrencyWithoutListingMultiplier(string minorCurrency, string majorCurrency)
    {
        var date = new DateTime(2024, 1, 1);
        _transactionRepository.Setup(x => x.GetCapitalFlowInputsByUser(
                _userId, It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new IInvestmentTransactionRepository.UserCapitalFlowInput(
                1, 1, DateOnly.FromDateTime(date), InvestmentTransactionType.Buy,
                2m, 100m, null, minorCurrency)]);

        var series = await CreateCapitalSut().GetCapitalSeriesAsync(
            _userId, new Currency(0, majorCurrency, majorCurrency), date, date, TestContext.Current.CancellationToken);

        Assert.Equal(2m, series[date]);
    }

    [Fact]
    public async Task GetCapitalSeries_SellRemovesHistoricalCostBasis_NotSaleProceedsOrFees()
    {
        var start = new DateTime(2024, 1, 1);
        var end = new DateTime(2024, 1, 2);
        SetupTransactions(
            Tx(1, InvestmentTransactionType.Buy, 10m, new DateOnly(2024, 1, 1), unitPrice: 100m, id: 1),
            Tx(1, InvestmentTransactionType.Sell, 2m, new DateOnly(2024, 1, 2), unitPrice: 1_000m, fee: 50m, id: 2));

        var series = await CreateCapitalSut().GetCapitalSeriesAsync(
            _userId, _usd, start, end, TestContext.Current.CancellationToken);

        Assert.Equal(1_000m, series[start]);
        Assert.Equal(800m, series[end]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GetCapitalSeries_IgnoresNonPositiveBuyQuantities(decimal quantity)
    {
        var date = new DateTime(2024, 1, 1);
        SetupTransactions(Tx(1, InvestmentTransactionType.Buy, quantity, DateOnly.FromDateTime(date), unitPrice: 100m, fee: 10m));

        var series = await CreateCapitalSut().GetCapitalSeriesAsync(
            _userId, _usd, date, date, TestContext.Current.CancellationToken);

        Assert.Equal(0m, series[date]);
    }

    [Fact]
    public async Task GetCapitalSeries_ReplaysEditedAndDeletedHistory()
    {
        var start = new DateTime(2024, 1, 1);
        var end = new DateTime(2024, 1, 2);
        var firstBuy = Tx(1, InvestmentTransactionType.Buy, 10m, new DateOnly(2024, 1, 1), unitPrice: 100m, id: 1);
        var secondBuy = Tx(1, InvestmentTransactionType.Buy, 5m, new DateOnly(2024, 1, 2), unitPrice: 100m, id: 2);
        var transactions = new List<InvestmentTransaction> { firstBuy, secondBuy };
        SetupCapitalFlowInputs(transactions);

        var initial = await CreateCapitalSut().GetCapitalSeriesAsync(
            _userId, _usd, start, end, TestContext.Current.CancellationToken);

        firstBuy.UnitPrice = 200m;
        transactions.Remove(secondBuy);
        var rebuilt = await CreateCapitalSut().GetCapitalSeriesAsync(
            _userId, _usd, start, end, TestContext.Current.CancellationToken);

        Assert.Equal(1_000m, initial[start]);
        Assert.Equal(1_500m, initial[end]);
        Assert.Equal(2_000m, rebuilt[start]);
        Assert.Equal(2_000m, rebuilt[end]);
    }

    [Fact]
    public async Task GetCapitalSeries_ReplaysSameDayHistoryByTransactionId()
    {
        var date = new DateOnly(2024, 1, 2);
        SetupTransactions(
            Tx(1, InvestmentTransactionType.Sell, 2m, date, unitPrice: 1_000m, id: 10),
            Tx(1, InvestmentTransactionType.Buy, 10m, date, unitPrice: 100m, id: 20));

        var series = await CreateCapitalSut().GetCapitalSeriesAsync(
            _userId, _usd, date.ToDateTime(TimeOnly.MinValue), date.ToDateTime(TimeOnly.MinValue), TestContext.Current.CancellationToken);

        Assert.Equal(1_000m, series[date.ToDateTime(TimeOnly.MinValue)]);
    }

    [Fact]
    public async Task GetCapitalSeries_PoolsOneCostBasisPerListing_AcrossAccountsAndUnlinkedTrades()
    {
        var start = new DateTime(2024, 1, 1);
        var end = new DateTime(2024, 1, 4);
        // 10 @ 100 in account 7, 10 @ 200 in account 8 and 20 @ 300 unlinked pool into 40 units costing 9000;
        // selling 10 from account 7 then removes the pooled average (225 each), not account 7's own cost.
        SetupTransactions(
            Tx(1, InvestmentTransactionType.Buy, 10m, new DateOnly(2024, 1, 1), accountId: 7, unitPrice: 100m, id: 1),
            Tx(1, InvestmentTransactionType.Buy, 10m, new DateOnly(2024, 1, 2), accountId: 8, unitPrice: 200m, id: 2),
            Tx(1, InvestmentTransactionType.Buy, 20m, new DateOnly(2024, 1, 3), accountId: null, unitPrice: 300m, id: 3),
            Tx(1, InvestmentTransactionType.Sell, 10m, new DateOnly(2024, 1, 4), accountId: 7, unitPrice: 999m, id: 4),
            Tx(1, InvestmentTransactionType.Buy, 99m, new DateOnly(2024, 1, 1), accountId: 7, unitPrice: 1m, id: 5, userId: 999));

        var series = await CreateCapitalSut().GetCapitalSeriesAsync(
            _userId, _usd, start, end, TestContext.Current.CancellationToken);

        Assert.Equal(1_000m, series[start]);
        Assert.Equal(3_000m, series[start.AddDays(1)]);
        Assert.Equal(9_000m, series[start.AddDays(2)]);
        Assert.Equal(6_750m, series[end]);
    }

    [Fact]
    public async Task GetCapitalSeries_ForAGuestUserWithANegativeId_ReplaysTheirTrades_AndNothingForUserZero()
    {
        var date = new DateTime(2024, 1, 1);
        SetupTransactions(Tx(1, InvestmentTransactionType.Buy, 3m, new DateOnly(2024, 1, 1), unitPrice: 10m, userId: -3));

        var guest = await CreateCapitalSut().GetCapitalSeriesAsync(-3, _usd, date, date, TestContext.Current.CancellationToken);
        var none = await CreateCapitalSut().GetCapitalSeriesAsync(0, _usd, date, date, TestContext.Current.CancellationToken);

        Assert.Equal(30m, guest[date]);
        Assert.Empty(none);
        _transactionRepository.Verify(x => x.GetCapitalFlowInputsByUser(0, It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetCapitalSeries_IgnoresUnknownFutureTransactionTypes()
    {
        var transactions = new[]
        {
            Tx(1, (InvestmentTransactionType)999, 5m, new DateOnly(2024, 1, 1), unitPrice: 100m)
        };
        SetupTransactions(transactions);

        var series = await CreateCapitalSut().GetCapitalSeriesAsync(
            _userId, _usd, new DateTime(2024, 1, 1), new DateTime(2024, 1, 2), TestContext.Current.CancellationToken);

        Assert.Empty(series);
        _currencyExchangeService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetBenchmarkSeries_DefaultsToInflation_AndOpensAtTheCarriedInHolding()
    {
        var start = new DateTime(2024, 1, 1);
        var end = new DateTime(2024, 3, 1);
        // 10 units bought before the range opens, worth 100 each on the first day.
        SetupTransactions(Tx(1, InvestmentTransactionType.Buy, 10m, new DateOnly(2023, 12, 1)));
        SetupPrices(1, new Dictionary<DateTime, decimal> { [start] = 100m, [end] = 130m });
        _inflationIndexProvider
            .Setup(x => x.GetIndexSeriesAsync(start, end, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<DateTime, decimal>
            {
                [start] = 100m,
                [end] = 105m
            });

        var series = await CreateBenchmarkSut().GetBenchmarkSeriesAsync(
            null, _userId, _usd, start, end, TestContext.Current.CancellationToken);

        Assert.Equal(1_000m, series[start]);
        Assert.Equal(1_050m, series[end]);
    }

    [Fact]
    public async Task GetBenchmarkSeries_AbsorbsContributionsMadeDuringTheRange()
    {
        var start = new DateTime(2024, 1, 1);
        var mid = new DateTime(2024, 1, 2);
        var end = new DateTime(2024, 1, 3);
        // Nothing carried in; one 500 purchase on the middle day.
        SetupTransactions(Tx(1, InvestmentTransactionType.Buy, 5m, new DateOnly(2024, 1, 2)));
        SetupPrices(1, new Dictionary<DateTime, decimal> { [start] = 100m, [mid] = 100m, [end] = 100m });
        _inflationIndexProvider
            .Setup(x => x.GetIndexSeriesAsync(start, end, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<DateTime, decimal>
            {
                [start] = 100m,
                [mid] = 100m,
                [end] = 110m
            });

        var series = await CreateBenchmarkSut().GetBenchmarkSeriesAsync(
            null, _userId, _usd, start, end, TestContext.Current.CancellationToken);

        // Before the purchase the shadow portfolio holds nothing, so the day is omitted entirely.
        Assert.DoesNotContain(start, series.Keys);
        Assert.Equal(500m, series[mid]);
        // The contribution rides the index from its own trade date: 500 * 110/100.
        Assert.Equal(550m, series[end]);
    }

    [Fact]
    public async Task GetBenchmarkSeries_TracksMoneyWithdrawnByASell()
    {
        var start = new DateTime(2024, 1, 1);
        var end = new DateTime(2024, 1, 2);
        SetupTransactions(
            Tx(1, InvestmentTransactionType.Buy, 10m, new DateOnly(2023, 12, 1)),
            Tx(1, InvestmentTransactionType.Sell, 4m, new DateOnly(2024, 1, 2)));
        SetupPrices(1, new Dictionary<DateTime, decimal> { [start] = 100m, [end] = 100m });
        _inflationIndexProvider
            .Setup(x => x.GetIndexSeriesAsync(start, end, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<DateTime, decimal> { [start] = 100m, [end] = 100m });

        var series = await CreateBenchmarkSut().GetBenchmarkSeriesAsync(
            null, _userId, _usd, start, end, TestContext.Current.CancellationToken);

        Assert.Equal(1_000m, series[start]);
        Assert.Equal(600m, series[end]); // 400 of the 1,000 withdrawn on the sell date
    }

    [Fact]
    public async Task GetBenchmarkSeries_CarriesTheLastPublishedIndexLevelAcrossGaps()
    {
        var start = new DateTime(2024, 1, 1);
        var end = new DateTime(2024, 1, 3);
        SetupTransactions(Tx(1, InvestmentTransactionType.Buy, 10m, new DateOnly(2023, 12, 1)));
        SetupPrices(1, new Dictionary<DateTime, decimal> { [start] = 100m });
        // Inflation only publishes monthly, so the range's later days have no point of their own.
        _inflationIndexProvider
            .Setup(x => x.GetIndexSeriesAsync(start, end, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<DateTime, decimal> { [start] = 100m });

        var series = await CreateBenchmarkSut().GetBenchmarkSeriesAsync(
            null, _userId, _usd, start, end, TestContext.Current.CancellationToken);

        Assert.Equal(3, series.Count);
        Assert.Equal(1_000m, series[end]);
    }

    [Fact]
    public async Task GetBenchmarkSeries_UsesSelectedAvailableInvestment()
    {
        var start = new DateTime(2024, 1, 1);
        var end = new DateTime(2024, 1, 2);
        SetupTransactions(Tx(1, InvestmentTransactionType.Buy, 10m, new DateOnly(2023, 12, 1)));
        SetupPrices(1, new Dictionary<DateTime, decimal> { [start] = 100m, [end] = 100m });
        SetupPrices(42, new Dictionary<DateTime, decimal> { [start] = 50m, [end] = 60m });

        var series = await CreateBenchmarkSut().GetBenchmarkSeriesAsync(
            42, _userId, _usd, start, end, TestContext.Current.CancellationToken);

        Assert.Equal(1_000m, series[start]);
        Assert.Equal(1_200m, series[end]); // 1,000 * 60/50
        _inflationIndexProvider.Verify(
            x => x.GetIndexSeriesAsync(
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetBenchmarkSeries_BenchmarkingAgainstAHeldInstrument_PricesItOnce()
    {
        var start = new DateTime(2024, 1, 1);
        var end = new DateTime(2024, 1, 2);
        SetupTransactions(Tx(42, InvestmentTransactionType.Buy, 10m, new DateOnly(2023, 12, 1)));
        SetupPrices(42, new Dictionary<DateTime, decimal> { [start] = 50m, [end] = 60m });

        var series = await CreateBenchmarkSut().GetBenchmarkSeriesAsync(
            42, _userId, _usd, start, end, TestContext.Current.CancellationToken);

        Assert.Equal(500m, series[start]);
        Assert.Equal(600m, series[end]);
        _priceProvider.Verify(
            x => x.GetPricePerUnitSeriesAsync(42, _usd, start, end, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetBenchmarkSeries_ForAUserWithoutTransactions_IsEmpty()
    {
        var start = new DateTime(2024, 1, 1);
        var end = new DateTime(2024, 1, 2);
        SetupTransactions();

        var series = await CreateBenchmarkSut().GetBenchmarkSeriesAsync(
            null, _userId, _usd, start, end, TestContext.Current.CancellationToken);

        Assert.Empty(series);
    }

    private void SetupTransactions(params InvestmentTransaction[] transactions)
    {
        _transactionRepository
            .Setup(x => x.GetValuationInputsByUser(It.IsAny<long>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((long userId, DateOnly startDate, DateOnly endDate, CancellationToken _) =>
            {
                var owned = transactions.Where(t => t.UserId == userId).ToList();
                var opening = owned
                    .Where(t => t.TradeDate < startDate)
                    .GroupBy(t => t.AssetListingId)
                    .Select(g => new IInvestmentTransactionRepository.UserValuationOpeningPosition(g.Key, g.Sum(t => t.SignedQuantity)))
                    .Where(p => p.Quantity != 0m)
                    .ToList();

                var trades = owned
                    .Where(t => t.TradeDate >= startDate && t.TradeDate <= endDate)
                    .GroupBy(t => (t.AssetListingId, t.TradeDate))
                    .Select(g => new IInvestmentTransactionRepository.UserValuationTradeInput(g.Key.AssetListingId, g.Key.TradeDate, g.Sum(t => t.SignedQuantity)))
                    .Where(t => t.SignedQuantity != 0m)
                    .ToList();

                return new IInvestmentTransactionRepository.UserValuationInputs(opening, trades);
            });

        SetupCapitalFlowInputs(transactions);
    }

    private void SetupCapitalFlowInputs(IReadOnlyList<InvestmentTransaction> transactions) =>
        _transactionRepository
            .Setup(x => x.GetCapitalFlowInputsByUser(It.IsAny<long>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((long userId, DateOnly toDate, CancellationToken _) =>
                transactions
                    .Where(t => t.UserId == userId && t.TradeDate <= toDate)
                    .OrderBy(t => t.TradeDate).ThenBy(t => t.EntryId)
                    .Select(t => new IInvestmentTransactionRepository.UserCapitalFlowInput(
                        t.EntryId,
                        t.AssetListingId,
                        t.TradeDate,
                        t.Type,
                        t.Quantity,
                        t.UnitPrice,
                        t.Fee,
                        t.Currency))
                    .ToList());

    private void SetupPrices(long listingId, Dictionary<DateTime, decimal> prices) =>
        _priceProvider
            .Setup(x => x.GetPricePerUnitSeriesAsync(
                listingId, _usd, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(prices);
}