using FinanceManager.Application.Services;
using FinanceManager.Domain.Entities;
using FinanceManager.Domain.Entities.Accounts;
using FinanceManager.Domain.Entities.Accounts.Entries;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Repositories;
using FinanceManager.Domain.Repositories.Account;
using FinanceManager.Domain.Services;
using Moq;

namespace FinanceManager.UnitTests.Services;

public class MoneyFlowServiceTests
{
    private readonly DateTime _startDate = new(2020, 1, 1);
    private readonly DateTime _endDate = new(2020, 1, 31);

    private readonly MoneyFlowService _moneyFlowService;
    private readonly Mock<IFinancialAccountRepository> _financialAccountRepositoryMock = new();
    private readonly Mock<IStockPriceRepository> _stockRepository = new();
    private readonly Mock<ICurrencyExchangeService> _currencyExchangeService = new();
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
        _financialAccountRepositoryMock.Setup(x => x.GetAccounts<StockAccount>(1, _startDate, _endDate))
                                      .Returns(_investmentAccountAccounts.ToAsyncEnumerable());

        _stockRepository.Setup(x => x.GetThisOrNextOlder("testStock1", It.IsAny<DateTime>()))
                        .ReturnsAsync(new StockPrice() { Currency = DefaultCurrency.PLN, Ticker = "testStock1", PricePerUnit = 2 });
        _stockRepository.Setup(x => x.GetThisOrNextOlder("testStock2", It.IsAny<DateTime>()))
                        .ReturnsAsync(new StockPrice() { Currency = DefaultCurrency.PLN, Ticker = "testStock2", PricePerUnit = 4 });
        _stockRepository.Setup(x => x.Get("testStock1", It.IsAny<DateTime>()))
                       .ReturnsAsync(new StockPrice() { Currency = DefaultCurrency.PLN, Ticker = "testStock1", PricePerUnit = 2 });
        _stockRepository.Setup(x => x.Get("testStock2", It.IsAny<DateTime>()))
                        .ReturnsAsync(new StockPrice() { Currency = DefaultCurrency.PLN, Ticker = "testStock2", PricePerUnit = 4 });

        _currencyExchangeService.Setup(x => x.GetExchangeRateAsync(It.IsAny<Currency>(), It.IsAny<Currency>(), It.IsAny<DateTime>())).ReturnsAsync(1);

        _moneyFlowService = new MoneyFlowService(_financialAccountRepositoryMock.Object, _stockRepository.Object, _currencyExchangeService.Object, null);
    }



    [Fact]
    public async Task GetNetWorth_ReturnsNetWorth()
    {
        // Arrange
        var userId = 1;
        DateTime date = new(2020, 12, 31);
        List<BankAccount> bankAccounts = [new(userId, 1, "Bank Account 1", [new(1, 1, date, 1000, 0)])];
        _financialAccountRepositoryMock.Setup(repo => repo.GetAccounts<BankAccount>(userId, date.Date, date)).Returns(bankAccounts.ToAsyncEnumerable());
        _financialAccountRepositoryMock.Setup(repo => repo.GetAccounts<StockAccount>(userId, date.Date, date)).Returns(_investmentAccountAccounts.ToAsyncEnumerable());

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
        DateTime start = new(2023, 1, 1);
        DateTime end = new(2023, 12, 31);
        List<BankAccount> bankAccounts =
            [
                new (userId, 1, "Bank Account 1",
                [
                    new BankAccountEntry(1, 1, end, 1000, 0)
                ])
            ];
        _financialAccountRepositoryMock.Setup(repo => repo.GetAccounts<BankAccount>(userId, It.IsAny<DateTime>(), It.IsAny<DateTime>())).Returns(bankAccounts.ToAsyncEnumerable());
        _financialAccountRepositoryMock.Setup(repo => repo.GetAccounts<StockAccount>(userId, It.IsAny<DateTime>(), It.IsAny<DateTime>())).Returns(_investmentAccountAccounts.ToAsyncEnumerable());

        // Act
        var result = await _moneyFlowService.GetNetWorth(userId, DefaultCurrency.PLN, start, end);

        // Assert
        Assert.NotEmpty(result);
        Assert.Equal(1000, result[end]);
    }
}
