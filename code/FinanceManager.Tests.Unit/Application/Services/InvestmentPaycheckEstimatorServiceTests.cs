using FinanceManager.Application.MoneyFlow.InvestmentPaycheck;
using FinanceManager.Application.Transactions.Bonds.Valuation;
using FinanceManager.Domain.Currencies.Entities;
using FinanceManager.Domain.Labels.Entities;
using FinanceManager.Domain.Labels.Repositories;
using FinanceManager.Domain.Portfolio.Valuation.Services;
using FinanceManager.Domain.Shared;
using FinanceManager.Domain.Transactions.Bonds.Entities;
using FinanceManager.Domain.Transactions.Bonds.Repositories;
using Moq;

namespace FinanceManager.Tests.Unit.Application.Services;

[Collection("Application")]
[Trait("Category", "Unit")]
public class InvestmentPaycheckEstimatorServiceTests
{
    private static readonly DateTime _asOfDate = new(2026, 3, 14, 0, 0, 0, DateTimeKind.Utc);

    private readonly InMemoryOwnerLedger _cashLedger = new();
    private readonly Mock<IFinancialLabelsRepository> _financialLabelsRepositoryMock = new();
    private readonly Mock<IInvestmentValuationService> _investmentValuationServiceMock = new();
    private readonly Mock<IBondDetailsRepository> _bondDetailsRepositoryMock = new();
    private readonly InMemoryOwnerLedger _bondLedger = new();
    private readonly InvestmentPaycheckEstimatorService _service;

    public InvestmentPaycheckEstimatorServiceTests()
    {
        _investmentValuationServiceMock
            .Setup(x => x.GetUserValueByListingAsync(It.IsAny<int>(), It.IsAny<Currency>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<long, decimal>());
        _bondDetailsRepositoryMock
            .Setup(x => x.GetByIdsAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<int> ids, CancellationToken _) => [.. ids.Select(CreateBondDetails)]);

        _service = new InvestmentPaycheckEstimatorService(
            _cashLedger,
            _financialLabelsRepositoryMock.Object,
            _investmentValuationServiceMock.Object,
            _bondLedger,
            new BondDashboardContext(_bondDetailsRepositoryMock.Object));
    }

    [Fact]
    public async Task GetEstimate_ExcludesCurrentPartialMonthFromSalaryAverage()
    {
        var userId = 1;
        var salaryLabel = new FinancialLabel { Id = 1, Name = "salary" };

        _cashLedger.Add(0, new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc), 3000m, [salaryLabel.Id])
            .Add(0, new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc), 4500m, [salaryLabel.Id]);

        _bondLedger.Add(1, _asOfDate.AddDays(-2), 12000m);

        _financialLabelsRepositoryMock
            .Setup(x => x.GetLabels(It.IsAny<CancellationToken>()))
            .Returns(new[] { salaryLabel }.ToAsyncEnumerable());
        // Pooled holdings: the owner's value counts regardless of which account (or none) a trade was booked in.
        _investmentValuationServiceMock
            .Setup(x => x.GetUserValueByListingAsync(userId, It.IsAny<Currency>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<long, decimal> { [7] = 600m, [8] = 400m });

        var result = await _service.GetEstimate(userId, DefaultCurrency.PLN, _asOfDate, 0.05m, 3);

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
        _bondLedger.Add(1, _asOfDate.AddDays(-2), 24000m);

        _financialLabelsRepositoryMock
            .Setup(x => x.GetLabels(It.IsAny<CancellationToken>()))
            .Returns(AsyncEnumerable.Empty<FinancialLabel>());

        var result = await _service.GetEstimate(userId, DefaultCurrency.PLN, _asOfDate, 0.04m, 3);

        Assert.Equal(24000m, result.InvestableAssetsValue);
        Assert.Equal(80m, result.SustainableMonthlyPaycheck);
        Assert.Equal(0, result.SalaryMonthsUsed);
        Assert.Null(result.AverageMonthlySalary);
        Assert.Null(result.IncomeReplacementRatio);
        Assert.False(result.HasSalaryData);
    }

    [Fact]
    public async Task GetEstimate_AveragesSalaryPerMonthAcrossCurrenciesAndUnlinkedEntries_ForGuestOwner()
    {
        var userId = -9;
        var salaryLabel = new FinancialLabel { Id = 1, Name = "Salary" };
        _financialLabelsRepositoryMock
            .Setup(x => x.GetLabels(It.IsAny<CancellationToken>()))
            .Returns(new[] { salaryLabel }.ToAsyncEnumerable());
        // Two paychecks in January count as one month; the boundary entry on the 1st of March (the current month) is excluded.
        _cashLedger.Add(0, new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc), 1000m, [salaryLabel.Id])
            .Add(0, new DateTime(2026, 1, 20, 0, 0, 0, DateTimeKind.Utc), 500m, [salaryLabel.Id])
            .Add(0, new DateTime(2026, 2, 5, 0, 0, 0, DateTimeKind.Utc), 3000m, [salaryLabel.Id])
            .Add(0, new DateTime(2026, 2, 6, 0, 0, 0, DateTimeKind.Utc), 9999m)
            .Add(0, new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc), 4500m, [salaryLabel.Id]);

        var result = await _service.GetEstimate(userId, DefaultCurrency.PLN, _asOfDate, 0.05m, 3);

        Assert.Equal(2, result.SalaryMonthsUsed);
        Assert.Equal(2250m, result.AverageMonthlySalary);
    }

    [Fact]
    public async Task GetEstimate_PoolsBondEntriesPerBond_AndIgnoresSoldOutBonds()
    {
        var userId = -4;
        _bondLedger.Add(1, _asOfDate.AddDays(-30), 5000m).Add(1, _asOfDate.AddDays(-2), 1000m)
            .Add(2, _asOfDate.AddDays(-30), 700m).Add(2, _asOfDate.AddDays(-3), -700m);
        _financialLabelsRepositoryMock
            .Setup(x => x.GetLabels(It.IsAny<CancellationToken>()))
            .Returns(AsyncEnumerable.Empty<FinancialLabel>());

        var result = await _service.GetEstimate(userId, DefaultCurrency.PLN, _asOfDate, 0.05m, 3);

        Assert.Equal(6000m, result.InvestableAssetsValue);
    }

    private static BondDetails CreateBondDetails(int id) => new(
        "Bond",
        "Issuer",
        DateOnly.FromDateTime(_asOfDate.AddYears(-1)),
        DateOnly.FromDateTime(_asOfDate.AddYears(1)),
        [new BondCalculationMethod { DateOperator = DateOperator.UntilDate, DateValue = _asOfDate.AddYears(1).ToString("yyyy-MM-dd"), Rate = 0m }],
        DefaultCurrency.PLN,
        BondType.InflationBond,
        1m)
    { Id = id };
}