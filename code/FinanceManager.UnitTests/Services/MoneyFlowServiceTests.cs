using FinanceManager.Application.Services;
using FinanceManager.Domain.Entities;
using FinanceManager.Domain.Entities.Accounts;
using FinanceManager.Domain.Entities.Accounts.Entries;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Repositories;
using FinanceManager.Domain.Repositories.Account;
using Moq;

namespace FinanceManager.UnitTests.Services;

public class MoneyFlowServiceTests
{
    private readonly DateTime _startDate = new(2020, 1, 1);
    private readonly DateTime _endDate = new(2020, 1, 31);
    private readonly decimal _totalAssetsValue = 0;

    private readonly MoneyFlowService _moneyFlowService;
    private readonly Mock<IFinancalAccountRepository> _financialAccountRepositoryMock = new();
    private readonly Mock<IStockRepository> _stockRepository = new();
    private readonly List<BankAccount> _bankAccounts;
    private readonly List<StockAccount> _investmentAccountAccounts;

    public MoneyFlowServiceTests()
    {
        BankAccount bankAccount1 = new(1, 1, "testBank1", AccountLabel.Cash);
        bankAccount1.Add(new BankAccountEntry(1, 1, _startDate.AddYears(-1), 10, 10));
        bankAccount1.Add(new BankAccountEntry(1, 2, _startDate, 20, 10));
        bankAccount1.Add(new BankAccountEntry(1, 3, _startDate.AddDays(1), 30, 10));

        BankAccount bankAccount2 = new(1, 2, "testBank2", AccountLabel.Cash);
        bankAccount2.Add(new BankAccountEntry(1, 1, _endDate, 10, 10));

        _bankAccounts = new List<BankAccount> { bankAccount1, bankAccount2 };
        _financialAccountRepositoryMock.Setup(x => x.GetAccounts<BankAccount>(1, _startDate, _endDate))
                                      .ReturnsAsync(_bankAccounts);

        StockAccount investmentAccount1 = new(1, 3, "testInvestmentAccount1");
        investmentAccount1.Add(new StockAccountEntry(1, 1, _startDate, 10, 10, "testStock1", InvestmentType.Stock));
        investmentAccount1.Add(new StockAccountEntry(1, 2, _endDate, 10, 10, "testStock2", InvestmentType.Stock));

        _investmentAccountAccounts = new List<StockAccount> { investmentAccount1 };
        _financialAccountRepositoryMock.Setup(x => x.GetAccounts<StockAccount>(1, _startDate, _endDate))
                                      .ReturnsAsync(_investmentAccountAccounts);

        _totalAssetsValue = 100;

        _stockRepository.Setup(x => x.GetStockPrice("testStock1", It.IsAny<DateTime>()))
                        .ReturnsAsync(new StockPrice() { Currency = "PLN", Ticker = "AnyTicker", PricePerUnit = 2 });
        _stockRepository.Setup(x => x.GetStockPrice("testStock2", It.IsAny<DateTime>()))
                        .ReturnsAsync(new StockPrice() { Currency = "PLN", Ticker = "AnyTicker", PricePerUnit = 4 });

        _moneyFlowService = new MoneyFlowService(_financialAccountRepositoryMock.Object, _stockRepository.Object);
    }

    [Fact]
    public async Task GetAssetsPerAcount()
    {
        // Arrange

        // Act
        var result = await _moneyFlowService.GetEndAssetsPerAccount(1, _startDate, _endDate);

        // Assert
        Assert.Equal(_bankAccounts.Count + _investmentAccountAccounts.Count, result.Count);
        Assert.Equal(_totalAssetsValue, result.Sum(x => x.Value));
    }

    [Fact]
    public async Task GetEndAssetsPerType()
    {
        // Arrange

        // Act
        var result = await _moneyFlowService.GetEndAssetsPerType(1, _startDate, _endDate);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal(_totalAssetsValue, result.Sum(x => x.Value));
    }

    [Fact]
    public async Task GetAssetsPerTypeTimeseries()
    {
        // Arrange

        // Act
        var result = await _moneyFlowService.GetAssetsTimeSeries(1, _startDate, _endDate);

        // Assert
        Assert.NotEmpty(result);
        Assert.Equal(_totalAssetsValue, result.First(x => x.DateTime == _endDate).Value);
    }

    [Theory]
    [InlineData(InvestmentType.Bond, 0)]
    [InlineData(InvestmentType.Stock, 60)]
    [InlineData(InvestmentType.Cash, 40)]
    [InlineData(InvestmentType.Property, 0)]
    public async Task GetAssetsPerTypeTimeseries_TypeAsParameter(InvestmentType investmentType, decimal finalValue)
    {
        // Arrange
        // Act
        var result = await _moneyFlowService.GetAssetsTimeSeries(1, _startDate, _endDate, investmentType);

        // Assert
        Assert.Equal(result.First().Value, finalValue);
        Assert.Equal(result.First(x => x.DateTime == _endDate).Value, finalValue);
    }


    [Fact]
    public async Task GetNetWorth_ReturnsNetWorth()
    {
        // Arrange
        var userId = 1;
        var date = new DateTime(2023, 12, 31);
        List<BankAccount> bankAccounts = [new(userId, 1, "Bank Account 1", [new(1, 1, date, 1000, 0)])];
        _financialAccountRepositoryMock.Setup(repo => repo.GetAccounts<BankAccount>(userId, date.Date, date)).ReturnsAsync(bankAccounts);

        // Act
        var result = await _moneyFlowService.GetNetWorth(userId, date);

        // Assert
        Assert.Equal(1000, result);
    }

    [Fact]
    public async Task GetNetWorth_ReturnsTimeSeries()
    {
        // Arrange
        var userId = 1;
        var start = new DateTime(2023, 1, 1);
        var end = new DateTime(2023, 12, 31);
        List<BankAccount> bankAccounts =
            [
                new (userId, 1, "Bank Account 1",
                [
                    new BankAccountEntry(1, 1, end, 1000, 0)
                ])
            ];
        _financialAccountRepositoryMock.Setup(repo => repo.GetAccounts<BankAccount>(userId, end, end)).ReturnsAsync(bankAccounts);

        // Act
        var result = await _moneyFlowService.GetNetWorth(userId, start, end);

        // Assert
        Assert.NotEmpty(result);
        Assert.Equal(1000, result[end]);
    }
}
