using FinanceManager.Application.Services;
using FinanceManager.Domain.Entities;
using FinanceManager.Domain.Entities.Accounts;
using FinanceManager.Domain.Entities.Accounts.Entries;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Repositories.Account;
using FinanceManager.Domain.Services;
using Moq;

namespace FinanceManager.UnitTests.Application.Services;

public class AssetsServiceStockTests
{
    private readonly Mock<IFinancialAccountRepository> _financialAccountRepositoryMock = new();
    private readonly Mock<IStockPriceProvider> _stockPriceProviderMock = new();
    private readonly DateTime _start = new(DateTime.UtcNow.Year, 1, 1);
    private readonly DateTime _end = new(DateTime.UtcNow.Year, 1, 31);
    private readonly AssetsServiceStock _assetsServiceStock;

    public AssetsServiceStockTests() => _assetsServiceStock = new(_financialAccountRepositoryMock.Object, _stockPriceProviderMock.Object);

    [Fact]
    public async Task IsAnyAccountWithAssets_ReturnsTrue_WhenRepositoryHasStockAccountWithAssets()
    {
        // arrange
        StockAccount account = new(1, 1, "stock-acc");
        account.Add(new StockAccountEntry(1, 1, _end, 10, 0, "TICKER1", InvestmentType.Stock), false);

        _financialAccountRepositoryMock.Setup(x => x.GetAccounts<StockAccount>(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
        .Returns(new[] { account }.ToAsyncEnumerable());

        // act
        var result = await _assetsServiceStock.IsAnyAccountWithAssets(1);

        // assert
        Assert.True(result);
    }

    [Fact]
    public async Task GetEndAssetsPerAccount_CalculatesValue_UsingStockPriceProvider()
    {
        // arrange
        var account = new StockAccount(1, 1, "inv-acc");
        account.Add(new StockAccountEntry(1, 1, _end, 5, 0, "TICKER1", InvestmentType.Stock), false);
        account.Add(new StockAccountEntry(1, 2, _end, 3, 0, "TICKER2", InvestmentType.Stock), false);

        _financialAccountRepositoryMock.Setup(x => x.GetAccounts<StockAccount>(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
        .Returns(new[] { account }.ToAsyncEnumerable());

        _stockPriceProviderMock.Setup(x => x.GetPricePerUnitAsync("TICKER1", It.IsAny<Currency>(), It.IsAny<DateTime>())).ReturnsAsync(2m);
        _stockPriceProviderMock.Setup(x => x.GetPricePerUnitAsync("TICKER2", It.IsAny<Currency>(), It.IsAny<DateTime>())).ReturnsAsync(3m);

        // act
        var list = await _assetsServiceStock.GetEndAssetsPerAccount(1, new Currency(0, "PLN", "PLN"), _end).ToListAsync(cancellationToken: TestContext.Current.CancellationToken);

        // assert
        Assert.Single(list);
        var result = list[0];
        // expected value =5*2 +3*3 =10 +9 =19
        Assert.Equal("inv-acc", result.Name);
        Assert.Equal(19m, result.Value);
    }

    [Fact]
    public async Task GetEndAssetsPerType_AggregatesByInvestmentType()
    {
        // arrange
        var account1 = new StockAccount(1, 1, "inv-1");
        account1.Add(new StockAccountEntry(1, 1, _end, 4, 0, "T1", InvestmentType.Stock), false);

        var account2 = new StockAccount(1, 2, "inv-2");
        account2.Add(new StockAccountEntry(1, 1, _end, 6, 0, "T2", InvestmentType.Bond), false);

        _financialAccountRepositoryMock.Setup(x => x.GetAccounts<StockAccount>(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
        .Returns(new[] { account1, account2 }.ToAsyncEnumerable());

        _stockPriceProviderMock.Setup(x => x.GetPricePerUnitAsync(It.IsAny<string>(), It.IsAny<Currency>(), It.IsAny<DateTime>())).ReturnsAsync(1m);

        // act
        var results = await _assetsServiceStock.GetEndAssetsPerType(1, new Currency(0, "PLN", "PLN"), _end).ToListAsync(cancellationToken: TestContext.Current.CancellationToken);

        // assert
        Assert.Equal(2, results.Count);
        Assert.Contains(results, r => r.Name == InvestmentType.Stock.ToString() && r.Value == 4m);
        Assert.Contains(results, r => r.Name == InvestmentType.Bond.ToString() && r.Value == 6m);
    }

    [Fact]
    public async Task GetAssetsTimeSeries_ReturnsResult_WhenDatesInitialized()
    {
        // arrange
        var account = new StockAccount(1, 1, "inv-acc");
        account.Add(new StockAccountEntry(1, 1, _start, 2, 0, "T1", InvestmentType.Stock), false);

        _financialAccountRepositoryMock.Setup(x => x.GetAccounts<StockAccount>(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
        .Returns(new[] { account }.ToAsyncEnumerable());

        _stockPriceProviderMock.Setup(x => x.GetPricePerUnitAsync(It.IsAny<string>(), It.IsAny<Currency>(), It.IsAny<DateTime>())).ReturnsAsync(1m);

        // act
        var series = await _assetsServiceStock.GetAssetsTimeSeries(1, DefaultCurrency.PLN, _start, _end);

        // assert
        Assert.NotEmpty(series);
    }
}