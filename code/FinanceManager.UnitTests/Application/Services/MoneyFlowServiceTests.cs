using FinanceManager.Application.Providers;
using FinanceManager.Application.Services;
using FinanceManager.Domain.Entities.Cash;
using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Entities.Shared.Accounts;
using FinanceManager.Domain.Entities.Stocks;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Repositories;
using FinanceManager.Domain.Repositories.Account;
using FinanceManager.Domain.Services;
using Microsoft.Extensions.Caching.Memory;
using Moq;

namespace FinanceManager.UnitTests.Application.Services;

public class MoneyFlowServiceTests
{
    private readonly DateTime _startDate = new(DateTime.UtcNow.Year - 1, 1, 1);
    private readonly DateTime _endDate = DateTime.UtcNow;

    private readonly MoneyFlowService _moneyFlowService;
    private readonly Mock<IFinancialAccountRepository> _financialAccountRepositoryMock = new();
    private readonly Mock<IStockPriceRepository> _stockRepository = new();
    private readonly Mock<ICurrencyExchangeService> _currencyExchangeService = new();
    private readonly Mock<IFinancialLabelsRepository> _financialLabelsRepositoryMock = new();
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

        _bankAccounts = [bankAccount1, bankAccount2];
        _financialAccountRepositoryMock.Setup(x => x.GetAccounts<BankAccount>(1, _startDate, _endDate))
                                      .Returns(_bankAccounts.ToAsyncEnumerable());

        StockAccount investmentAccount1 = new(1, 3, "testInvestmentAccount1");
        investmentAccount1.Add(new StockAccountEntry(1, 1, _startDate, 10, 10, "testStock1", InvestmentType.Stock));
        investmentAccount1.Add(new StockAccountEntry(1, 2, _endDate, 10, 10, "testStock2", InvestmentType.Stock));

        _investmentAccountAccounts = [investmentAccount1];

        _stockRepository.Setup(x => x.GetThisOrNextOlder("testStock1", It.IsAny<DateTime>()))
                        .ReturnsAsync(new StockPrice() { Currency = DefaultCurrency.PLN, Ticker = "testStock1", PricePerUnit = 2 });
        _stockRepository.Setup(x => x.GetThisOrNextOlder("testStock2", It.IsAny<DateTime>()))
                        .ReturnsAsync(new StockPrice() { Currency = DefaultCurrency.PLN, Ticker = "testStock2", PricePerUnit = 4 });
        _stockRepository.Setup(x => x.Get("testStock1", It.IsAny<DateTime>()))
                       .ReturnsAsync(new StockPrice() { Currency = DefaultCurrency.PLN, Ticker = "testStock1", PricePerUnit = 2 });
        _stockRepository.Setup(x => x.Get("testStock2", It.IsAny<DateTime>()))
                        .ReturnsAsync(new StockPrice() { Currency = DefaultCurrency.PLN, Ticker = "testStock2", PricePerUnit = 4 });

        _currencyExchangeService.Setup(x => x.GetExchangeRateAsync(It.IsAny<Currency>(), It.IsAny<Currency>(), It.IsAny<DateTime>())).ReturnsAsync(1);

        IMemoryCache cache = new MemoryCache(new MemoryCacheOptions());
        var stockPriceProvider = new StockPriceProvider(_stockRepository.Object, _currencyExchangeService.Object, cache);

