using FinanceManager.Application.FinancialAccounts.Investments.Valuation;
using FinanceManager.Domain.Assets.Services;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Repositories;
using Moq;

namespace FinanceManager.Tests.Unit.Application.Services.Investments;

[Trait("Category", "Unit")]
public class InvestmentValuationServiceTests
{
    private static readonly Currency _usd = new(1, "USD", "$");
    private const int _accountId = 7;

    private readonly Mock<IInvestmentTransactionRepository> _transactionRepository = new();
    private readonly Mock<IInvestmentPriceProvider> _priceProvider = new();

    private InvestmentValuationService CreateSut() => new(_transactionRepository.Object, _priceProvider.Object);

    private static InvestmentTransaction Tx(long listingId, InvestmentTransactionType type, decimal qty, DateOnly tradeDate) =>
        new()
        {
            AccountId = _accountId,
            AssetListingId = listingId,
            Type = type,
            Quantity = qty,
            UnitPrice = 1m,
            Currency = "USD",
            TradeDate = tradeDate
        };

    [Fact]
    public async Task GetHoldingsAsOf_OmitsZeroNetHoldings()
    {
        var asOf = new DateOnly(2024, 6, 30);
        _transactionRepository
            .Setup(x => x.GetHoldingsAsOf(It.Is<IReadOnlyCollection<int>>(a => a.Contains(_accountId)), asOf, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<long, decimal> { [10] = 5m, [20] = 0m });

        var holdings = await CreateSut().GetHoldingsAsOfAsync(_accountId, asOf, TestContext.Current.CancellationToken);

        Assert.Equal(5m, Assert.Contains(10L, holdings));
        Assert.DoesNotContain(20L, holdings);
    }

    [Fact]
    public async Task GetAccountValue_SumsHoldingsTimesPrice_AcrossListings()
    {
        var asOf = new DateTime(2024, 6, 30);
        _transactionRepository
            .Setup(x => x.GetHoldingsAsOf(It.IsAny<IReadOnlyCollection<int>>(), DateOnly.FromDateTime(asOf), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<long, decimal> { [10] = 3m, [20] = 2m });
        _priceProvider.Setup(x => x.GetPricePerUnitAsync(10, _usd, asOf, It.IsAny<CancellationToken>())).ReturnsAsync(100m);
        _priceProvider.Setup(x => x.GetPricePerUnitAsync(20, _usd, asOf, It.IsAny<CancellationToken>())).ReturnsAsync(50m);

        var value = await CreateSut().GetAccountValueAsync(_accountId, _usd, asOf, TestContext.Current.CancellationToken);

        Assert.Equal(3m * 100m + 2m * 50m, value); // 400
    }

    [Fact]
    public async Task GetAccountValue_ReturnsZero_WhenAccountHoldsNothing()
    {
        var asOf = new DateTime(2024, 6, 30);
        _transactionRepository
            .Setup(x => x.GetHoldingsAsOf(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<long, decimal>());

        var value = await CreateSut().GetAccountValueAsync(_accountId, _usd, asOf, TestContext.Current.CancellationToken);

        Assert.Equal(0m, value);
        _priceProvider.Verify(
            x => x.GetPricePerUnitAsync(It.IsAny<long>(), It.IsAny<Currency>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetAccountValue_NetsBuyAndSell()
    {
        var asOf = new DateTime(2024, 6, 30);
        _transactionRepository
            .Setup(x => x.GetHoldingsAsOf(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<long, decimal> { [10] = 4m }); // e.g. bought 6, sold 2
        _priceProvider.Setup(x => x.GetPricePerUnitAsync(10, _usd, asOf, It.IsAny<CancellationToken>())).ReturnsAsync(25m);

        var value = await CreateSut().GetAccountValueAsync(_accountId, _usd, asOf, TestContext.Current.CancellationToken);

        Assert.Equal(100m, value);
    }

    [Fact]
    public async Task GetAccountValueSeries_CarriesHoldingsForward_AndPricesPerDay()
    {
        var start = new DateTime(2024, 1, 1); // Monday
        var end = new DateTime(2024, 1, 4);

        // Buy 2 on Jan 1, buy 1 more on Jan 3 → holding is 2 on Jan 1-2, then 3 on Jan 3-4.
        _transactionRepository
            .Setup(x => x.GetByAccount(_accountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<InvestmentTransaction>
            {
                Tx(10, InvestmentTransactionType.Buy, 2m, new DateOnly(2024, 1, 1)),
                Tx(10, InvestmentTransactionType.Buy, 1m, new DateOnly(2024, 1, 3))
            });

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

        var series = await CreateSut().GetAccountValueSeriesAsync(_accountId, _usd, start, end, TestContext.Current.CancellationToken);

        Assert.Equal(20m, series[new DateTime(2024, 1, 1)]); // 2 * 10
        Assert.Equal(20m, series[new DateTime(2024, 1, 2)]); // holding carried forward
        Assert.Equal(30m, series[new DateTime(2024, 1, 3)]); // 3 * 10
        Assert.Equal(30m, series[new DateTime(2024, 1, 4)]);
    }

    [Fact]
    public async Task GetAccountValueSeries_SeedsHoldingsFromBeforeWindow()
    {
        var start = new DateTime(2024, 2, 1);
        var end = new DateTime(2024, 2, 2);

        // The only purchase happened before the window; the holding must be carried into it.
        _transactionRepository
            .Setup(x => x.GetByAccount(_accountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<InvestmentTransaction>
            {
                Tx(10, InvestmentTransactionType.Buy, 5m, new DateOnly(2024, 1, 15))
            });
        _priceProvider
            .Setup(x => x.GetPricePerUnitSeriesAsync(10, _usd, start, end, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<DateTime, decimal>
            {
                [new DateTime(2024, 2, 1)] = 8m,
                [new DateTime(2024, 2, 2)] = 9m
            });

        var series = await CreateSut().GetAccountValueSeriesAsync(_accountId, _usd, start, end, TestContext.Current.CancellationToken);

        Assert.Equal(40m, series[new DateTime(2024, 2, 1)]); // 5 * 8
        Assert.Equal(45m, series[new DateTime(2024, 2, 2)]); // 5 * 9
    }

    [Fact]
    public async Task GetAccountValueSeries_ReturnsEmpty_WhenNoTransactions()
    {
        _transactionRepository
            .Setup(x => x.GetByAccount(_accountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<InvestmentTransaction>());

        var series = await CreateSut().GetAccountValueSeriesAsync(
            _accountId, _usd, new DateTime(2024, 1, 1), new DateTime(2024, 1, 31), TestContext.Current.CancellationToken);

        Assert.Empty(series);
    }
}