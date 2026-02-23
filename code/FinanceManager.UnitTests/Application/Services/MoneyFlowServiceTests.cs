using FinanceManager.Application.Providers;
using FinanceManager.Application.Services;
using FinanceManager.Domain.Entities.Bonds;
using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Entities.FinancialAccounts.Currencies;
using FinanceManager.Domain.Entities.Shared.Accounts;
using FinanceManager.Domain.Entities.Stocks;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Repositories;
using FinanceManager.Domain.Repositories.Account;
using FinanceManager.Domain.Services;
using Microsoft.Extensions.Caching.Memory;
using Moq;

namespace FinanceManager.UnitTests.Application.Services;

[Collection("Application")]
[Trait("Category", "Unit")]
public class MoneyFlowServiceTests
{
    private readonly DateTime _startDate = new(DateTime.UtcNow.Year - 1, 1, 1);
    private readonly DateTime _endDate = DateTime.UtcNow;

    private readonly MoneyFlowService _moneyFlowService;
    private readonly Mock<IFinancialAccountRepository> _financialAccountRepositoryMock = new();
    private readonly Mock<IStockPriceRepository> _stockRepository = new();
    private readonly Mock<ICurrencyExchangeService> _currencyExchangeService = new();
    private readonly Mock<IFinancialLabelsRepository> _financialLabelsRepositoryMock = new();
    private readonly List<CurrencyAccount> _currencyAccounts;
    private readonly List<StockAccount> _investmentAccountAccounts;

