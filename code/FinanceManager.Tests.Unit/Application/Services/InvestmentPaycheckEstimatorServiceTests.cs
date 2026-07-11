using FinanceManager.Application.MoneyFlow.InvestmentPaycheck;
using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Bond.Repositories;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Services;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
using FinanceManager.Domain.Labels.Repositories;
using FinanceManager.Domain.Shared;
using Moq;

namespace FinanceManager.Tests.Unit.Application.Services;

[Collection("Application")]
[Trait("Category", "Unit")]
public class InvestmentPaycheckEstimatorServiceTests
{
    private readonly Mock<IFinancialAccountRepository> _financialAccountRepositoryMock = new();
    private readonly Mock<IFinancialLabelsRepository> _financialLabelsRepositoryMock = new();
    private readonly Mock<IInvestmentValuationService> _investmentValuationServiceMock = new();
    private readonly Mock<IBondDetailsRepository> _bondDetailsRepositoryMock = new();
    private readonly InvestmentPaycheckEstimatorService _service;

    public InvestmentPaycheckEstimatorServiceTests()
    {
        _financialAccountRepositoryMock
            .Setup(x => x.GetAccounts<InvestmentAccount>(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(AsyncEnumerable.Empty<InvestmentAccount>());
        _investmentValuationServiceMock
            .Setup(x => x.GetAccountValueAsync(It.IsAny<int>(), It.IsAny<Currency>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0m);

        _service = new InvestmentPaycheckEstimatorService(_financialAccountRepositoryMock.Object, _financialLabelsRepositoryMock.Object, _investmentValuationServiceMock.Object, _bondDetailsRepositoryMock.Object);
    }

    [Fact]
    public async Task GetEstimate_ExcludesCurrentPartialMonthFromSalaryAverage()
    {
        var userId = 1;
        var asOfDate = new DateTime(2026, 3, 14, 0, 0, 0, DateTimeKind.Utc);
        var salaryLabel = new FinancialLabel { Id = 1, Name = "salary" };

        var salaryAccount = new CurrencyAccount(userId, 10, "Salary", AccountLabel.Cash);
        salaryAccount.Add(new CurrencyAccountEntry(10, 1, new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc), 3000m, 3000m) { Labels = [salaryLabel] }, false);
        salaryAccount.Add(new CurrencyAccountEntry(10, 2, new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc), 4500m, 4500m) { Labels = [salaryLabel] }, false);

        var investmentAccount = new InvestmentAccount(userId, 20, "Stocks");

        var bondAccount = new BondAccount(userId, 30, "Bonds", AccountLabel.Other);
        bondAccount.Add(new BondAccountEntry(30, 1, asOfDate.AddDays(-2), 12000m, 12000m, 1), false);

        _financialLabelsRepositoryMock
            .Setup(x => x.GetLabels(It.IsAny<CancellationToken>()))
            .Returns(new[] { salaryLabel }.ToAsyncEnumerable());

        _financialAccountRepositoryMock
            .Setup(x => x.GetAccounts<CurrencyAccount>(userId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(new[] { salaryAccount }.ToAsyncEnumerable());
        _financialAccountRepositoryMock
            .Setup(x => x.GetAccounts<InvestmentAccount>(userId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(new[] { investmentAccount }.ToAsyncEnumerable());
        _investmentValuationServiceMock
            .Setup(x => x.GetAccountValueAsync(20, It.IsAny<Currency>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1000m);
        _financialAccountRepositoryMock
            .Setup(x => x.GetAccounts<BondAccount>(userId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(new[] { bondAccount }.ToAsyncEnumerable());
        _bondDetailsRepositoryMock
            .Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>()))
            .Returns(new[]
            {
                new BondDetails(
                    "Bond",
                    "Issuer",
                    DateOnly.FromDateTime(asOfDate.AddYears(-1)),
                    DateOnly.FromDateTime(asOfDate.AddYears(1)),
                    [new BondCalculationMethod { DateOperator = DateOperator.UntilDate, DateValue = asOfDate.AddYears(1).ToString("yyyy-MM-dd"), Rate = 0m }],
                    DefaultCurrency.PLN,
                    BondType.InflationBond,
                    1m)
                {
                    Id = 1
                }
            }.ToAsyncEnumerable());

        var result = await _service.GetEstimate(userId, DefaultCurrency.PLN, asOfDate, 0.05m, 3);

        Assert.Equal(13000m, result.InvestableAssetsValue);
        Assert.Equal(54.17m, result.SustainableMonthlyPaycheck);
        Assert.Equal(3, result.SalaryMonthsRequested);
        Assert.Equal(1, result.SalaryMonthsUsed);
        Assert.Equal(3000m, result.AverageMonthlySalary);
        Assert.Equal(0.0181m, result.IncomeReplacementRatio);
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
            .Setup(x => x.GetAccounts<InvestmentAccount>(userId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(AsyncEnumerable.Empty<InvestmentAccount>());
        _financialAccountRepositoryMock
            .Setup(x => x.GetAccounts<BondAccount>(userId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(new[] { bondAccount }.ToAsyncEnumerable());
        _bondDetailsRepositoryMock
            .Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>()))
            .Returns(new[]
            {
                new BondDetails(
                    "Bond",
                    "Issuer",
                    DateOnly.FromDateTime(asOfDate.AddYears(-1)),
                    DateOnly.FromDateTime(asOfDate.AddYears(1)),
                    [new BondCalculationMethod { DateOperator = DateOperator.UntilDate, DateValue = asOfDate.AddYears(1).ToString("yyyy-MM-dd"), Rate = 0m }],
                    DefaultCurrency.PLN,
                    BondType.InflationBond,
                    1m)
                {
                    Id = 1
                }
            }.ToAsyncEnumerable());

        var result = await _service.GetEstimate(userId, DefaultCurrency.PLN, asOfDate, 0.04m, 3);

        Assert.Equal(24000m, result.InvestableAssetsValue);
        Assert.Equal(80m, result.SustainableMonthlyPaycheck);
        Assert.Equal(0, result.SalaryMonthsUsed);
        Assert.Null(result.AverageMonthlySalary);
        Assert.Null(result.IncomeReplacementRatio);
        Assert.False(result.HasSalaryData);
    }
}