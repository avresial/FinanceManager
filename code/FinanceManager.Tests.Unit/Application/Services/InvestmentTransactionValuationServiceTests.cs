using FinanceManager.Application.FinancialAccounts.Investments.Valuation;
using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.Assets.Services;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Services;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Repositories;
using Moq;

namespace FinanceManager.Tests.Unit.Application.Services;

[Collection("Application")]
[Trait("Category", "Unit")]
public class InvestmentTransactionValuationServiceTests
{
    private const int _accountId = 5;
    private const long _listingId = 11;
    private static readonly AssetListing _listing = new() { Id = _listingId, Ticker = "CSPX" };

    private static InvestmentTransaction Buy(decimal quantity, decimal unitPrice, string currency, DateOnly tradeDate, long id = 1) => new()
    {
        Id = id,
        AccountId = _accountId,
        AssetListingId = _listingId,
        AssetListing = _listing,
        Type = InvestmentTransactionType.Buy,
        Quantity = quantity,
        UnitPrice = unitPrice,
        Currency = currency,
        TradeDate = tradeDate,
    };

    private static (Mock<IInvestmentTransactionRepository> Repo, Mock<IInvestmentPriceProvider> Price, Mock<ICurrencyExchangeService> Fx) Mocks(
        params InvestmentTransaction[] transactions)
    {
        var repo = new Mock<IInvestmentTransactionRepository>();
        repo.Setup(x => x.GetByAccount(_accountId, It.IsAny<CancellationToken>())).ReturnsAsync(transactions);
        return (repo, new Mock<IInvestmentPriceProvider>(), new Mock<ICurrencyExchangeService>());
    }

    private static InvestmentTransactionValuationService Build(
        Mock<IInvestmentTransactionRepository> repo, Mock<IInvestmentPriceProvider> price, Mock<ICurrencyExchangeService> fx) =>
        new(repo.Object, price.Object, fx.Object);

