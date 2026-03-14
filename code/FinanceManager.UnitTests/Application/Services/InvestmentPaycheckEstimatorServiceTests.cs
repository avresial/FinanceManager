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
public class InvestmentPaycheckEstimatorServiceTests
{
    private readonly Mock<IFinancialAccountRepository> _financialAccountRepositoryMock = new();
    private readonly Mock<IFinancialLabelsRepository> _financialLabelsRepositoryMock = new();
    private readonly Mock<IStockPriceRepository> _stockRepositoryMock = new();
    private readonly Mock<ICurrencyExchangeService> _currencyExchangeServiceMock = new();
    private readonly InvestmentPaycheckEstimatorService _service;

    public InvestmentPaycheckEstimatorServiceTests()
    {
        _currencyExchangeServiceMock
            .Setup(x => x.GetExchangeRateAsync(It.IsAny<Currency>(), It.IsAny<Currency>(), It.IsAny<DateTime>()))
            .ReturnsAsync(1m);

        IMemoryCache cache = new MemoryCache(new MemoryCacheOptions());
        var stockPriceProvider = new StockPriceProvider(_stockRepositoryMock.Object, _currencyExchangeServiceMock.Object, cache);
        _service = new InvestmentPaycheckEstimatorService(_financialAccountRepositoryMock.Object, _financialLabelsRepositoryMock.Object, stockPriceProvider);
    }

    [Fact]
    public async Task GetEstimate_MixedAssetsAndPartialSalaryHistory_ReturnsExpectedValues()
    {
        var userId = 1;
        var asOfDate = new DateTime(2026, 3, 14, 0, 0, 0, DateTimeKind.Utc);
        var salaryLabel = new FinancialLabel { Id = 1, Name = "salary" };

        var salaryAccount = new CurrencyAccount(userId, 10, "Salary", AccountLabel.Cash);
        salaryAccount.Add(new CurrencyAccountEntry(10, 1, new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc), 3000m, 3000m) { Labels = [salaryLabel] }, false);
        salaryAccount.Add(new CurrencyAccountEntry(10, 2, new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc), 4500m, 4500m) { Labels = [salaryLabel] }, false);

        var stockAccount = new StockAccount(userId, 20, "Stocks");
        stockAccount.Add(new StockAccountEntry(20, 1, asOfDate.AddDays(-1), 100m, 100m, "MSFT", InvestmentType.Stock), false);

        var bondAccount = new BondAccount(userId, 30, "Bonds", AccountLabel.Other);
        bondAccount.Add(new BondAccountEntry(30, 1, asOfDate.AddDays(-2), 12000m, 12000m, 1), false);

        _financialLabelsRepositoryMock
            .Setup(x => x.GetLabels(It.IsAny<CancellationToken>()))
            .Returns(new[] { salaryLabel }.ToAsyncEnumerable());

        _financialAccountRepositoryMock
            .Setup(x => x.GetAccounts<CurrencyAccount>(userId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(new[] { salaryAccount }.ToAsyncEnumerable());
        _financialAccountRepositoryMock
            .Setup(x => x.GetAccounts<StockAccount>(userId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(new[] { stockAccount }.ToAsyncEnumerable());
        _financialAccountRepositoryMock
            .Setup(x => x.GetAccounts<BondAccount>(userId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(new[] { bondAccount }.ToAsyncEnumerable());

        _stockRepositoryMock
            .Setup(x => x.GetThisOrNextOlder("MSFT", It.IsAny<DateTime>()))
            .ReturnsAsync(new StockPrice { Ticker = "MSFT", Currency = DefaultCurrency.PLN, PricePerUnit = 10m, Date = asOfDate });

        var result = await _service.GetEstimate(userId, DefaultCurrency.PLN, asOfDate, 0.05m, 3);

        Assert.Equal(13000m, result.InvestableAssetsValue);
        Assert.Equal(54.17m, result.SustainableMonthlyPaycheck);
        Assert.Equal(3, result.SalaryMonthsRequested);
        Assert.Equal(2, result.SalaryMonthsUsed);
        Assert.Equal(3750m, result.AverageMonthlySalary);
        Assert.Equal(0.0144m, result.IncomeReplacementRatio);
        Assert.True(result.HasPartialSalaryHistory);
    }

    [Fact]
    public async Task GetEstimate_WithoutSalaryLabel_ReturnsAssetValuesWithoutIncomeBaseline()
    {
        var userId = 1;
        var asOfDate = new DateTime(2026, 3, 14, 0, 0, 0, DateTimeKind.Utc);

        var bondAccount = new BondAccount(userId, 30, "Bonds", AccountLabel.Other);
        bondAccount.Add(new BondAccountEntry(30, 1, asOfDate.AddDays(-2), 24000m, 24000m, 1), false);

        _financialLabelsRepositoryMock
            .Setup(x => x.GetLabels(It.IsAny<CancellationToken>()))
            .Returns(AsyncEnumerable.Empty<FinancialLabel>());

        _financialAccountRepositoryMock
            .Setup(x => x.GetAccounts<CurrencyAccount>(userId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(AsyncEnumerable.Empty<CurrencyAccount>());
        _financialAccountRepositoryMock
            .Setup(x => x.GetAccounts<StockAccount>(userId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(AsyncEnumerable.Empty<StockAccount>());
        _financialAccountRepositoryMock
            .Setup(x => x.GetAccounts<BondAccount>(userId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(new[] { bondAccount }.ToAsyncEnumerable());

        var result = await _service.GetEstimate(userId, DefaultCurrency.PLN, asOfDate, 0.04m, 3);

        Assert.Equal(24000m, result.InvestableAssetsValue);
        Assert.Equal(80m, result.SustainableMonthlyPaycheck);
        Assert.Equal(0, result.SalaryMonthsUsed);
        Assert.Null(result.AverageMonthlySalary);
        Assert.Null(result.IncomeReplacementRatio);
        Assert.False(result.HasSalaryData);
    }
}