        _moneyFlowService = new MoneyFlowService(_financialAccountRepositoryMock.Object, _financialLabelsRepositoryMock.Object, stockPriceProvider);
    }



    [Fact]
    public async Task GetNetWorth_ReturnsNetWorth()
    {
        // Arrange
        var userId = 1;
        DateTime date = _startDate.AddDays(-1);
        List<BankAccount> bankAccounts = [new(userId, 1, "Bank Account 1", [new(1, 1, date, 1000, 0)])];
        _financialAccountRepositoryMock.Setup(repo => repo.GetAccounts<BankAccount>(userId, It.IsAny<DateTime>(), It.IsAny<DateTime>())).Returns(bankAccounts.ToAsyncEnumerable());
        _financialAccountRepositoryMock.Setup(repo => repo.GetAccounts<StockAccount>(userId, It.IsAny<DateTime>(), It.IsAny<DateTime>())).Returns(_investmentAccountAccounts.ToAsyncEnumerable());

        // Act
        var result = await _moneyFlowService.GetNetWorth(userId, DefaultCurrency.PLN, date);

        // Assert
        Assert.Equal(1000, result);
    }

    [Fact]
    public async Task GetNetWorth_ReturnsTimeSeries()
    {
        // Arrange
        var userId = 1;
        List<BankAccount> bankAccounts =
        [
            new (userId, 1, "Bank Account 1",
            [
                new(1, 1, _endDate, 1000, 0)
            ])
        ];

        _financialAccountRepositoryMock.Setup(repo => repo.GetAccounts<BankAccount>(userId, It.IsAny<DateTime>(), It.IsAny<DateTime>())).Returns(bankAccounts.ToAsyncEnumerable());
        _financialAccountRepositoryMock.Setup(repo => repo.GetAccounts<StockAccount>(userId, It.IsAny<DateTime>(), It.IsAny<DateTime>())).Returns(_investmentAccountAccounts.ToAsyncEnumerable());

        // Act
        var result = await _moneyFlowService.GetNetWorth(userId, DefaultCurrency.PLN, _startDate, _endDate);

        // Assert
        Assert.NotEmpty(result);
        Assert.Equal(1000, result[_endDate]);
    }

    [Fact]
    public async Task GetIncome_ReturnsIncomeTimeSeries()
    {
        // Arrange
        var userId = 1;
        var account = new BankAccount(userId, 1, "Bank Account 1", AccountLabel.Cash);
        account.Add(new BankAccountEntry(1, 1, _startDate, 100, 100)); // Positive value change for income

        _financialAccountRepositoryMock.Setup(repo => repo.GetAccounts<BankAccount>(userId, It.IsAny<DateTime>(), It.IsAny<DateTime>())).Returns(new[] { account }.ToAsyncEnumerable());

        // Act
        var result = await _moneyFlowService.GetIncome(userId, DefaultCurrency.PLN, _startDate, _endDate);

        // Assert
        Assert.NotEmpty(result);
        Assert.Contains(result, ts => ts.Value > 0);
    }

    [Fact]
    public async Task GetSpending_ReturnsSpendingTimeSeries()
    {
        // Arrange
        var userId = 1;
        var account = new BankAccount(userId, 1, "Bank Account 1", AccountLabel.Cash);
        account.Add(new BankAccountEntry(1, 1, _startDate, -50, -50)); // Negative value change for spending

        _financialAccountRepositoryMock.Setup(repo => repo.GetAccounts<BankAccount>(userId, It.IsAny<DateTime>(), It.IsAny<DateTime>())).Returns(new[] { account }.ToAsyncEnumerable());

        // Act
        var result = await _moneyFlowService.GetSpending(userId, DefaultCurrency.PLN, _startDate, _endDate);

        // Assert
        Assert.NotEmpty(result);
        Assert.Contains(result, ts => ts.Value < 0);
    }


    [Fact]
    public async Task GetLabelsValue_ReturnsLabelsValue()
    {
        // Arrange
        var userId = 1;
        var label = new FinancialLabel { Id = 1, Name = "Salary" };
        _financialLabelsRepositoryMock.Setup(repo => repo.GetLabels()).Returns(new[] { label }.ToAsyncEnumerable());

        var account = new BankAccount(userId, 1, "Bank Account 1", AccountLabel.Cash);
        account.Add(new BankAccountEntry(1, 1, _startDate, 500, 500) { Labels = [label] });

        _financialAccountRepositoryMock.Setup(repo => repo.GetAccounts<BankAccount>(userId, It.IsAny<DateTime>(), It.IsAny<DateTime>())).Returns(new[] { account }.ToAsyncEnumerable());

        // Act
        var result = await _moneyFlowService.GetLabelsValue(userId, _startDate, _endDate);

        // Assert
        Assert.NotEmpty(result);
        Assert.Equal(500, result.First(x => x.Name == label.Name).Value);
    }

    [Fact]
    public async Task GetInvestmentRate_ReturnsInvestmentRate()
    {
        // Arrange
        var userId = 1;
        var salaryLabel = new FinancialLabel { Id = 1, Name = "Salary" };
        _financialLabelsRepositoryMock.Setup(repo => repo.GetLabels()).Returns(new[] { salaryLabel }.ToAsyncEnumerable());

        var bankAccount = new BankAccount(userId, 1, "Bank Account 1", AccountLabel.Cash);
        bankAccount.Add(new BankAccountEntry(1, 1, _startDate, 1000, 1000) { Labels = [salaryLabel] }, false);

        var stockAccount = new StockAccount(userId, 2, "Stock Account 1");
        stockAccount.Add(new StockAccountEntry(1, 1, _startDate, 10, 10, "TICKER", InvestmentType.Stock), false);

        _financialAccountRepositoryMock.Setup(repo => repo.GetAccounts<BankAccount>(userId, It.IsAny<DateTime>(), It.IsAny<DateTime>())).Returns(new[] { bankAccount }.ToAsyncEnumerable());
        _financialAccountRepositoryMock.Setup(repo => repo.GetAccounts<StockAccount>(userId, It.IsAny<DateTime>(), It.IsAny<DateTime>())).Returns(new[] { stockAccount }.ToAsyncEnumerable());

        // Act
        var result = await _moneyFlowService.GetInvestmentRate(userId, _startDate, _endDate).ToListAsync(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(result);
        Assert.Equal(1000, result.First().Salary);
        Assert.Equal(10, result.First().InvestmentsChange);
    }
}