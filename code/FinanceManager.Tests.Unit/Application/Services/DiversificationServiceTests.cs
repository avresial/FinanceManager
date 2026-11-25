using FinanceManager.Application.Insights.Diversification;
using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.Currencies.Entities;
using FinanceManager.Domain.Currencies.Repositories;
using FinanceManager.Domain.Transactions.Bonds.Repositories;
using FinanceManager.Domain.Transactions.Cash.Repositories;
using FinanceManager.Domain.Transactions.Investments.Dtos;
using FinanceManager.Domain.Transactions.Investments.Repositories;
using FinanceManager.Domain.Transactions.Shared.Dtos;
using Moq;

namespace FinanceManager.Tests.Unit.Application.Services;

[Collection("Application")]
[Trait("Category", "Unit")]
public class DiversificationServiceTests
{
    private const int _userId = 1;
    private readonly Mock<ICashTransactionReader> _cashReaderMock = new();
    private readonly Mock<IBondTransactionReader> _bondReaderMock = new();
    private readonly Mock<IBondDetailsRepository> _bondDetailsMock = new();
    private readonly Mock<IInvestmentTransactionRepository> _investmentTransactionMock = new();
    private readonly Mock<ICurrencyRepository> _currencyMock = new();
    private readonly DiversificationService _service;
    private readonly DateTime _asOfDate = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public DiversificationServiceTests()
    {
        SetupInvestments();
        SetupBondPositions();
        SetupCashPositions();
        _bondDetailsMock.Setup(x => x.GetNamesByIdsAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, string>());
        _currencyMock.Setup(x => x.GetCurrencies(It.IsAny<CancellationToken>()))
            .Returns(() => new[]
            {
                new Currency(0, "PLN", "zł"),
                new Currency(2, "EUR", "€"),
                new Currency(3, "USD", "$"),
            }.ToAsyncEnumerable());
        _service = new(_cashReaderMock.Object, _bondReaderMock.Object, _bondDetailsMock.Object, _investmentTransactionMock.Object, _currencyMock.Object);
    }

