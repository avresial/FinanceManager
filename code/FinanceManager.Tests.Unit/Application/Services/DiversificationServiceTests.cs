using FinanceManager.Application.Services;
using FinanceManager.Domain.Entities.Bonds;
using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Entities.FinancialAccounts.Currencies;
using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.Entities.Stocks;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Repositories;
using FinanceManager.Domain.Repositories.Account;
using Moq;

namespace FinanceManager.Tests.Unit.Application.Services;

[Collection("Application")]
[Trait("Category", "Unit")]
public class DiversificationServiceTests
{
    private readonly Mock<IFinancialAccountRepository> _repositoryMock = new();
    private readonly Mock<IStockDetailsRepository> _stockDetailsMock = new();
    private readonly Mock<IBondDetailsRepository> _bondDetailsMock = new();
    private readonly DiversificationService _service;
    private readonly DateTime _asOfDate = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public DiversificationServiceTests()
    {
        _stockDetailsMock.Setup(x => x.GetAll(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _service = new(_repositoryMock.Object, _stockDetailsMock.Object, _bondDetailsMock.Object);
    }

    private void SetupStockAccounts(params StockAccount[] accounts) =>
        _repositoryMock.Setup(x => x.GetAccounts<StockAccount>(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(accounts.ToAsyncEnumerable());

    private void SetupBondAccounts(params BondAccount[] accounts) =>
        _repositoryMock.Setup(x => x.GetAccounts<BondAccount>(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(accounts.ToAsyncEnumerable());

    private void SetupCurrencyAccounts(params CurrencyAccount[] accounts) =>
        _repositoryMock.Setup(x => x.GetAccounts<CurrencyAccount>(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(accounts.ToAsyncEnumerable());

    private void SetupStockDetails(params StockDetails[] details) =>
        _stockDetailsMock.Setup(x => x.GetAll(It.IsAny<CancellationToken>()))
            .ReturnsAsync(details);

    private void SetupBondDetails(params BondDetails[] details)
    {
        foreach (var detail in details)
            _bondDetailsMock.Setup(x => x.GetByIdAsync(detail.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(detail);
    }

    private static StockDetails Stock(string isin, string ticker) =>
        new() { Isin = isin, Ticker = ticker, Currency = new Currency(1, "USD", "$") };

    private static BondDetails Bond(int id, string name) =>
        new() { Id = id, Name = name, Type = BondType.InflationBond, Currency = new Currency(1, "PLN", "zł") };

    [Fact]
    public async Task GetDiversificationScore_EmptyPortfolio_ReturnsZero()
    {
        SetupStockAccounts();
        SetupBondAccounts();
        SetupCurrencyAccounts();

        var result = await _service.GetDiversificationScore(1, _asOfDate);

        Assert.Equal(0, result.Score);
        Assert.Equal(0, result.AssetClassScore);
        Assert.Equal(0, result.HoldingsScore);
        Assert.Equal("Limited", result.Band);
    }

    [Fact]
    public async Task GetDiversificationScore_OneClassOneTicker_ReturnsLowNonZeroScore()
    {
        var account = new StockAccount(1, 1, "stocks");
        account.Add(new StockAccountEntry(1, 1, _asOfDate, 10, 10, "AAPL", InvestmentType.Stock), false);

        SetupStockAccounts(account);
        SetupBondAccounts();
        SetupCurrencyAccounts();

        var result = await _service.GetDiversificationScore(1, _asOfDate);

        // 1 class → (1/6)*50 ≈ 8; 1 ticker → (1/30)*50 ≈ 1; total ≈ 9
        Assert.True(result.Score > 0);
        Assert.Equal("Limited", result.Band);
    }

    [Fact]
    public async Task GetDiversificationScore_AllCurrentAccountTypes_ThirtyTickers_ScoresCorrectly()
    {
        // 27 stock tickers + 2 bond instruments + 1 cash = 30 unique holdings
        var stockAccount = new StockAccount(1, 1, "stocks");
        for (int i = 1; i <= 27; i++)
            stockAccount.Add(new StockAccountEntry(1, i, _asOfDate, 10, 10, $"STK{i}", InvestmentType.Stock), false);

        var bondAccount = new BondAccount(1, 2, "bonds",
            [new BondAccountEntry(2, 1, _asOfDate, 10m, 10m, 1),
             new BondAccountEntry(2, 2, _asOfDate, 10m, 10m, 2)],
            AccountLabel.Other);

        var cashAccount = new CurrencyAccount(1, 3, "cash", AccountLabel.Cash);
        cashAccount.Add(new CurrencyAccountEntry(3, 1, _asOfDate, 100, 100), false);

        SetupStockAccounts(stockAccount);
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
    public async Task GetDiversificationScore_DuplicateTickerAcrossAccounts_CountedOnce()
    {
        var account1 = new StockAccount(1, 1, "stocks-a");
        account1.Add(new StockAccountEntry(1, 1, _asOfDate, 10, 10, "AAPL", InvestmentType.Stock), false);

        var account2 = new StockAccount(1, 2, "stocks-b");
        account2.Add(new StockAccountEntry(2, 1, _asOfDate, 5, 5, "AAPL", InvestmentType.Stock), false);

        SetupStockAccounts(account1, account2);
        SetupBondAccounts();
        SetupCurrencyAccounts();

        var result = await _service.GetDiversificationScore(1, _asOfDate);

        // 1 unique ticker despite two accounts
        var expectedHoldingsScore = (int)(1 / 30.0 * 50);
        Assert.Equal(expectedHoldingsScore, result.HoldingsScore);
    }

    [Fact]
    public async Task GetDiversificationScore_UnknownInvestmentType_IsIgnoredFromAssetClasses()
    {
        var account = new StockAccount(1, 1, "mixed");
        account.Add(new StockAccountEntry(1, 1, _asOfDate, 10, 10, "AAPL", InvestmentType.Stock), false);
        account.Add(new StockAccountEntry(1, 2, _asOfDate, 5, 5, "???", InvestmentType.Unknown), false);

        SetupStockAccounts(account);
        SetupBondAccounts();
        SetupCurrencyAccounts();

        var result = await _service.GetDiversificationScore(1, _asOfDate);

        // Only Stock class counted, Unknown is excluded
        var expectedAssetClassScore = (int)(1 / 6.0 * 50);
        Assert.Equal(expectedAssetClassScore, result.AssetClassScore);
    }

    [Fact]
    public async Task GetDiversificationScore_MultipleCurrencyAccounts_CashCountsAsOneTicker()
    {
        var cashAccount1 = new CurrencyAccount(1, 1, "checking", AccountLabel.Cash);
        cashAccount1.Add(new CurrencyAccountEntry(1, 1, _asOfDate, 500, 500), false);

        var cashAccount2 = new CurrencyAccount(1, 2, "savings", AccountLabel.Cash);
        cashAccount2.Add(new CurrencyAccountEntry(2, 1, _asOfDate, 1000, 1000), false);

        SetupStockAccounts();
        SetupBondAccounts();
        SetupCurrencyAccounts(cashAccount1, cashAccount2);

        var result = await _service.GetDiversificationScore(1, _asOfDate);

        // Two cash accounts → still only 1 cash holding and 1 asset class
        var expectedHoldingsScore = (int)(1 / 30.0 * 50);
        Assert.Equal(expectedHoldingsScore, result.HoldingsScore);
        var expectedAssetClassScore = (int)(1 / 6.0 * 50);
        Assert.Equal(expectedAssetClassScore, result.AssetClassScore);
    }

    [Fact]
    public async Task GetDiversificationScore_CurrencyAccountWithNegativeBalance_DoesNotCountAsCash()
    {
        // Liability-style account: latest balance is negative
        var debtAccount = new CurrencyAccount(1, 1, "credit", AccountLabel.Other);
        debtAccount.Add(new CurrencyAccountEntry(1, 1, _asOfDate, -500, -500), false);

        SetupStockAccounts();
        SetupBondAccounts();
        SetupCurrencyAccounts(debtAccount);

        var result = await _service.GetDiversificationScore(1, _asOfDate);

        Assert.Equal(0, result.Score);
        Assert.Equal("Limited", result.Band);
    }

    [Fact]
    public async Task GetDiversificationScore_StockFullySold_NotCounted()
    {
        // Buy 10 shares, then sell all 10. Net position = 0 → not held.
        var buyDate = _asOfDate.AddDays(-30);
        var sellDate = _asOfDate.AddDays(-10);
        var account = new StockAccount(1, 1, "stocks");
        account.Add(new StockAccountEntry(1, 1, buyDate, 10, 10, "AAPL", InvestmentType.Stock), false);
        account.Add(new StockAccountEntry(1, 2, sellDate, 0, -10, "AAPL", InvestmentType.Stock), false);

        SetupStockAccounts(account);
        SetupBondAccounts();
        SetupCurrencyAccounts();

        var result = await _service.GetDiversificationScore(1, _asOfDate);

        Assert.Equal(0, result.HoldingsScore);
        Assert.Equal(0, result.AssetClassScore);
        Assert.Equal(0, result.Score);
    }

    [Fact]
    public async Task GetDiversificationScore_StockFullySold_OneStillHeld_OnlyHeldCounts()
    {
        // AAPL fully sold, MSFT still held → only MSFT counts; Stock class still present.
        var buyDate = _asOfDate.AddDays(-30);
        var sellDate = _asOfDate.AddDays(-10);
        var account = new StockAccount(1, 1, "stocks");
        account.Add(new StockAccountEntry(1, 1, buyDate, 10, 10, "AAPL", InvestmentType.Stock), false);
        account.Add(new StockAccountEntry(1, 2, sellDate, 0, -10, "AAPL", InvestmentType.Stock), false);
        account.Add(new StockAccountEntry(1, 3, buyDate, 5, 5, "MSFT", InvestmentType.Stock), false);

        SetupStockAccounts(account);
        SetupBondAccounts();
        SetupCurrencyAccounts();

        var result = await _service.GetDiversificationScore(1, _asOfDate);

        var expectedHoldingsScore = (int)(1 / 30.0 * 50);
        var expectedAssetClassScore = (int)(1 / 6.0 * 50);
        Assert.Equal(expectedHoldingsScore, result.HoldingsScore);
        Assert.Equal(expectedAssetClassScore, result.AssetClassScore);
    }

    [Fact]
    public async Task GetDiversificationScore_StockPartiallySold_StillCounted()
    {
        // Buy 10, sell 5. Net = 5 > 0 → still held.
        var buyDate = _asOfDate.AddDays(-30);
        var sellDate = _asOfDate.AddDays(-10);
        var account = new StockAccount(1, 1, "stocks");
        account.Add(new StockAccountEntry(1, 1, buyDate, 10, 10, "AAPL", InvestmentType.Stock), false);
        account.Add(new StockAccountEntry(1, 2, sellDate, 5, -5, "AAPL", InvestmentType.Stock), false);

        SetupStockAccounts(account);
        SetupBondAccounts();
        SetupCurrencyAccounts();

        var result = await _service.GetDiversificationScore(1, _asOfDate);

        var expectedHoldingsScore = (int)(1 / 30.0 * 50);
        var expectedAssetClassScore = (int)(1 / 6.0 * 50);
        Assert.Equal(expectedHoldingsScore, result.HoldingsScore);
        Assert.Equal(expectedAssetClassScore, result.AssetClassScore);
    }

    [Fact]
    public async Task GetDiversificationScore_BondFullyLiquidated_NotCounted()
    {
        // Bond 1 fully liquidated, bond 2 still held.
        var buyDate = _asOfDate.AddDays(-30);
        var sellDate = _asOfDate.AddDays(-10);
        var bondAccount = new BondAccount(1, 1, "bonds",
            [
                new BondAccountEntry(1, 1, buyDate, 10m, 10m, 1),
                new BondAccountEntry(1, 2, sellDate, 0m, -10m, 1),
                new BondAccountEntry(1, 3, buyDate, 10m, 10m, 2),
            ],
            AccountLabel.Other);

        SetupStockAccounts();
        SetupBondAccounts(bondAccount);
        SetupCurrencyAccounts();

        var result = await _service.GetDiversificationScore(1, _asOfDate);

        // Only bond 2 counts → 1 ticker, 1 asset class
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

        SetupStockAccounts();
        SetupBondAccounts(bondAccount);
        SetupCurrencyAccounts();

        var result = await _service.GetDiversificationScore(1, _asOfDate);

        Assert.Equal(0, result.HoldingsScore);
        Assert.Equal(0, result.AssetClassScore);
    }

    [Fact]
    public async Task GetDiversificationScore_HoldingSoldInOneAccount_HeldInAnother_StillCounts()
    {
        // AAPL sold in account 1, but still held in account 2 → ticker is currently held.
        var buyDate = _asOfDate.AddDays(-30);
        var sellDate = _asOfDate.AddDays(-10);
        var account1 = new StockAccount(1, 1, "stocks-a");
        account1.Add(new StockAccountEntry(1, 1, buyDate, 10, 10, "AAPL", InvestmentType.Stock), false);
        account1.Add(new StockAccountEntry(1, 2, sellDate, 0, -10, "AAPL", InvestmentType.Stock), false);

        var account2 = new StockAccount(1, 2, "stocks-b");
        account2.Add(new StockAccountEntry(2, 1, buyDate, 5, 5, "AAPL", InvestmentType.Stock), false);

        SetupStockAccounts(account1, account2);
        SetupBondAccounts();
        SetupCurrencyAccounts();

        var result = await _service.GetDiversificationScore(1, _asOfDate);

        var expectedHoldingsScore = (int)(1 / 30.0 * 50);
        Assert.Equal(expectedHoldingsScore, result.HoldingsScore);
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
        SetupStockAccounts();
        SetupBondAccounts();
        SetupCurrencyAccounts();

        var result = await _service.GetDiversificationBreakdown(1, _asOfDate, TestContext.Current.CancellationToken);

        Assert.Empty(result.AssetClasses);
    }

    [Fact]
    public async Task GetDiversificationBreakdown_GroupsHoldingsByClassWithResolvedNames()
    {
        var stockAccount = new StockAccount(1, 1, "stocks");
        stockAccount.Add(new StockAccountEntry(1, 1, _asOfDate, 10, 10, "US0378331005", InvestmentType.Stock), false);
        stockAccount.Add(new StockAccountEntry(1, 2, _asOfDate, 10, 10, "US5949181045", InvestmentType.Stock), false);

        var bondAccount = new BondAccount(1, 2, "bonds",
            [new BondAccountEntry(2, 1, _asOfDate, 10m, 10m, 7)], AccountLabel.Other);

        var cashAccount = new CurrencyAccount(1, 3, "cash", AccountLabel.Cash);
        cashAccount.Add(new CurrencyAccountEntry(3, 1, _asOfDate, 100, 100), false);

        SetupStockAccounts(stockAccount);
        SetupBondAccounts(bondAccount);
        SetupCurrencyAccounts(cashAccount);
        SetupStockDetails(Stock("US0378331005", "AAPL"), Stock("US5949181045", "MSFT"));
        SetupBondDetails(Bond(7, "Treasury 2030"));

        var result = await _service.GetDiversificationBreakdown(1, _asOfDate, TestContext.Current.CancellationToken);

        Assert.Equal(["Stocks", "Bonds", "Cash"], result.AssetClasses.Select(g => g.AssetClass));
        Assert.Equal(["AAPL", "MSFT"], result.AssetClasses[0].Holdings);
        Assert.Equal(["Treasury 2030"], result.AssetClasses[1].Holdings);
        Assert.Equal(["Cash"], result.AssetClasses[2].Holdings);
    }

    [Fact]
    public async Task GetDiversificationBreakdown_UnknownStockIsin_FallsBackToIsin()
    {
        var stockAccount = new StockAccount(1, 1, "stocks");
        stockAccount.Add(new StockAccountEntry(1, 1, _asOfDate, 10, 10, "US0378331005", InvestmentType.Stock), false);

        SetupStockAccounts(stockAccount);
        SetupBondAccounts();
        SetupCurrencyAccounts();
        // No matching StockDetails registered.

        var result = await _service.GetDiversificationBreakdown(1, _asOfDate, TestContext.Current.CancellationToken);

        var stocks = Assert.Single(result.AssetClasses);
        Assert.Equal("Stocks", stocks.AssetClass);
        Assert.Equal(["US0378331005"], stocks.Holdings);
    }

    [Fact]
    public async Task GetDiversificationBreakdown_UnknownBondId_FallsBackToPlaceholderName()
    {
        var bondAccount = new BondAccount(1, 1, "bonds",
            [new BondAccountEntry(1, 1, _asOfDate, 10m, 10m, 42)], AccountLabel.Other);

        SetupStockAccounts();
        SetupBondAccounts(bondAccount);
        SetupCurrencyAccounts();
        // No matching BondDetails registered → GetByIdAsync returns null.

        var result = await _service.GetDiversificationBreakdown(1, _asOfDate, TestContext.Current.CancellationToken);

        var bonds = Assert.Single(result.AssetClasses);
        Assert.Equal(["Bond #42"], bonds.Holdings);
    }

    [Fact]
    public async Task GetDiversificationBreakdown_SoldOutAndDuplicateHoldings_AreExcludedAndDeduped()
    {
        var sellDate = _asOfDate.AddDays(-5);

        var account1 = new StockAccount(1, 1, "stocks-a");
        account1.Add(new StockAccountEntry(1, 1, _asOfDate, 5, 5, "US0378331005", InvestmentType.Stock), false);
        // Fully sold position must not appear.
        account1.Add(new StockAccountEntry(1, 2, _asOfDate.AddDays(-10), 10, 10, "US38259P5089", InvestmentType.Stock), false);
        account1.Add(new StockAccountEntry(1, 3, sellDate, 0, -10, "US38259P5089", InvestmentType.Stock), false);

        // Same ISIN held in another account must be listed once.
        var account2 = new StockAccount(1, 2, "stocks-b");
        account2.Add(new StockAccountEntry(2, 1, _asOfDate, 3, 3, "US0378331005", InvestmentType.Stock), false);

        SetupStockAccounts(account1, account2);
        SetupBondAccounts();
        SetupCurrencyAccounts();
        SetupStockDetails(Stock("US0378331005", "AAPL"), Stock("US38259P5089", "GOOG"));

        var result = await _service.GetDiversificationBreakdown(1, _asOfDate, TestContext.Current.CancellationToken);

        var stocks = Assert.Single(result.AssetClasses);
        Assert.Equal(["AAPL"], stocks.Holdings);
    }
}