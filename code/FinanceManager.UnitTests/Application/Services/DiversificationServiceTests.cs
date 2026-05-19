using FinanceManager.Application.Services;
using FinanceManager.Domain.Entities.Bonds;
using FinanceManager.Domain.Entities.FinancialAccounts.Currencies;
using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.Entities.Stocks;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Repositories.Account;
using Moq;

namespace FinanceManager.UnitTests.Application.Services;

[Collection("Application")]
[Trait("Category", "Unit")]
public class DiversificationServiceTests
{
    private readonly Mock<IFinancialAccountRepository> _repositoryMock = new();
    private readonly DiversificationService _service;
    private readonly DateTime _asOfDate = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public DiversificationServiceTests() => _service = new(_repositoryMock.Object);

    private void SetupStockAccounts(params StockAccount[] accounts) =>
        _repositoryMock.Setup(x => x.GetAccounts<StockAccount>(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(accounts.ToAsyncEnumerable());

    private void SetupBondAccounts(params BondAccount[] accounts) =>
        _repositoryMock.Setup(x => x.GetAccounts<BondAccount>(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(accounts.ToAsyncEnumerable());

    private void SetupCurrencyAccounts(params CurrencyAccount[] accounts) =>
        _repositoryMock.Setup(x => x.GetAccounts<CurrencyAccount>(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(accounts.ToAsyncEnumerable());

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
}