    [Fact]
    public async Task Converts_PurchaseAtTradeDateRate_And_CurrentAtLatestPrice()
    {
        var tradeDate = new DateOnly(2026, 1, 10);
        var (repo, price, fx) = Mocks(Buy(10m, 100m, "USD", tradeDate));

        // Purchase value converts at the trade-date rate; current price comes back already in PLN.
        fx.Setup(x => x.GetExchangeRateAsync(It.Is<Currency>(c => c.ShortName == "USD"), DefaultCurrency.PLN,
                tradeDate.ToDateTime(TimeOnly.MinValue)))
            .ReturnsAsync(4m);
        price.Setup(x => x.GetPricePerUnitAsync(_listingId, DefaultCurrency.PLN, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(500m);

        var result = Assert.Single(await Build(repo, price, fx).GetForAccountAsync(_accountId, DefaultCurrency.PLN, TestContext.Current.CancellationToken));

        Assert.True(result.IsConverted);
        Assert.Equal("PLN", result.Currency);
        Assert.True(result.HasCurrentPrice);
        Assert.Equal(400m, result.PurchaseUnitPrice); // 100 * 4
        Assert.Equal(4000m, result.PurchaseValue);   // 10 * 100 * 4
        Assert.Equal(500m, result.CurrentPrice);
        Assert.Equal(5000m, result.CurrentValuation); // 10 * 500
        Assert.Equal(1000m, result.GainLoss);
        Assert.Equal(25m, result.GainLossPercent);    // (5000/4000 - 1) * 100
    }

    [Fact]
    public async Task FallsBackToTransactionCurrency_WhenTradeDateRateMissing()
    {
        var tradeDate = new DateOnly(2026, 1, 10);
        var (repo, price, fx) = Mocks(Buy(2m, 50m, "EUR", tradeDate));

        fx.Setup(x => x.GetExchangeRateAsync(It.Is<Currency>(c => c.ShortName == "EUR"), DefaultCurrency.PLN, It.IsAny<DateTime>()))
            .ReturnsAsync((decimal?)null);
        // With no conversion, the current price is requested in the transaction currency instead.
        price.Setup(x => x.GetPricePerUnitAsync(_listingId, It.Is<Currency>(c => c.ShortName == "EUR"), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(60m);

        var result = Assert.Single(await Build(repo, price, fx).GetForAccountAsync(_accountId, DefaultCurrency.PLN, TestContext.Current.CancellationToken));

        Assert.False(result.IsConverted);
        Assert.Equal("EUR", result.Currency);
        Assert.Equal(50m, result.PurchaseUnitPrice);  // 50, unconverted
        Assert.Equal(100m, result.PurchaseValue);     // 2 * 50, unconverted
        Assert.Equal(120m, result.CurrentValuation);  // 2 * 60
        Assert.Equal(20m, result.GainLoss);
        Assert.Equal(20m, result.GainLossPercent);
    }

    [Fact]
    public async Task MissingCurrentPrice_IsFlagged_WithoutMisleadingLoss()
    {
        var (repo, price, fx) = Mocks(Buy(3m, 100m, "USD", new DateOnly(2026, 1, 10)));
        price.Setup(x => x.GetPricePerUnitAsync(_listingId, It.IsAny<Currency>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0m);

        var result = Assert.Single(await Build(repo, price, fx).GetForAccountAsync(_accountId, DefaultCurrency.USD, TestContext.Current.CancellationToken));

        Assert.False(result.HasCurrentPrice);
        Assert.Equal(300m, result.PurchaseValue);
        Assert.Equal(0m, result.CurrentValuation);
        Assert.Equal(0m, result.GainLoss);
        Assert.Equal(0m, result.GainLossPercent);
    }

    [Fact]
    public async Task SameCurrencyAsTarget_SkipsExchangeLookup()
    {
        var (repo, price, fx) = Mocks(Buy(4m, 25m, "USD", new DateOnly(2026, 1, 10)));
        price.Setup(x => x.GetPricePerUnitAsync(_listingId, DefaultCurrency.USD, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(30m);

        var result = Assert.Single(await Build(repo, price, fx).GetForAccountAsync(_accountId, DefaultCurrency.USD, TestContext.Current.CancellationToken));

        Assert.True(result.IsConverted);
        Assert.Equal("USD", result.Currency);
        Assert.Equal(100m, result.PurchaseValue);
        Assert.Equal(120m, result.CurrentValuation);
        fx.Verify(x => x.GetExchangeRateAsync(It.IsAny<Currency>(), It.IsAny<Currency>(), It.IsAny<DateTime>()), Times.Never);
    }

    [Fact]
    public async Task OmitsSellTransactions()
    {
        var tradeDate = new DateOnly(2026, 1, 10);
        var sell = Buy(1m, 100m, "USD", tradeDate, id: 2);
        sell.Type = InvestmentTransactionType.Sell;
        var (repo, price, fx) = Mocks(Buy(1m, 100m, "USD", tradeDate, id: 1), sell);
        price.Setup(x => x.GetPricePerUnitAsync(_listingId, DefaultCurrency.USD, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(110m);

        var results = await Build(repo, price, fx).GetForAccountAsync(_accountId, DefaultCurrency.USD, TestContext.Current.CancellationToken);

        Assert.Equal(1L, Assert.Single(results).TransactionId);
    }

    [Fact]
    public async Task MemoisesPriceAndRateLookups_AcrossTradesInSameListingAndDate()
    {
        var tradeDate = new DateOnly(2026, 1, 10);
        var (repo, price, fx) = Mocks(
            Buy(1m, 100m, "USD", tradeDate, id: 1),
            Buy(2m, 100m, "USD", tradeDate, id: 2));
        fx.Setup(x => x.GetExchangeRateAsync(It.Is<Currency>(c => c.ShortName == "USD"), DefaultCurrency.PLN, It.IsAny<DateTime>()))
            .ReturnsAsync(4m);
        price.Setup(x => x.GetPricePerUnitAsync(_listingId, DefaultCurrency.PLN, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(500m);

        var results = await Build(repo, price, fx).GetForAccountAsync(_accountId, DefaultCurrency.PLN, TestContext.Current.CancellationToken);

        Assert.Equal(2, results.Count);
        price.Verify(x => x.GetPricePerUnitAsync(_listingId, DefaultCurrency.PLN, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
        fx.Verify(x => x.GetExchangeRateAsync(It.Is<Currency>(c => c.ShortName == "USD"), DefaultCurrency.PLN, It.IsAny<DateTime>()), Times.Once);
    }
}