    // The repository pools all of the owner's trades per listing and returns only non-zero holdings.
    private void SetupInvestments(params InvestmentHolding[] holdings) =>
        _investmentTransactionMock.Setup(x => x.GetHoldingsByUserAsOf(It.IsAny<long>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(holdings);

    private static InvestmentHolding Investment(long listingId, string ticker, decimal units) =>
        new(listingId, ticker, "London Stock Exchange", AssetType.Stock, units);

    // Bond positions are pooled per bond definition (partition id) across all accounts and unlinked entries.
    private void SetupBondPositions(params (int BondId, decimal Units)[] positions) =>
        _bondReaderMock.Setup(x => x.GetPositions(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([.. positions.Select(position => new OwnerLedgerPosition(position.BondId, position.Units, _asOfDate))]);

    // Cash positions are pooled per currency id (partition id); PLN is currency 0.
    private void SetupCashPositions(params (int CurrencyId, decimal Balance)[] positions) =>
        _cashReaderMock.Setup(x => x.GetPositions(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([.. positions.Select(position => new OwnerLedgerPosition(position.CurrencyId, position.Balance, _asOfDate))]);

    private void SetupBondNames(params (int BondId, string Name)[] names) =>
        _bondDetailsMock.Setup(x => x.GetNamesByIdsAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(names.ToDictionary(name => name.BondId, name => name.Name));

    [Fact]
    public async Task GetDiversificationBreakdown_IncludesInvestmentHoldingsUnderStocks()
    {
        SetupInvestments(Investment(7, "CSPX", 3m));

        var breakdown = await _service.GetDiversificationBreakdown(_userId, _asOfDate, TestContext.Current.CancellationToken);

        var stocks = Assert.Single(breakdown.AssetClasses, g => g.AssetClass == "Stocks");
        Assert.Contains("CSPX", stocks.Holdings);
    }

    [Fact]
    public async Task GetDiversificationScore_CountsInvestmentHoldingsAsStockClass()
    {
        SetupInvestments(Investment(7, "CSPX", 3m));

        var score = await _service.GetDiversificationScore(_userId, _asOfDate);

        Assert.True(score.HoldingsScore > 0);
        Assert.True(score.Score > 0);
    }

    [Fact]
    public async Task GetDiversificationBreakdown_OmitsSoldOutAndOversoldInvestmentHoldings()
    {
        SetupInvestments(Investment(7, "CSPX", 0m), Investment(8, "VWCE", -2m));

        var breakdown = await _service.GetDiversificationBreakdown(_userId, _asOfDate, TestContext.Current.CancellationToken);

        Assert.DoesNotContain(breakdown.AssetClasses, g => g.AssetClass == "Stocks");
    }

    [Fact]
    public async Task GetDiversificationScore_EmptyPortfolio_ReturnsZero()
    {
        var result = await _service.GetDiversificationScore(_userId, _asOfDate);

        Assert.Equal(0, result.Score);
        Assert.Equal(0, result.AssetClassScore);
        Assert.Equal(0, result.HoldingsScore);
        Assert.Equal("Limited", result.Band);
    }

    [Fact]
    public async Task GetDiversificationScore_OneClassOneTicker_ReturnsLowNonZeroScore()
    {
        SetupInvestments(Investment(1, "AAPL", 10m));

        var result = await _service.GetDiversificationScore(_userId, _asOfDate);

        Assert.True(result.Score > 0);
        Assert.Equal("Limited", result.Band);
    }

    [Fact]
    public async Task GetDiversificationScore_AllCurrentHoldingTypes_ThirtyTickers_ScoresCorrectly()
    {
        // 27 investment tickers + 2 bond instruments + 1 cash = 30 unique holdings
        SetupInvestments([.. Enumerable.Range(1, 27).Select(i => Investment(i, $"STK{i}", 10m))]);
        SetupBondPositions((1, 10m), (2, 10m));
        SetupCashPositions((0, 100m));

        var result = await _service.GetDiversificationScore(_userId, _asOfDate);

        // 3 classes (Stock, Bond, Cash) → (3/6)*50 = 25 pts
        // 30 tickers → min(30/30, 1)*50 = 50 pts
        Assert.Equal(25, result.AssetClassScore);
        Assert.Equal(50, result.HoldingsScore);
        Assert.Equal(75, result.Score);
    }

    [Fact]
    public async Task GetDiversificationScore_SameTickerOnTwoListings_CountedOnce()
    {
        SetupInvestments(Investment(1, "AAPL", 10m), Investment(2, "aapl", 5m));

        var result = await _service.GetDiversificationScore(_userId, _asOfDate);

        var expectedHoldingsScore = (int)(1 / 30.0 * 50);
        Assert.Equal(expectedHoldingsScore, result.HoldingsScore);
    }

    [Fact]
    public async Task GetDiversificationScore_PooledCashInOneCurrency_CountsOneHolding()
    {
        // Several former cash accounts in one currency are a single pooled position.
        SetupCashPositions((0, 1500m));

        var result = await _service.GetDiversificationScore(_userId, _asOfDate);

        var expectedHoldingsScore = (int)(1 / 30.0 * 50);
        Assert.Equal(expectedHoldingsScore, result.HoldingsScore);
        var expectedAssetClassScore = (int)(1 / 6.0 * 50);
        Assert.Equal(expectedAssetClassScore, result.AssetClassScore);
    }

    [Fact]
    public async Task GetDiversificationScore_DifferentCashCurrencies_CountAsDistinctHoldingsButOneClass()
    {
        SetupCashPositions((0, 500m), (2, 10m));

        var result = await _service.GetDiversificationScore(_userId, _asOfDate);

        Assert.Equal((int)(1 / 6.0 * 50), result.AssetClassScore);
        Assert.Equal((int)(2 / 30.0 * 50), result.HoldingsScore);
    }

    [Fact]
    public async Task GetDiversificationScore_NegativeCashBalance_DoesNotCountAsCash()
    {
        SetupCashPositions((0, -500m));

        var result = await _service.GetDiversificationScore(_userId, _asOfDate);

        Assert.Equal(0, result.Score);
        Assert.Equal("Limited", result.Band);
    }

    [Fact]
    public async Task GetDiversificationScore_StockFullySold_NotCounted()
    {
        SetupInvestments(Investment(1, "AAPL", 0m));

        var result = await _service.GetDiversificationScore(_userId, _asOfDate);

        Assert.Equal(0, result.HoldingsScore);
        Assert.Equal(0, result.AssetClassScore);
        Assert.Equal(0, result.Score);
    }

    [Fact]
    public async Task GetDiversificationScore_StockPartiallySold_StillCounted()
    {
        SetupInvestments(Investment(1, "AAPL", 5m));

        var result = await _service.GetDiversificationScore(_userId, _asOfDate);

        var expectedHoldingsScore = (int)(1 / 30.0 * 50);
        var expectedAssetClassScore = (int)(1 / 6.0 * 50);
        Assert.Equal(expectedHoldingsScore, result.HoldingsScore);
        Assert.Equal(expectedAssetClassScore, result.AssetClassScore);
    }

    [Fact]
    public async Task GetDiversificationScore_BondFullyLiquidated_NotCounted()
    {
        SetupBondPositions((1, 0m), (2, 10m));

        var result = await _service.GetDiversificationScore(_userId, _asOfDate);

        var expectedHoldingsScore = (int)(1 / 30.0 * 50);
        var expectedAssetClassScore = (int)(1 / 6.0 * 50);
        Assert.Equal(expectedHoldingsScore, result.HoldingsScore);
        Assert.Equal(expectedAssetClassScore, result.AssetClassScore);
    }

    [Fact]
    public async Task GetDiversificationScore_AllBondsLiquidated_BondClassExcluded()
    {
        SetupBondPositions((1, 0m));

        var result = await _service.GetDiversificationScore(_userId, _asOfDate);

        Assert.Equal(0, result.HoldingsScore);
        Assert.Equal(0, result.AssetClassScore);
    }

    [Theory]
    [InlineData(0, "Limited")]
    [InlineData(33, "Limited")]
    [InlineData(34, "Moderate")]
    [InlineData(66, "Moderate")]
    [InlineData(67, "Broad")]
    [InlineData(100, "Broad")]
    public void GetBand_MatchesScoreRanges(int score, string expectedBand)
    {
        Assert.Equal(expectedBand, DiversificationService.GetBand(score));
    }

    [Fact]
    public async Task GetDiversificationBreakdown_EmptyPortfolio_ReturnsNoGroups()
    {
        var result = await _service.GetDiversificationBreakdown(_userId, _asOfDate, TestContext.Current.CancellationToken);

        Assert.Empty(result.AssetClasses);
    }

    [Fact]
    public async Task GetDiversificationBreakdown_GroupsHoldingsByClassWithResolvedNames()
    {
        SetupInvestments(Investment(2, "MSFT", 10m), Investment(1, "AAPL", 10m));
        SetupBondPositions((7, 10m));
        SetupCashPositions((0, 100m));
        SetupBondNames((7, "Treasury 2030"));

        var result = await _service.GetDiversificationBreakdown(_userId, _asOfDate, TestContext.Current.CancellationToken);

        Assert.Equal(["Stocks", "Bonds", "Cash"], result.AssetClasses.Select(g => g.AssetClass));
        Assert.Equal(["AAPL", "MSFT"], result.AssetClasses[0].Holdings);
        Assert.Equal(["Treasury 2030"], result.AssetClasses[1].Holdings);
        Assert.Equal(["PLN"], result.AssetClasses[2].Holdings);
    }

    [Fact]
    public async Task GetDiversificationBreakdown_CashCurrencies_ArePositiveDeduplicatedAndSorted()
    {
        SetupCashPositions((3, 50m), (2, 15m), (0, 0m), (4, -1m));

        var result = await _service.GetDiversificationBreakdown(_userId, _asOfDate, TestContext.Current.CancellationToken);

        var cash = Assert.Single(result.AssetClasses);
        Assert.Equal("Cash", cash.AssetClass);
        Assert.Equal(["EUR", "USD"], cash.Holdings);
    }

    [Fact]
    public async Task GetDiversificationBreakdown_UnknownBondId_FallsBackToPlaceholderName()
    {
        SetupBondPositions((42, 10m));
        // No matching name registered → the batched lookup omits the id.

        var result = await _service.GetDiversificationBreakdown(_userId, _asOfDate, TestContext.Current.CancellationToken);

        var bonds = Assert.Single(result.AssetClasses);
        Assert.Equal(["Bond #42"], bonds.Holdings);
    }

    [Fact]
    public async Task GetDiversificationBreakdown_ResolvesAllBondNamesInOneBatch()
    {
        SetupBondPositions((7, 10m), (8, 5m), (9, 0m));
        SetupBondNames((7, "Treasury 2030"), (8, "Corporate 2028"));

        var result = await _service.GetDiversificationBreakdown(_userId, _asOfDate, TestContext.Current.CancellationToken);

        Assert.Equal(["Corporate 2028", "Treasury 2030"], Assert.Single(result.AssetClasses).Holdings);
        _bondDetailsMock.Verify(x => x.GetNamesByIdsAsync(
            It.Is<IReadOnlyCollection<int>>(ids => ids.OrderBy(id => id).SequenceEqual(new[] { 7, 8 })),
            It.IsAny<CancellationToken>()), Times.Once);
        _bondDetailsMock.Verify(x => x.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetDiversificationBreakdown_DuplicateTickersAcrossListings_AreListedOnce()
    {
        SetupInvestments(Investment(1, "AAPL", 8m), Investment(3, "AAPL", 2m));

        var result = await _service.GetDiversificationBreakdown(_userId, _asOfDate, TestContext.Current.CancellationToken);

        var stocks = Assert.Single(result.AssetClasses);
        Assert.Equal(["AAPL"], stocks.Holdings);
    }

    [Fact]
    public async Task GetDiversificationBreakdown_ReadsHoldingsOfNegativeGuestUserAsOfDate()
    {
        const int guestUserId = -3;
        SetupInvestments(Investment(1, "AAPL", 8m));
        SetupBondPositions((7, 1m));
        SetupCashPositions((0, 1m));

        var result = await _service.GetDiversificationBreakdown(guestUserId, _asOfDate, TestContext.Current.CancellationToken);

        Assert.Equal(["Stocks", "Bonds", "Cash"], result.AssetClasses.Select(g => g.AssetClass));
        _investmentTransactionMock.Verify(x => x.GetHoldingsByUserAsOf(guestUserId, new DateOnly(2025, 1, 1), It.IsAny<CancellationToken>()), Times.Once);
        _bondReaderMock.Verify(x => x.GetPositions(guestUserId, _asOfDate, It.IsAny<CancellationToken>()), Times.Once);
        _cashReaderMock.Verify(x => x.GetPositions(guestUserId, _asOfDate, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetDiversificationScore_UnsetUserId_ReturnsZeroWithoutReading()
    {
        var result = await _service.GetDiversificationScore(0, _asOfDate);

        Assert.Equal(0, result.Score);
        _investmentTransactionMock.Verify(x => x.GetHoldingsByUserAsOf(It.IsAny<long>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()), Times.Never);
        _bondReaderMock.Verify(x => x.GetPositions(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
        _cashReaderMock.Verify(x => x.GetPositions(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}