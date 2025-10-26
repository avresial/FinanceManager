using FinanceManager.Application.Providers;
using FinanceManager.Application.Services;
using FinanceManager.Domain.Entities;
using FinanceManager.Domain.Entities.Accounts;
using FinanceManager.Domain.Entities.Accounts.Entries;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Repositories;
using FinanceManager.Domain.Repositories.Account;
using FinanceManager.Domain.Services;
using Microsoft.Extensions.Caching.Memory;
using Moq;

namespace FinanceManager.UnitTests.Services;
public class AssetsServiceTests
{
    private readonly DateTime _startDate = new(2020, 1, 1);
    private readonly DateTime _endDate = new(2020, 1, 31);
    private readonly decimal _totalAssetsValue = 0;
    private readonly AssetsService _assetsService;

    private readonly Mock<IFinancialAccountRepository> _financialAccountRepositoryMock = new();
    private readonly Mock<IStockPriceRepository> _stockRepository = new();
    private readonly Mock<ICurrencyExchangeService> _currencyExchangeService = new();

    private readonly List<BankAccount> _bankAccounts;
    private readonly List<StockAccount> _investmentAccountAccounts;

    public AssetsServiceTests()
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

        _investmentAccountAccounts = new List<StockAccount> { investmentAccount1 };
        _financialAccountRepositoryMock.Setup(x => x.GetAccounts<StockAccount>(1, _startDate, _endDate))
                                      .Returns(_investmentAccountAccounts.ToAsyncEnumerable());

        _totalAssetsValue = 80;
        _stockRepository.Setup(x => x.GetThisOrNextOlder("testStock1", It.IsAny<DateTime>()))
                   .ReturnsAsync(new StockPrice() { Currency = DefaultCurrency.PLN, Ticker = "testStock1", PricePerUnit = 2 });
        _stockRepository.Setup(x => x.GetThisOrNextOlder("testStock2", It.IsAny<DateTime>()))
                        .ReturnsAsync(new StockPrice() { Currency = DefaultCurrency.PLN, Ticker = "testStock2", PricePerUnit = 2 });
        _stockRepository.Setup(x => x.Get("testStock1", It.IsAny<DateTime>()))
                       .ReturnsAsync(new StockPrice() { Currency = DefaultCurrency.PLN, Ticker = "testStock1", PricePerUnit = 2 });
        _stockRepository.Setup(x => x.Get("testStock2", It.IsAny<DateTime>()))
                        .ReturnsAsync(new StockPrice() { Currency = DefaultCurrency.PLN, Ticker = "testStock2", PricePerUnit = 2 });

        _currencyExchangeService.Setup(x => x.GetExchangeRateAsync(It.IsAny<Currency>(), It.IsAny<Currency>(), It.IsAny<DateTime>())).ReturnsAsync(1);
        _currencyExchangeService.Setup(x => x.GetPricePerUnit(It.IsAny<StockPrice>(), It.IsAny<Currency>(), It.IsAny<DateTime>())).ReturnsAsync(2);

        IMemoryCache cache = new MemoryCache(new MemoryCacheOptions());
        var stockPriceProvider = new StockPriceProvider(_stockRepository.Object, _currencyExchangeService.Object, cache);

        _assetsService = new(_financialAccountRepositoryMock.Object, stockPriceProvider);
    }

    [Fact]
    public async Task GetAssetsPerAcount()
    {
        // Arrange

        // Act
        var result = await _assetsService.GetEndAssetsPerAccount(1, DefaultCurrency.PLN, _endDate).ToListAsync(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(_bankAccounts.Count + _investmentAccountAccounts.Count, result.Count);
        Assert.Equal(_totalAssetsValue, result.Sum(x => x.Value));
    }

    [Fact]
    public async Task GetEndAssetsPerType()
    {
        // Arrange

        // Act
        var result = await _assetsService.GetEndAssetsPerType(1, DefaultCurrency.PLN, _endDate).ToListAsync(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal(_totalAssetsValue, result.Sum(x => x.Value));
    }

    [Fact]
    public async Task GetAssetsPerTypeTimeseries()
    {
        // Arrange

        // Act
        var result = await _assetsService.GetAssetsTimeSeries(1, DefaultCurrency.PLN, _startDate, _endDate);

        // Assert
        Assert.NotEmpty(result);
        Assert.Equal(_totalAssetsValue, result.First(x => x.DateTime == _endDate).Value);
    }

    [Theory]
    [InlineData(InvestmentType.Bond, 0)]
    [InlineData(InvestmentType.Stock, 40)]
    [InlineData(InvestmentType.Cash, 40)]
    [InlineData(InvestmentType.Property, 0)]
    public async Task GetAssetsPerTypeTimeseries_TypeAsParameter(InvestmentType investmentType, decimal finalValue)
    {
        // Arrange
        // Act
        var result = await _assetsService.GetAssetsTimeSeries(1, DefaultCurrency.PLN, _startDate, _endDate, investmentType);

        // Assert
        Assert.Equal(result.First().Value, finalValue);
        Assert.Equal(result.First(x => x.DateTime == _endDate).Value, finalValue);
    }

}
