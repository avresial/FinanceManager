using FinanceManager.Application.Insights.Diversification;
using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Bond.Repositories;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Repositories;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
using Moq;

namespace FinanceManager.Tests.Unit.Application.Services;

[Collection("Application")]
[Trait("Category", "Unit")]
public class DiversificationServiceTests
{
    private readonly Mock<IFinancialAccountRepository> _repositoryMock = new();
    private readonly Mock<IBondDetailsRepository> _bondDetailsMock = new();
    private readonly Mock<IInvestmentTransactionRepository> _investmentTransactionMock = new();
    private readonly DiversificationService _service;
    private readonly DateTime _asOfDate = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public DiversificationServiceTests()
    {
        _investmentTransactionMock.Setup(x => x.GetByUser(It.IsAny<long>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<InvestmentTransaction>)[]);
        SetupBondAccounts();
        SetupCurrencyAccounts();
        _service = new(_repositoryMock.Object, _bondDetailsMock.Object, _investmentTransactionMock.Object);
    }

    private void SetupInvestmentTransactions(params InvestmentTransaction[] transactions) =>
        _investmentTransactionMock.Setup(x => x.GetByUser(It.IsAny<long>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(transactions);

    private static InvestmentTransaction Investment(long listingId, string ticker, decimal signedQuantity, DateOnly? tradeDate = null) =>
        new()
        {
            AssetListingId = listingId,
            AssetListing = new AssetListing { Id = listingId, Ticker = ticker, ExchangeMic = "XLON", ExchangeName = "London Stock Exchange", TradingCurrency = "USD" },
            Type = signedQuantity < 0 ? InvestmentTransactionType.Sell : InvestmentTransactionType.Buy,
            Quantity = Math.Abs(signedQuantity),
            UnitPrice = 100m,
            Currency = "USD",
            TradeDate = tradeDate ?? new DateOnly(2024, 12, 1)
        };

    private void SetupBondAccounts(params BondAccount[] accounts) =>
        _repositoryMock.Setup(x => x.GetAccounts<BondAccount>(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(accounts.ToAsyncEnumerable());

    private void SetupCurrencyAccounts(params CurrencyAccount[] accounts) =>
        _repositoryMock.Setup(x => x.GetAccounts<CurrencyAccount>(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(accounts.ToAsyncEnumerable());

    private void SetupBondDetails(params BondDetails[] details)
    {
        foreach (var detail in details)
            _bondDetailsMock.Setup(x => x.GetByIdAsync(detail.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(detail);
    }

    private static BondDetails Bond(int id, string name) =>
        new() { Id = id, Name = name, Type = BondType.InflationBond, Currency = new Currency(1, "PLN", "zł") };

    [Fact]
    public async Task GetDiversificationBreakdown_IncludesInvestmentHoldingsUnderStocks()
    {
        SetupInvestmentTransactions(Investment(7, "CSPX", 3m));

        var breakdown = await _service.GetDiversificationBreakdown(1, _asOfDate, TestContext.Current.CancellationToken);

        var stocks = Assert.Single(breakdown.AssetClasses, g => g.AssetClass == "Stocks");
        Assert.Contains("CSPX", stocks.Holdings);
    }

    [Fact]
    public async Task GetDiversificationScore_CountsInvestmentHoldingsAsStockClass()
    {
        SetupInvestmentTransactions(Investment(7, "CSPX", 3m));

        var score = await _service.GetDiversificationScore(1, _asOfDate);

        Assert.True(score.HoldingsScore > 0);
        Assert.True(score.Score > 0);
    }

    [Fact]
    public async Task GetDiversificationBreakdown_OmitsFullySoldInvestmentHoldings()
    {
        SetupInvestmentTransactions(
            Investment(7, "CSPX", 3m, new DateOnly(2024, 12, 1)),
            Investment(7, "CSPX", -3m, new DateOnly(2024, 12, 2)));

        var breakdown = await _service.GetDiversificationBreakdown(1, _asOfDate, TestContext.Current.CancellationToken);

        Assert.DoesNotContain(breakdown.AssetClasses, g => g.AssetClass == "Stocks");
    }

    [Fact]
    public async Task GetDiversificationScore_EmptyPortfolio_ReturnsZero()
    {
        var result = await _service.GetDiversificationScore(1, _asOfDate);

        Assert.Equal(0, result.Score);
        Assert.Equal(0, result.AssetClassScore);
        Assert.Equal(0, result.HoldingsScore);
        Assert.Equal("Limited", result.Band);
    }

    [Fact]
    public async Task GetDiversificationScore_OneClassOneTicker_ReturnsLowNonZeroScore()
    {
        SetupInvestmentTransactions(Investment(1, "AAPL", 10m));

        var result = await _service.GetDiversificationScore(1, _asOfDate);

        Assert.True(result.Score > 0);
        Assert.Equal("Limited", result.Band);
    }

    [Fact]
    public async Task GetDiversificationScore_AllCurrentAccountTypes_ThirtyTickers_ScoresCorrectly()
    {
        // 27 investment tickers + 2 bond instruments + 1 cash = 30 unique holdings
        SetupInvestmentTransactions(Enumerable.Range(1, 27).Select(i => Investment(i, $"STK{i}", 10m)).ToArray());

        var bondAccount = new BondAccount(1, 2, "bonds",
            [new BondAccountEntry(2, 1, _asOfDate, 10m, 10m, 1),
             new BondAccountEntry(2, 2, _asOfDate, 10m, 10m, 2)],
            AccountLabel.Other);

        var cashAccount = new CurrencyAccount(1, 3, "cash", AccountLabel.Cash);
        cashAccount.Add(new CurrencyAccountEntry(3, 1, _asOfDate, 100, 100), false);

        SetupBondAccounts(bondAccount);
        SetupCurrencyAccounts(cashAccount);

        var result = await _service.GetDiversificationScore(1, _asOfDate);

        // 3 classes (Stock, Bond, Cash) → (3/6)*50 = 25 pts
        // 30 tickers → min(30/30, 1)*50 = 50 pts
        Assert.Equal(25, result.AssetClassScore);
        Assert.Equal(50, result.HoldingsScore);
        Assert.Equal(75, result.Score);
    }

    [Fact]
    public async Task GetDiversificationScore_DuplicateTicker_CountedOnce()
    {
        // Same listing bought twice nets one held holding.
        SetupInvestmentTransactions(
            Investment(1, "AAPL", 10m, new DateOnly(2024, 11, 1)),
            Investment(1, "AAPL", 5m, new DateOnly(2024, 12, 1)));

        var result = await _service.GetDiversificationScore(1, _asOfDate);

        var expectedHoldingsScore = (int)(1 / 30.0 * 50);
        Assert.Equal(expectedHoldingsScore, result.HoldingsScore);
    }

    [Fact]
    public async Task GetDiversificationScore_MultipleCurrencyAccounts_CashCountsAsOneTicker()
    {
        var cashAccount1 = new CurrencyAccount(1, 1, "checking", AccountLabel.Cash);
        cashAccount1.Add(new CurrencyAccountEntry(1, 1, _asOfDate, 500, 500), false);

        var cashAccount2 = new CurrencyAccount(1, 2, "savings", AccountLabel.Cash);
        cashAccount2.Add(new CurrencyAccountEntry(2, 1, _asOfDate, 1000, 1000), false);

        SetupCurrencyAccounts(cashAccount1, cashAccount2);

        var result = await _service.GetDiversificationScore(1, _asOfDate);

        var expectedHoldingsScore = (int)(1 / 30.0 * 50);
        Assert.Equal(expectedHoldingsScore, result.HoldingsScore);
        var expectedAssetClassScore = (int)(1 / 6.0 * 50);
        Assert.Equal(expectedAssetClassScore, result.AssetClassScore);
    }

    [Fact]
    public async Task GetDiversificationScore_CurrencyAccountWithNegativeBalance_DoesNotCountAsCash()
    {
        var debtAccount = new CurrencyAccount(1, 1, "credit", AccountLabel.Other);
        debtAccount.Add(new CurrencyAccountEntry(1, 1, _asOfDate, -500, -500), false);

        SetupCurrencyAccounts(debtAccount);

        var result = await _service.GetDiversificationScore(1, _asOfDate);

        Assert.Equal(0, result.Score);
        Assert.Equal("Limited", result.Band);
    }

    [Fact]
    public async Task GetDiversificationScore_StockFullySold_NotCounted()
    {
        SetupInvestmentTransactions(
            Investment(1, "AAPL", 10m, new DateOnly(2024, 11, 1)),
            Investment(1, "AAPL", -10m, new DateOnly(2024, 12, 1)));

        var result = await _service.GetDiversificationScore(1, _asOfDate);

        Assert.Equal(0, result.HoldingsScore);
        Assert.Equal(0, result.AssetClassScore);
        Assert.Equal(0, result.Score);
    }

    [Fact]
    public async Task GetDiversificationScore_StockPartiallySold_StillCounted()
    {
        SetupInvestmentTransactions(
            Investment(1, "AAPL", 10m, new DateOnly(2024, 11, 1)),
            Investment(1, "AAPL", -5m, new DateOnly(2024, 12, 1)));

        var result = await _service.GetDiversificationScore(1, _asOfDate);

        var expectedHoldingsScore = (int)(1 / 30.0 * 50);
        var expectedAssetClassScore = (int)(1 / 6.0 * 50);
        Assert.Equal(expectedHoldingsScore, result.HoldingsScore);
        Assert.Equal(expectedAssetClassScore, result.AssetClassScore);
    }

    [Fact]
    public async Task GetDiversificationScore_BondFullyLiquidated_NotCounted()
    {
        var buyDate = _asOfDate.AddDays(-30);
        var sellDate = _asOfDate.AddDays(-10);
        var bondAccount = new BondAccount(1, 1, "bonds",
            [
                new BondAccountEntry(1, 1, buyDate, 10m, 10m, 1),
                new BondAccountEntry(1, 2, sellDate, 0m, -10m, 1),
                new BondAccountEntry(1, 3, buyDate, 10m, 10m, 2),
            ],
            AccountLabel.Other);

        SetupBondAccounts(bondAccount);

        var result = await _service.GetDiversificationScore(1, _asOfDate);

        var expectedHoldingsScore = (int)(1 / 30.0 * 50);
        var expectedAssetClassScore = (int)(1 / 6.0 * 50);
        Assert.Equal(expectedHoldingsScore, result.HoldingsScore);
        Assert.Equal(expectedAssetClassScore, result.AssetClassScore);
    }

    [Fact]
    public async Task GetDiversificationScore_AllBondsLiquidated_BondClassExcluded()
    {
        var buyDate = _asOfDate.AddDays(-30);
        var sellDate = _asOfDate.AddDays(-10);
        var bondAccount = new BondAccount(1, 1, "bonds",
            [
                new BondAccountEntry(1, 1, buyDate, 10m, 10m, 1),
                new BondAccountEntry(1, 2, sellDate, 0m, -10m, 1),
            ],
            AccountLabel.Other);

        SetupBondAccounts(bondAccount);

        var result = await _service.GetDiversificationScore(1, _asOfDate);

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
        var result = await _service.GetDiversificationBreakdown(1, _asOfDate, TestContext.Current.CancellationToken);

        Assert.Empty(result.AssetClasses);
    }

    [Fact]
    public async Task GetDiversificationBreakdown_GroupsHoldingsByClassWithResolvedNames()
    {
        SetupInvestmentTransactions(
            Investment(1, "AAPL", 10m),
            Investment(2, "MSFT", 10m));

        var bondAccount = new BondAccount(1, 2, "bonds",
            [new BondAccountEntry(2, 1, _asOfDate, 10m, 10m, 7)], AccountLabel.Other);

        var cashAccount = new CurrencyAccount(1, 3, "cash", AccountLabel.Cash);
        cashAccount.Add(new CurrencyAccountEntry(3, 1, _asOfDate, 100, 100), false);

        SetupBondAccounts(bondAccount);
        SetupCurrencyAccounts(cashAccount);
        SetupBondDetails(Bond(7, "Treasury 2030"));

        var result = await _service.GetDiversificationBreakdown(1, _asOfDate, TestContext.Current.CancellationToken);

        Assert.Equal(["Stocks", "Bonds", "Cash"], result.AssetClasses.Select(g => g.AssetClass));
        Assert.Equal(["AAPL", "MSFT"], result.AssetClasses[0].Holdings);
        Assert.Equal(["Treasury 2030"], result.AssetClasses[1].Holdings);
        Assert.Equal(["Cash"], result.AssetClasses[2].Holdings);
    }

    [Fact]
    public async Task GetDiversificationBreakdown_UnknownBondId_FallsBackToPlaceholderName()
    {
        var bondAccount = new BondAccount(1, 1, "bonds",
            [new BondAccountEntry(1, 1, _asOfDate, 10m, 10m, 42)], AccountLabel.Other);

        SetupBondAccounts(bondAccount);
        // No matching BondDetails registered → GetByIdAsync returns null.

        var result = await _service.GetDiversificationBreakdown(1, _asOfDate, TestContext.Current.CancellationToken);

        var bonds = Assert.Single(result.AssetClasses);
        Assert.Equal(["Bond #42"], bonds.Holdings);
    }

    [Fact]
    public async Task GetDiversificationBreakdown_SoldOutAndDuplicateHoldings_AreExcludedAndDeduped()
    {
        SetupInvestmentTransactions(
            Investment(1, "AAPL", 5m, new DateOnly(2024, 11, 1)),
            // Fully sold position must not appear.
            Investment(2, "GOOG", 10m, new DateOnly(2024, 11, 1)),
            Investment(2, "GOOG", -10m, new DateOnly(2024, 12, 1)),
            // Same listing bought again must be listed once.
            Investment(1, "AAPL", 3m, new DateOnly(2024, 12, 1)));

        var result = await _service.GetDiversificationBreakdown(1, _asOfDate, TestContext.Current.CancellationToken);

        var stocks = Assert.Single(result.AssetClasses);
        Assert.Equal(["AAPL"], stocks.Holdings);
    }
}