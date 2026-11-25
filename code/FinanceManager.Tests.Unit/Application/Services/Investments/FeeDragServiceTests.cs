using FinanceManager.Application.Portfolio.FeeDrag;
using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.Assets.Repositories;
using FinanceManager.Domain.Assets.Services;
using FinanceManager.Domain.Currencies.Entities;
using FinanceManager.Domain.Portfolio.FeeDrag.Dtos;
using FinanceManager.Domain.Transactions.Investments.Dtos;
using FinanceManager.Domain.Transactions.Investments.Repositories;
using Moq;

namespace FinanceManager.Tests.Unit.Application.Services.Investments;

[Trait("Category", "Unit")]
public class FeeDragServiceTests
{
    private readonly Mock<IInvestmentTransactionRepository> _transactionRepository = new();
    private readonly Mock<IAssetListingRepository> _assetListingRepository = new();
    private readonly Mock<IInvestmentPriceProvider> _priceProvider = new();

    [Fact]
    public async Task GetAnalysis_AggregatesEtfHoldingsAndReportsMissingExpenseRatios()
    {
        var knownListing = CreateListing(101, "KNOWN", 0.005m, AssetType.ETF);
        var missingListing = CreateListing(102, "MISSING", null, AssetType.ETF);
        var stockListing = CreateListing(103, "STOCK", 0.002m, AssetType.Stock);

        // The repository pools every account and unlinked trade per listing: 2 - 0.5 in one account plus 1 in another.
        SetupHoldings(1, (knownListing, 2.5m), (missingListing, 4m), (stockListing, 10m));
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
        _transactionRepository.Verify(repository => repository.GetHoldingsByUserAsOf(1, new DateOnly(2026, 9, 15), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAnalysis_SkipsOversoldHoldingsAndHoldingsWithoutPrices()
    {
        var oversoldListing = CreateListing(201, "ZERO", 0.004m, AssetType.ETF);
        var noPriceListing = CreateListing(202, "NO-PRICE", 0.004m, AssetType.ETF);
        SetupHoldings(1, (oversoldListing, -2m), (noPriceListing, 3m));
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
        SetupHoldings(1, (listing, 2m));
        _priceProvider.Setup(provider => provider.GetPricePerUnitAsync(301, DefaultCurrency.USD, It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(75m);

        var result = await CreateService().GetAnalysisAsync(1, DefaultCurrency.USD, DateTime.UtcNow, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(150m, result.TotalHoldingsValue);
        _priceProvider.Verify(provider => provider.GetPricePerUnitAsync(301, DefaultCurrency.USD, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAnalysis_OrdersHoldingsByTickerThenListingId()
    {
        var second = CreateListing(502, "bbb", null, AssetType.ETF);
        var first = CreateListing(501, "AAA", null, AssetType.ETF);
        var sameTicker = CreateListing(500, "BBB", null, AssetType.ETF);
        SetupHoldings(1, (second, 1m), (first, 1m), (sameTicker, 1m));
        _priceProvider.Setup(provider => provider.GetPricePerUnitAsync(It.IsAny<long>(), It.IsAny<Currency>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(10m);

        var result = await CreateService().GetAnalysisAsync(1, DefaultCurrency.PLN, DateTime.UtcNow, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal([501L, 500L, 502L], result.Holdings.Select(holding => holding.ListingId).ToArray());
    }

    [Fact]
    public async Task GetAnalysis_IncludesEtfHoldingsForGuestSandboxUserWithNegativeId()
    {
        // Guest sandbox users are issued negative ids, so they must not be treated as invalid.
        const int guestUserId = -2;
        var listing = CreateListing(401, "CSPX", null, AssetType.ETF);
        var asOfDate = new DateTime(2026, 9, 15);
        SetupHoldings(guestUserId, (listing, 3m));
        _priceProvider.Setup(provider => provider.GetPricePerUnitAsync(401, DefaultCurrency.PLN, asOfDate, It.IsAny<CancellationToken>())).ReturnsAsync(400m);

        var result = await CreateService().GetAnalysisAsync(guestUserId, DefaultCurrency.PLN, asOfDate, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(1, result.TotalHoldingsCount);
        Assert.Equal(1200m, result.TotalHoldingsValue);
        Assert.Equal(1, result.MissingTerCount);
        Assert.Equal(1200m, result.MissingTerHoldingsValue);
        Assert.Equal("CSPX", Assert.Single(result.Holdings).Ticker);
        _transactionRepository.Verify(repository => repository.GetHoldingsByUserAsOf(guestUserId, new DateOnly(2026, 9, 15), It.IsAny<CancellationToken>()), Times.Once);
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
    public async Task GetAnalysis_WithNoHoldingsReturnsSafeEmptyProjection()
    {
        SetupHoldings(1);

        var result = await CreateService().GetAnalysisAsync(1, DefaultCurrency.PLN, DateTime.UtcNow, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Empty(result.Holdings);
        Assert.Equal(0m, result.AnnualFeeCost);
        Assert.Equal(3, result.Projections.Count);
        Assert.All(result.Projections, projection => Assert.Equal(0m, projection.CumulativeFeeCost));
        _assetListingRepository.Verify(repository => repository.GetByIds(It.IsAny<IReadOnlyCollection<long>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetAnalysis_WithUnsetUserIdReturnsSafeEmptyProjectionWithoutReading()
    {
        var result = await CreateService().GetAnalysisAsync(0, DefaultCurrency.PLN, DateTime.UtcNow, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Empty(result.Holdings);
        Assert.Equal(3, result.Projections.Count);
        _transactionRepository.Verify(repository => repository.GetHoldingsByUserAsOf(It.IsAny<long>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private void SetupHoldings(int userId, params (AssetListing Listing, decimal Units)[] holdings)
    {
        _transactionRepository
            .Setup(repository => repository.GetHoldingsByUserAsOf(userId, It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([.. holdings.Select(holding => new InvestmentHolding(
                holding.Listing.Id, holding.Listing.Ticker, "Exchange", holding.Listing.Asset.Type, holding.Units))]);
        _assetListingRepository
            .Setup(repository => repository.GetByIds(It.IsAny<IReadOnlyCollection<long>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<long> ids, CancellationToken _) =>
                [.. holdings.Select(holding => holding.Listing).Where(listing => ids.Contains(listing.Id))]);
    }

    private FeeDragService CreateService() => new(_transactionRepository.Object, _assetListingRepository.Object, _priceProvider.Object);

    private static AssetListing CreateListing(long id, string ticker, decimal? expenseRatio, AssetType type) => new()
    {
        Id = id,
        Ticker = ticker,
        Asset = new Asset { Id = id, Name = $"{ticker} ETF", Type = type, TotalExpenseRatio = expenseRatio },
    };

    // .NET 11 made decimal<->double conversions correctly rounded. Projections compound through
    // Math.Pow(double), so these tests pin the cent results against exact decimal compounding.
    [Fact]
    public void BuildProjections_PinsKnownCentValues()
    {
        var projections = FeeDragService.BuildProjections(10_000m, 0.002m, 0.07m);

        Assert.Collection(
            projections,
            ten => AssertProjection(ten, 10, 19_671.51m, 19_306.90m, 364.61m),
            twenty => AssertProjection(twenty, 20, 38_696.84m, 37_275.64m, 1_421.20m),
            thirty => AssertProjection(thirty, 30, 76_122.55m, 71_967.69m, 4_154.86m));
    }

    public static TheoryData<decimal, decimal, decimal> ProjectionInputs => new()
    {
        { 0.01m, 0m, 0.07m },
        { 999.99m, 0.0003m, 0.05m },
        { 12_345.67m, 0.0075m, 0.10m },
        { 250_000m, 0.02m, -0.10m },
        { 1_000_000m, 0.0022m, 0.0731m },
        { 10_000_000m, 0.0099m, 0.0999m },
        { 1_000_000_000m, 0.005m, 0.10m },
    };

    [Theory]
    [MemberData(nameof(ProjectionInputs))]
    public void BuildProjections_MatchesExactDecimalCompounding(decimal principal, decimal expenseRatio, decimal returnRate)
    {
        var projections = FeeDragService.BuildProjections(principal, expenseRatio, returnRate);

        var netRate = Math.Max(-1m, returnRate - expenseRatio);
        foreach (var projection in projections)
        {
            var gross = RoundMoney(principal * ExactPower(1m + returnRate, projection.Years));
            var net = RoundMoney(principal * ExactPower(1m + netRate, projection.Years));
            AssertProjection(projection, projection.Years, gross, net, RoundMoney(Math.Max(0m, gross - net)));
        }
    }

    private static void AssertProjection(FeeDragProjection projection, int years, decimal gross, decimal net, decimal fee)
    {
        Assert.Equal(years, projection.Years);
        Assert.Equal(gross, projection.GrossFutureValue);
        Assert.Equal(net, projection.NetFutureValue);
        Assert.Equal(fee, projection.CumulativeFeeCost);
    }

    // Repeated multiplication keeps the compounding in decimal, so it is exact to 28 significant digits.
    private static decimal ExactPower(decimal factor, int years)
    {
        var result = 1m;
        for (var year = 0; year < years; year++)
            result *= factor;
        return result;
    }

    private static decimal RoundMoney(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}