    public MoneyFlowServiceTests()
    {
        CurrencyAccount currencyAccount1 = new(1, 1, "testCurrency1", AccountLabel.Cash);
        currencyAccount1.Add(new CurrencyAccountEntry(1, 1, _startDate.AddYears(-1), 10, 10));
        currencyAccount1.Add(new CurrencyAccountEntry(1, 2, _startDate, 20, 10));
        currencyAccount1.Add(new CurrencyAccountEntry(1, 3, _startDate.AddDays(1), 30, 10));

        CurrencyAccount currencyAccount2 = new(1, 2, "testCurrency2", AccountLabel.Cash);
        currencyAccount2.Add(new CurrencyAccountEntry(1, 1, _endDate, 10, 10));

        _currencyAccounts = [currencyAccount1, currencyAccount2];
        _financialAccountRepositoryMock.Setup(x => x.GetAccounts<CurrencyAccount>(1, _startDate, _endDate))
                                      .Returns(_currencyAccounts.ToAsyncEnumerable());

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
        List<CurrencyAccount> currencyAccounts = [new(userId, 1, "Currency Account 1", [new(1, 1, date, 1000, 0)])];
        List<BondAccount> bondAccounts = [];
        _financialAccountRepositoryMock.Setup(repo => repo.GetAccounts<CurrencyAccount>(userId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(currencyAccounts.ToAsyncEnumerable());
        _financialAccountRepositoryMock.Setup(repo => repo.GetAccounts<StockAccount>(userId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(_investmentAccountAccounts.ToAsyncEnumerable());
        _financialAccountRepositoryMock.Setup(repo => repo.GetAccounts<BondAccount>(userId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(bondAccounts.ToAsyncEnumerable());

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
        List<CurrencyAccount> currencyAccounts =
        [
            new (userId, 1, "Currency Account 1",
            [
                new(1, 1, _endDate, 1000, 0)
            ])
        ];
        List<BondAccount> bondAccounts = [];

        _financialAccountRepositoryMock.Setup(repo => repo.GetAccounts<CurrencyAccount>(userId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(currencyAccounts.ToAsyncEnumerable());
        _financialAccountRepositoryMock.Setup(repo => repo.GetAccounts<BondAccount>(userId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(bondAccounts.ToAsyncEnumerable());
        _financialAccountRepositoryMock.Setup(repo => repo.GetAccounts<StockAccount>(userId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(_investmentAccountAccounts.ToAsyncEnumerable());

        // Act
        var result = await _moneyFlowService.GetNetWorth(userId, DefaultCurrency.PLN, _startDate, _endDate);

        // Assert
        Assert.NotEmpty(result);
        // Currency account: 1000, Stock account: testStock1 (10 * 2) + testStock2 (10 * 4) = 20 + 40 = 60
        Assert.Equal(1060m, result[_endDate]);
    }

    [Fact]
    public async Task GetLabelsValue_ReturnsLabelsValue()
    {
        // Arrange
        var userId = 1;
        var label = new FinancialLabel { Id = 1, Name = "Salary" };
        _financialLabelsRepositoryMock.Setup(repo => repo.GetLabels(It.IsAny<CancellationToken>())).Returns(new[] { label }.ToAsyncEnumerable());

        var account = new CurrencyAccount(userId, 1, "Currency Account 1", AccountLabel.Cash);
        account.Add(new CurrencyAccountEntry(1, 1, _startDate, 500, 500) { Labels = [label] });

        _financialAccountRepositoryMock.Setup(repo => repo.GetAccounts<CurrencyAccount>(userId, It.IsAny<DateTime>(), It.IsAny<DateTime>())).Returns(new[] { account }.ToAsyncEnumerable());

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
        _financialLabelsRepositoryMock.Setup(repo => repo.GetLabels(It.IsAny<CancellationToken>())).Returns(new[] { salaryLabel }.ToAsyncEnumerable());

        var currencyAccount = new CurrencyAccount(userId, 1, "Currency Account 1", AccountLabel.Cash);
        currencyAccount.Add(new CurrencyAccountEntry(1, 1, _startDate, 1000, 1000) { Labels = [salaryLabel] }, false);

        var stockAccount = new StockAccount(userId, 2, "Stock Account 1");
        stockAccount.Add(new StockAccountEntry(1, 1, _startDate, 10, 10, "TICKER", InvestmentType.Stock), false);

        // Setup mock for TICKER
        _stockRepository.Setup(x => x.GetThisOrNextOlder("TICKER", It.IsAny<DateTime>()))
            .ReturnsAsync(new StockPrice() { Currency = DefaultCurrency.PLN, Ticker = "TICKER", PricePerUnit = 1m });

        _financialAccountRepositoryMock.Setup(repo => repo.GetAccounts<CurrencyAccount>(userId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(new[] { currencyAccount }.ToAsyncEnumerable());
        _financialAccountRepositoryMock.Setup(repo => repo.GetAccounts<StockAccount>(userId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(new[] { stockAccount }.ToAsyncEnumerable());

        // Act
        var result = await _moneyFlowService.GetInvestmentRate(userId, _startDate, _endDate).ToListAsync(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(result);
        Assert.Equal(1000, result.First().Salary);
        Assert.Equal(10, result.First().InvestmentsChange);
    }

    [Fact]
    public async Task GetNetWorth_MixedPortfolio_ShouldAggregateAllAccountTypes()
    {
        // Arrange
        var userId = 1;
        var date = new DateTime(2023, 6, 15, 0, 0, 0, DateTimeKind.Utc);

        // Currency account: 5000
        var currencyAccount = new CurrencyAccount(userId, 1, "Checking", AccountLabel.Cash);
        currencyAccount.Add(new CurrencyAccountEntry(1, 1, date.AddDays(-10), 5000m, 5000m) { Labels = [] });

        // Stock account: 10 shares at 100 per share = 1000
        var stockAccount = new StockAccount(userId, 2, "Stocks");
        stockAccount.Add(new StockAccountEntry(2, 1, date.AddDays(-5), 10m, 10m, "MSFT", InvestmentType.Stock));

        // Bond account: 3000
        var bondAccount = new BondAccount(userId, 3, "Bonds", AccountLabel.Other);
        bondAccount.Add(new BondAccountEntry(3, 1, date.AddDays(-7), 3000m, 3000m, 1));

        _financialAccountRepositoryMock.Setup(repo => repo.GetAccounts<CurrencyAccount>(userId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(new[] { currencyAccount }.ToAsyncEnumerable());
        _financialAccountRepositoryMock.Setup(repo => repo.GetAccounts<StockAccount>(userId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(new[] { stockAccount }.ToAsyncEnumerable());
        _financialAccountRepositoryMock.Setup(repo => repo.GetAccounts<BondAccount>(userId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(new[] { bondAccount }.ToAsyncEnumerable());

        _stockRepository.Setup(x => x.GetThisOrNextOlder("MSFT", It.IsAny<DateTime>()))
            .ReturnsAsync(new StockPrice { Currency = DefaultCurrency.PLN, Ticker = "MSFT", PricePerUnit = 100m });

        // Act
        var result = await _moneyFlowService.GetNetWorth(userId, DefaultCurrency.PLN, date);

        // Assert - Should be 5000 + 1000 + 3000 = 9000
        Assert.NotNull(result);
        Assert.Equal(9000m, result.Value);
    }

    [Fact]
    public async Task GetNetWorth_MissingStockPrice_ShouldUsePriceProviderFallback()
    {
        // Arrange
        var userId = 1;
        var date = new DateTime(2023, 6, 15, 0, 0, 0, DateTimeKind.Utc);

        var stockAccount = new StockAccount(userId, 1, "Stocks");
        stockAccount.Add(new StockAccountEntry(1, 1, date.AddDays(-5), 10m, 10m, "UNKNOWN", InvestmentType.Stock));

        _financialAccountRepositoryMock.Setup(repo => repo.GetAccounts<CurrencyAccount>(userId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(AsyncEnumerable.Empty<CurrencyAccount>());
        _financialAccountRepositoryMock.Setup(repo => repo.GetAccounts<StockAccount>(userId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(new[] { stockAccount }.ToAsyncEnumerable());
        _financialAccountRepositoryMock.Setup(repo => repo.GetAccounts<BondAccount>(userId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(AsyncEnumerable.Empty<BondAccount>());

        // Stock price not found - returns null, provider should return 0
        _stockRepository.Setup(x => x.GetThisOrNextOlder("UNKNOWN", It.IsAny<DateTime>()))
            .ReturnsAsync((StockPrice?)null);

        // Act
        var result = await _moneyFlowService.GetNetWorth(userId, DefaultCurrency.PLN, date);

        // Assert - Should be 0 (10 shares * 0 price)
        Assert.NotNull(result);
        Assert.Equal(0m, result.Value);
    }

    [Fact]
    public async Task GetNetWorth_EmptyAccounts_ShouldReturnZero()
    {
        // Arrange
        var userId = 1;
        var date = new DateTime(2023, 6, 15, 0, 0, 0, DateTimeKind.Utc);

        // Accounts with no entries
        var currencyAccount = new CurrencyAccount(userId, 1, "Empty", AccountLabel.Cash);
        var stockAccount = new StockAccount(userId, 2, "Empty Stocks");
        var bondAccount = new BondAccount(userId, 3, "Empty Bonds", AccountLabel.Other);

        _financialAccountRepositoryMock.Setup(repo => repo.GetAccounts<CurrencyAccount>(userId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(new[] { currencyAccount }.ToAsyncEnumerable());
        _financialAccountRepositoryMock.Setup(repo => repo.GetAccounts<StockAccount>(userId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(new[] { stockAccount }.ToAsyncEnumerable());
        _financialAccountRepositoryMock.Setup(repo => repo.GetAccounts<BondAccount>(userId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(new[] { bondAccount }.ToAsyncEnumerable());

        // Act
        var result = await _moneyFlowService.GetNetWorth(userId, DefaultCurrency.PLN, date);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0m, result.Value);
    }

    [Fact]
    public async Task GetNetWorth_FutureDate_ShouldUseCurrentDate()
    {
        // Arrange
        var userId = 1;
        var futureDate = DateTime.UtcNow.AddYears(1);

        var currencyAccount = new CurrencyAccount(userId, 1, "Test", AccountLabel.Cash);
        currencyAccount.Add(new CurrencyAccountEntry(1, 1, DateTime.UtcNow.AddDays(-1), 0, 1000m) { Labels = [] });

        _financialAccountRepositoryMock.Setup(repo => repo.GetAccounts<CurrencyAccount>(userId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(new[] { currencyAccount }.ToAsyncEnumerable());
        _financialAccountRepositoryMock.Setup(repo => repo.GetAccounts<StockAccount>(userId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(AsyncEnumerable.Empty<StockAccount>());
        _financialAccountRepositoryMock.Setup(repo => repo.GetAccounts<BondAccount>(userId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(AsyncEnumerable.Empty<BondAccount>());

        // Act
        var result = await _moneyFlowService.GetNetWorth(userId, DefaultCurrency.PLN, futureDate);

        // Assert - Should calculate using DateTime.UtcNow instead
        Assert.NotNull(result);
        Assert.Equal(1000m, result.Value);
    }

    [Fact]
    public async Task GetNetWorth_LargePortfolioValues_ShouldMaintainPrecision()
    {
        // Arrange
        var userId = 1;
        var date = new DateTime(2023, 6, 15, 0, 0, 0, DateTimeKind.Utc);

        // Test with very large values approaching decimal limits
        var currencyAccount = new CurrencyAccount(userId, 1, "Large", AccountLabel.Cash);
        currencyAccount.Add(new CurrencyAccountEntry(1, 1, date, 999999999.99m, 999999999.99m) { Labels = [] });

        var stockAccount = new StockAccount(userId, 2, "Large Stocks");
        stockAccount.Add(new StockAccountEntry(2, 1, date, 1000000m, 1000000m, "MEGA", InvestmentType.Stock));

        var bondAccount = new BondAccount(userId, 3, "Large Bonds", AccountLabel.Other);
        bondAccount.Add(new BondAccountEntry(3, 1, date, 888888888.88m, 888888888.88m, 1));

        _financialAccountRepositoryMock.Setup(repo => repo.GetAccounts<CurrencyAccount>(userId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(new[] { currencyAccount }.ToAsyncEnumerable());
        _financialAccountRepositoryMock.Setup(repo => repo.GetAccounts<StockAccount>(userId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(new[] { stockAccount }.ToAsyncEnumerable());
        _financialAccountRepositoryMock.Setup(repo => repo.GetAccounts<BondAccount>(userId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(new[] { bondAccount }.ToAsyncEnumerable());

        _stockRepository.Setup(x => x.GetThisOrNextOlder("MEGA", It.IsAny<DateTime>()))
            .ReturnsAsync(new StockPrice { Currency = DefaultCurrency.PLN, Ticker = "MEGA", PricePerUnit = 1.11m });

        // Act
        var result = await _moneyFlowService.GetNetWorth(userId, DefaultCurrency.PLN, date);

        // Assert - Should handle large values: 999999999.99 + (1000000 * 1.11) + 888888888.88
        Assert.NotNull(result);
        var expected = 999999999.99m + (1000000m * 1.11m) + 888888888.88m;
        Assert.Equal(Math.Round(expected, 2), result.Value);
    }

    [Fact]
    public async Task GetNetWorth_MultipleStocksInSameAccount_ShouldAggregateCorrectly()
    {
        // Arrange
        var userId = 1;
        var date = new DateTime(2023, 6, 15, 0, 0, 0, DateTimeKind.Utc);

        var stockAccount = new StockAccount(userId, 1, "Diversified");
        stockAccount.Add(new StockAccountEntry(1, 1, date, 10m, 10m, "AAPL", InvestmentType.Stock));
        stockAccount.Add(new StockAccountEntry(1, 2, date, 20m, 20m, "GOOGL", InvestmentType.Stock));
        stockAccount.Add(new StockAccountEntry(1, 3, date, 15m, 15m, "MSFT", InvestmentType.Stock));

        _financialAccountRepositoryMock.Setup(repo => repo.GetAccounts<CurrencyAccount>(userId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(AsyncEnumerable.Empty<CurrencyAccount>());
        _financialAccountRepositoryMock.Setup(repo => repo.GetAccounts<StockAccount>(userId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(new[] { stockAccount }.ToAsyncEnumerable());
        _financialAccountRepositoryMock.Setup(repo => repo.GetAccounts<BondAccount>(userId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(AsyncEnumerable.Empty<BondAccount>());

        _stockRepository.Setup(x => x.GetThisOrNextOlder("AAPL", It.IsAny<DateTime>()))
            .ReturnsAsync(new StockPrice { Currency = DefaultCurrency.PLN, Ticker = "AAPL", PricePerUnit = 150m });
        _stockRepository.Setup(x => x.GetThisOrNextOlder("GOOGL", It.IsAny<DateTime>()))
            .ReturnsAsync(new StockPrice { Currency = DefaultCurrency.PLN, Ticker = "GOOGL", PricePerUnit = 100m });
        _stockRepository.Setup(x => x.GetThisOrNextOlder("MSFT", It.IsAny<DateTime>()))
            .ReturnsAsync(new StockPrice { Currency = DefaultCurrency.PLN, Ticker = "MSFT", PricePerUnit = 200m });

        // Act
        var result = await _moneyFlowService.GetNetWorth(userId, DefaultCurrency.PLN, date);

        // Assert - (10 * 150) + (20 * 100) + (15 * 200) = 1500 + 2000 + 3000 = 6500
        Assert.NotNull(result);
        Assert.Equal(6500m, result.Value);
    }

    [Fact]
    public async Task GetNetWorth_MultipleBondsInSameAccount_ShouldAggregateCorrectly()
    {
        // Arrange
        var userId = 1;
        var date = new DateTime(2023, 6, 15, 0, 0, 0, DateTimeKind.Utc);

        var bondAccount = new BondAccount(userId, 1, "Mixed Bonds", AccountLabel.Other);
        bondAccount.Add(new BondAccountEntry(1, 1, date, 1000m, 1000m, 1)); // Bond ID 1
        bondAccount.Add(new BondAccountEntry(1, 2, date, 2000m, 2000m, 2)); // Bond ID 2
        bondAccount.Add(new BondAccountEntry(1, 3, date, 1500m, 1500m, 3)); // Bond ID 3

        _financialAccountRepositoryMock.Setup(repo => repo.GetAccounts<CurrencyAccount>(userId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(AsyncEnumerable.Empty<CurrencyAccount>());
        _financialAccountRepositoryMock.Setup(repo => repo.GetAccounts<StockAccount>(userId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(AsyncEnumerable.Empty<StockAccount>());
        _financialAccountRepositoryMock.Setup(repo => repo.GetAccounts<BondAccount>(userId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(new[] { bondAccount }.ToAsyncEnumerable());

        // Act
        var result = await _moneyFlowService.GetNetWorth(userId, DefaultCurrency.PLN, date);

        // Assert - 1000 + 2000 + 1500 = 4500
        Assert.NotNull(result);
        Assert.Equal(4500m, result.Value);
    }

    [Fact]
    public async Task GetNetWorth_DateBeforeAllEntries_ShouldReturnZero()
    {
        // Arrange
        var userId = 1;
        var entryDate = new DateTime(2023, 6, 15, 0, 0, 0, DateTimeKind.Utc);
        var queryDate = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc); // Before all entries

        var currencyAccount = new CurrencyAccount(userId, 1, "Test", AccountLabel.Cash);
        currencyAccount.Add(new CurrencyAccountEntry(1, 1, entryDate, 0, 1000m) { Labels = [] });

        _financialAccountRepositoryMock.Setup(repo => repo.GetAccounts<CurrencyAccount>(userId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(new[] { currencyAccount }.ToAsyncEnumerable());
        _financialAccountRepositoryMock.Setup(repo => repo.GetAccounts<StockAccount>(userId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(AsyncEnumerable.Empty<StockAccount>());
        _financialAccountRepositoryMock.Setup(repo => repo.GetAccounts<BondAccount>(userId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(AsyncEnumerable.Empty<BondAccount>());

        // Act
        var result = await _moneyFlowService.GetNetWorth(userId, DefaultCurrency.PLN, queryDate);

        // Assert - GetThisOrNextOlder should return null for dates before all entries
        Assert.NotNull(result);
        Assert.Equal(0m, result.Value);
    }

    [Fact]
    public async Task GetNetWorth_NegativeBalances_ShouldCalculateCorrectly()
    {
        // Arrange
        var userId = 1;
        var date = new DateTime(2023, 6, 15, 0, 0, 0, DateTimeKind.Utc);

        // Currency account with negative balance (debt/credit card)
        var currencyAccount = new CurrencyAccount(userId, 1, "Credit Card", AccountLabel.Loan);
        currencyAccount.Add(new CurrencyAccountEntry(1, 1, date, 0, 1000m) { Labels = [] });
        currencyAccount.Add(new CurrencyAccountEntry(1, 2, date.AddDays(1), 0, -1500m) { Labels = [] });

        _financialAccountRepositoryMock.Setup(repo => repo.GetAccounts<CurrencyAccount>(userId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(new[] { currencyAccount }.ToAsyncEnumerable());
        _financialAccountRepositoryMock.Setup(repo => repo.GetAccounts<StockAccount>(userId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(AsyncEnumerable.Empty<StockAccount>());
        _financialAccountRepositoryMock.Setup(repo => repo.GetAccounts<BondAccount>(userId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(AsyncEnumerable.Empty<BondAccount>());

        // Act
        var result = await _moneyFlowService.GetNetWorth(userId, DefaultCurrency.PLN, date.AddDays(1));

        // Assert - Should be -500 (1000 - 1500)
        Assert.NotNull(result);
        Assert.Equal(-500m, result.Value);
    }

    [Fact]
    public async Task GetNetWorth_TimeSeries_ShouldContainAllDates()
    {
        // Arrange
        var userId = 1;
        var startDate = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var endDate = new DateTime(2023, 1, 5, 0, 0, 0, DateTimeKind.Utc);

        var currencyAccount = new CurrencyAccount(userId, 1, "Test", AccountLabel.Cash);
        currencyAccount.Add(new CurrencyAccountEntry(1, 1, startDate, 0, 1000m) { Labels = [] });

        _financialAccountRepositoryMock.Setup(repo => repo.GetAccounts<CurrencyAccount>(userId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(new[] { currencyAccount }.ToAsyncEnumerable());
        _financialAccountRepositoryMock.Setup(repo => repo.GetAccounts<StockAccount>(userId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(AsyncEnumerable.Empty<StockAccount>());
        _financialAccountRepositoryMock.Setup(repo => repo.GetAccounts<BondAccount>(userId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(AsyncEnumerable.Empty<BondAccount>());

        // Act
        var result = await _moneyFlowService.GetNetWorth(userId, DefaultCurrency.PLN, startDate, endDate);

        // Assert - Should have 5 days (Jan 1-5)
        Assert.Equal(5, result.Count);
        Assert.True(result.ContainsKey(startDate));
        Assert.True(result.ContainsKey(endDate));

        // All values should be 1000 since no changes
        Assert.All(result.Values, value => Assert.Equal(1000m, value));
    }
}