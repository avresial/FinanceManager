using FinanceManager.Application.FinancialAccounts.Investments.FeeDrag;
using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.Assets.Services;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Repositories;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
using Moq;

namespace FinanceManager.Tests.Unit.Application.Services.Investments;

[Trait("Category", "Unit")]
public class FeeDragServiceTests
{
    private readonly Mock<IAccountRepository<InvestmentAccount>> _accountRepository = new();
    private readonly Mock<IInvestmentTransactionRepository> _transactionRepository = new();
    private readonly Mock<IInvestmentPriceProvider> _priceProvider = new();

    [Fact]
    public async Task GetAnalysis_AggregatesEtfHoldingsAndReportsMissingExpenseRatios()
    {
        var knownListing = CreateListing(101, "KNOWN", 0.005m, AssetType.ETF);
        var missingListing = CreateListing(102, "MISSING", null, AssetType.ETF);
        var stockListing = CreateListing(103, "STOCK", 0.002m, AssetType.Stock);
        var accountOne = new InvestmentAccount(1, 10, "One");
        var accountTwo = new InvestmentAccount(1, 11, "Two");
        _accountRepository.Setup(repository => repository.GetAll(1)).ReturnsAsync([accountOne, accountTwo]);

        _transactionRepository.Setup(repository => repository.GetByAccount(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                CreateTransaction(10, knownListing, InvestmentTransactionType.Buy, 2m),
                CreateTransaction(10, knownListing, InvestmentTransactionType.Sell, 0.5m),
                CreateTransaction(10, missingListing, InvestmentTransactionType.Buy, 4m),
                CreateTransaction(10, stockListing, InvestmentTransactionType.Buy, 10m),
            ]);
        _transactionRepository.Setup(repository => repository.GetByAccount(11, It.IsAny<CancellationToken>()))
            .ReturnsAsync([CreateTransaction(11, knownListing, InvestmentTransactionType.Buy, 1m)]);
        _priceProvider.Setup(provider => provider.GetPricePerUnitAsync(101, It.IsAny<Currency>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(100m);
        _priceProvider.Setup(provider => provider.GetPricePerUnitAsync(102, It.IsAny<Currency>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(50m);

        var result = await CreateService().GetAnalysisAsync(1, DefaultCurrency.PLN, new DateTime(2026, 9, 15), 0.07m, TestContext.Current.CancellationToken);

        Assert.Equal(450m, result.TotalHoldingsValue);
        Assert.Equal(2, result.TotalHoldingsCount);
        Assert.Equal(1, result.MissingTerCount);
        Assert.Equal(200m, result.MissingTerHoldingsValue);
        Assert.Equal(1.25m, result.AnnualFeeCost);
        Assert.Equal(0.005m, result.WeightedExpenseRatio);
        Assert.Equal(2, result.Holdings.Count);
        Assert.Equal(250m, result.Holdings.Single(holding => holding.ListingId == 101).Value);
        Assert.Null(result.Holdings.Single(holding => holding.ListingId == 102).ExpenseRatio);
        Assert.Equal([10, 20, 30], result.Projections.Select(projection => projection.Years).ToArray());
        Assert.True(result.Projections[2].CumulativeFeeCost > result.Projections[0].CumulativeFeeCost);
    }

    [Fact]
    public async Task GetAnalysis_SkipsZeroHoldingsAndHoldingsWithoutPrices()
    {
        var zeroListing = CreateListing(201, "ZERO", 0.004m, AssetType.ETF);
        var noPriceListing = CreateListing(202, "NO-PRICE", 0.004m, AssetType.ETF);
        var account = new InvestmentAccount(1, 20, "Portfolio");
        _accountRepository.Setup(repository => repository.GetAll(1)).ReturnsAsync([account]);
        _transactionRepository.Setup(repository => repository.GetByAccount(20, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                CreateTransaction(20, zeroListing, InvestmentTransactionType.Buy, 2m),
                CreateTransaction(20, zeroListing, InvestmentTransactionType.Sell, 2m),
                CreateTransaction(20, noPriceListing, InvestmentTransactionType.Buy, 3m),
            ]);
        _priceProvider.Setup(provider => provider.GetPricePerUnitAsync(202, It.IsAny<Currency>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(0m);

        var result = await CreateService().GetAnalysisAsync(1, DefaultCurrency.USD, DateTime.UtcNow, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(0m, result.TotalHoldingsValue);
        Assert.Empty(result.Holdings);
        Assert.Equal(0, result.TotalHoldingsCount);
        _priceProvider.Verify(provider => provider.GetPricePerUnitAsync(201, It.IsAny<Currency>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetAnalysis_UsesRequestedCurrencyForEachPriceLookup()
    {
        var listing = CreateListing(301, "EUR-ETF", 0.001m, AssetType.ETF);
        var account = new InvestmentAccount(1, 30, "Portfolio");
        _accountRepository.Setup(repository => repository.GetAll(1)).ReturnsAsync([account]);
        _transactionRepository.Setup(repository => repository.GetByAccount(30, It.IsAny<CancellationToken>()))
            .ReturnsAsync([CreateTransaction(30, listing, InvestmentTransactionType.Buy, 2m)]);
        _priceProvider.Setup(provider => provider.GetPricePerUnitAsync(301, DefaultCurrency.USD, It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(75m);

        var result = await CreateService().GetAnalysisAsync(1, DefaultCurrency.USD, DateTime.UtcNow, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(150m, result.TotalHoldingsValue);
        _priceProvider.Verify(provider => provider.GetPricePerUnitAsync(301, DefaultCurrency.USD, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAnalysis_RejectsReturnRateOutsideSupportedRange()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            CreateService().GetAnalysisAsync(1, DefaultCurrency.PLN, DateTime.UtcNow, 0.1001m, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            CreateService().GetAnalysisAsync(1, DefaultCurrency.PLN, DateTime.UtcNow, -0.1001m, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetAnalysis_WithNoAccountsReturnsSafeEmptyProjection()
    {
        _accountRepository.Setup(repository => repository.GetAll(1)).ReturnsAsync([]);

        var result = await CreateService().GetAnalysisAsync(1, DefaultCurrency.PLN, DateTime.UtcNow, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Empty(result.Holdings);
        Assert.Equal(0m, result.AnnualFeeCost);
        Assert.Equal(3, result.Projections.Count);
        Assert.All(result.Projections, projection => Assert.Equal(0m, projection.CumulativeFeeCost));
    }

    private FeeDragService CreateService() => new(_accountRepository.Object, _transactionRepository.Object, _priceProvider.Object);

    private static AssetListing CreateListing(long id, string ticker, decimal? expenseRatio, AssetType type) => new()
    {
        Id = id,
        Ticker = ticker,
        Asset = new Asset { Id = id, Name = $"{ticker} ETF", Type = type, TotalExpenseRatio = expenseRatio },
    };

    private static InvestmentTransaction CreateTransaction(
        int accountId,
        AssetListing listing,
        InvestmentTransactionType type,
        decimal quantity) => new()
        {
            AccountId = accountId,
            AssetListingId = listing.Id,
            AssetListing = listing,
            Type = type,
            Quantity = quantity,
            TradeDate = new DateOnly(2026, 9, 1),
            Currency = listing.TradingCurrency,
        };
}