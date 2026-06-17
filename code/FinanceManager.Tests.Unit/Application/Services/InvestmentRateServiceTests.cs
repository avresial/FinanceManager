using FinanceManager.Application.FinancialAccounts.Stock.Pricing;
using FinanceManager.Application.MoneyFlow.InvestmentRate;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Repositories;
using FinanceManager.Domain.FinancialAccounts.Currencies.Services;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
using FinanceManager.Domain.FinancialAccounts.Shared.Services;
using FinanceManager.Domain.FinancialAccounts.Stock.Entities;
using FinanceManager.Domain.FinancialAccounts.Stock.Repositories;
using FinanceManager.Domain.FinancialAccounts.Stock.Services;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Identity.Repositories;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.Labels.Repositories;
using FinanceManager.Domain.MoneyFlow.Entities;
using Microsoft.Extensions.Caching.Memory;
using Moq;

namespace FinanceManager.Tests.Unit.Application.Services;

[Collection("Application")]
[Trait("Category", "Unit")]
public class InvestmentRateServiceTests
{
    private readonly DateTime _startDate = new(DateTime.UtcNow.Year - 1, 1, 1);
    private readonly DateTime _endDate = DateTime.UtcNow;

    private readonly InvestmentRateService _investmentRateService;
    private readonly Mock<IFinancialAccountRepository> _financialAccountRepositoryMock = new();
    private readonly Mock<IFinancialLabelsRepository> _financialLabelsRepositoryMock = new();
    private readonly Mock<IStockPriceRepository> _stockRepository = new();
    private readonly Mock<ICurrencyExchangeService> _currencyExchangeService = new();

    public InvestmentRateServiceTests()
    {
        _currencyExchangeService.Setup(x => x.GetExchangeRateAsync(It.IsAny<Currency>(), It.IsAny<Currency>(), It.IsAny<DateTime>())).ReturnsAsync(1);

        IMemoryCache cache = new MemoryCache(new MemoryCacheOptions());
        var stockDetailsRepoMock = new Mock<IStockDetailsRepository>();
        stockDetailsRepoMock.Setup(x => x.Get(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string ticker, CancellationToken _) => new StockDetails
            {
                Isin = "US0000000001",
                Ticker = ticker,
                Currency = DefaultCurrency.PLN,
                Name = ticker,
                Type = "Stock",
                Region = "US"
            });
        var isinResolverMock = new Mock<IIsinResolver>();
        isinResolverMock.Setup(x => x.ResolveAsync("TICKER", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("US0000000001");
        var stockPriceProvider = new StockPriceProvider(
            _stockRepository.Object,
            new Mock<IAlphaVantageClient>().Object,
            stockDetailsRepoMock.Object,
            new Mock<ICurrencyRepository>().Object,
            _currencyExchangeService.Object,
            cache,
            isinResolverMock.Object);

        _investmentRateService = new InvestmentRateService(_financialAccountRepositoryMock.Object, _financialLabelsRepositoryMock.Object, stockPriceProvider);
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
        _stockRepository.Setup(x => x.GetThisOrNextOlder("US0000000001", It.IsAny<DateTime>()))
            .ReturnsAsync(new StockPrice() { Isin = "US0000000001", Currency = DefaultCurrency.PLN, PricePerUnit = 1m });

        _financialAccountRepositoryMock.Setup(repo => repo.GetAccounts<CurrencyAccount>(userId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(new[] { currencyAccount }.ToAsyncEnumerable());
        _financialAccountRepositoryMock.Setup(repo => repo.GetAccounts<StockAccount>(userId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(new[] { stockAccount }.ToAsyncEnumerable());

        // Act
        var result = await _investmentRateService.GetInvestmentRate(userId, _startDate, _endDate).ToListAsync(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(result);
        Assert.Equal(1000, result.First().Salary);
        Assert.Equal(10, result.First().InvestmentsChange);
    }
}