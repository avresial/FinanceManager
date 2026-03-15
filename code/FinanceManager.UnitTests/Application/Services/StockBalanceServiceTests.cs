using FinanceManager.Application.Services.Stocks;
using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Entities.Stocks;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Repositories.Account;
using FinanceManager.Domain.Services;
using Moq;

namespace FinanceManager.UnitTests.Application.Services;

[Collection("Application")]
[Trait("Category", "Unit")]
public class StockBalanceServiceTests
{
    private readonly Mock<IFinancialAccountRepository> _financialAccountRepositoryMock = new();
    private readonly Mock<IStockPriceProvider> _stockPriceProviderMock = new();
    private readonly StockBalanceService _service;

    public StockBalanceServiceTests()
    {
        _service = new StockBalanceService(_financialAccountRepositoryMock.Object, _stockPriceProviderMock.Object);
    }

    [Fact]
    public async Task GetClosingBalance_ReturnsPricedValuePerTicker()
    {
        var userId = 1;
        DateTime startDate = new(2024, 1, 1);
        DateTime endDate = new(2024, 1, 3);

        var account = new StockAccount(userId, 1, "Stocks",
        [
            new StockAccountEntry(1, 2, new DateTime(2024, 1, 3), 3, 1, "AAPL", InvestmentType.Stock),
            new StockAccountEntry(1, 1, new DateTime(2024, 1, 1), 2, 2, "AAPL", InvestmentType.Stock)
        ]);

        _financialAccountRepositoryMock.Setup(repo => repo.GetAccounts<StockAccount>(userId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                                       .Returns(new[] { account }.ToAsyncEnumerable());
        _stockPriceProviderMock.Setup(x => x.GetPricePerUnitAsync("AAPL", DefaultCurrency.PLN, It.IsAny<DateTime>())).ReturnsAsync(10);

        var result = await _service.GetClosingBalance(userId, DefaultCurrency.PLN, startDate, endDate);

        Assert.Equal(3, result.Count);
        Assert.Equal(20, result.Single(x => x.DateTime == startDate).Value);
        Assert.Equal(20, result.Single(x => x.DateTime == startDate.AddDays(1)).Value);
        Assert.Equal(30, result.Single(x => x.DateTime == endDate).Value);
    }

    [Fact]
    public async Task GetNetCashFlow_ReturnsTransactionValueSeries()
    {
        var userId = 1;
        DateTime startDate = new(2024, 1, 1);
        DateTime endDate = new(2024, 1, 2);

        var account = new StockAccount(userId, 1, "Stocks",
        [
            new StockAccountEntry(1, 2, endDate, 1, -1, "AAPL", InvestmentType.Stock),
            new StockAccountEntry(1, 1, startDate, 2, 2, "AAPL", InvestmentType.Stock)
        ]);

        _financialAccountRepositoryMock.Setup(repo => repo.GetAccounts<StockAccount>(userId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                                       .Returns(new[] { account }.ToAsyncEnumerable());
        _stockPriceProviderMock.Setup(x => x.GetPricePerUnitAsync("AAPL", DefaultCurrency.PLN, startDate)).ReturnsAsync(10);
        _stockPriceProviderMock.Setup(x => x.GetPricePerUnitAsync("AAPL", DefaultCurrency.PLN, endDate)).ReturnsAsync(12);

        var result = await _service.GetNetCashFlow(userId, DefaultCurrency.PLN, startDate, endDate);

        Assert.Equal(2, result.Count);
        Assert.Equal(20, result.Single(x => x.DateTime == startDate).Value);
        Assert.Equal(-12, result.Single(x => x.DateTime == endDate).Value);
    }
}