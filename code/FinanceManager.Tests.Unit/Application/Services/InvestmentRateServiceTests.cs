using FinanceManager.Application.MoneyFlow.InvestmentRate;
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
public class InvestmentRateServiceTests
{
    private readonly DateTime _startDate = new(DateTime.UtcNow.Year - 1, 1, 1);
    private readonly DateTime _endDate = DateTime.UtcNow;

    private readonly InvestmentRateService _investmentRateService;
    private readonly Mock<IFinancialAccountRepository> _financialAccountRepositoryMock = new();
    private readonly Mock<IFinancialLabelsRepository> _financialLabelsRepositoryMock = new();
    private readonly Mock<IInvestmentValuationService> _investmentValuationServiceMock = new();

    public InvestmentRateServiceTests()
    {
        _financialAccountRepositoryMock.Setup(r => r.GetAccounts<CurrencyAccount>(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(AsyncEnumerable.Empty<CurrencyAccount>());
        _financialAccountRepositoryMock.Setup(r => r.GetAccounts<InvestmentAccount>(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(AsyncEnumerable.Empty<InvestmentAccount>());
        _investmentValuationServiceMock
            .Setup(x => x.GetAccountValueAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<Currency>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, decimal>());

        _investmentRateService = new InvestmentRateService(_financialAccountRepositoryMock.Object, _financialLabelsRepositoryMock.Object, _investmentValuationServiceMock.Object);
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

        var investmentAccount = new InvestmentAccount(userId, 2, "Investments");

        _financialAccountRepositoryMock.Setup(repo => repo.GetAccounts<CurrencyAccount>(userId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(new[] { currencyAccount }.ToAsyncEnumerable());
        _financialAccountRepositoryMock.Setup(repo => repo.GetAccounts<InvestmentAccount>(userId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(new[] { investmentAccount }.ToAsyncEnumerable());

        // The investment value grew from 0 at the start of the window to 10 at the end.
        _investmentValuationServiceMock
            .Setup(x => x.GetAccountValueAsync(It.Is<IReadOnlyCollection<int>>(a => a.Contains(2)), It.IsAny<Currency>(), _startDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, decimal>());
        _investmentValuationServiceMock
            .Setup(x => x.GetAccountValueAsync(It.Is<IReadOnlyCollection<int>>(a => a.Contains(2)), It.IsAny<Currency>(), _endDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, decimal> { [2] = 10m });

        // Act
        var result = await _investmentRateService.GetInvestmentRate(userId, _startDate, _endDate).ToListAsync(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(result);
        Assert.Equal(1000, result.First().Salary);
        Assert.Equal(10, result.First().InvestmentsChange);
    }

    [Fact]
    public async Task GetInvestmentRate_NoSalaryInWindow_FallsBackToMostRecentEarlierSalary()
    {
        // A salary was paid two months ago and the investments changed this month. Querying just the
        // current month must not come back empty — the rate should use the most recent earlier salary.
        // Arrange
        var userId = 1;
        var salaryLabel = new FinancialLabel { Id = 1, Name = "Salary" };
        _financialLabelsRepositoryMock.Setup(repo => repo.GetLabels(It.IsAny<CancellationToken>())).Returns(new[] { salaryLabel }.ToAsyncEnumerable());

        var monthStart = new DateTime(_endDate.Year, _endDate.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var salaryDate = monthStart.AddMonths(-2).AddDays(9);

        var accountWithOldSalary = new CurrencyAccount(userId, 1, "Currency Account 1", AccountLabel.Cash);
        accountWithOldSalary.Add(new CurrencyAccountEntry(1, 1, salaryDate, 1000, 1000) { Labels = [salaryLabel] }, false);

        _financialAccountRepositoryMock.Setup(repo => repo.GetAccounts<CurrencyAccount>(userId, monthStart, _endDate))
            .Returns(AsyncEnumerable.Empty<CurrencyAccount>());
        _financialAccountRepositoryMock.Setup(repo => repo.GetAccounts<CurrencyAccount>(userId, monthStart.AddMonths(-12), monthStart))
            .Returns(new[] { accountWithOldSalary }.ToAsyncEnumerable());

        var investmentAccount = new InvestmentAccount(userId, 2, "Investments");
        _financialAccountRepositoryMock.Setup(repo => repo.GetAccounts<InvestmentAccount>(userId, monthStart, _endDate))
            .Returns(new[] { investmentAccount }.ToAsyncEnumerable());

        _investmentValuationServiceMock
            .Setup(x => x.GetAccountValueAsync(It.Is<IReadOnlyCollection<int>>(a => a.Contains(2)), It.IsAny<Currency>(), monthStart, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, decimal> { [2] = 100m });
        _investmentValuationServiceMock
            .Setup(x => x.GetAccountValueAsync(It.Is<IReadOnlyCollection<int>>(a => a.Contains(2)), It.IsAny<Currency>(), _endDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, decimal> { [2] = 600m });

        // Act
        var result = await _investmentRateService.GetInvestmentRate(userId, monthStart, _endDate).ToListAsync(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var rate = Assert.Single(result);
        Assert.Equal(1000, rate.Salary);
        Assert.Equal(500, rate.InvestmentsChange);
        Assert.Equal(0.5m, rate.GetPercentage());
    }

    [Fact]
    public async Task GetInvestmentRate_NoSalaryFound_StillReportsInvestmentChange()
    {
        // No salary anywhere (window or lookback), but the investments did change — the change must
        // still be reported instead of the month silently disappearing from the series.
        // Arrange
        var userId = 1;
        var salaryLabel = new FinancialLabel { Id = 1, Name = "Salary" };
        _financialLabelsRepositoryMock.Setup(repo => repo.GetLabels(It.IsAny<CancellationToken>())).Returns(new[] { salaryLabel }.ToAsyncEnumerable());

        var monthStart = new DateTime(_endDate.Year, _endDate.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var investmentAccount = new InvestmentAccount(userId, 2, "Investments");
        _financialAccountRepositoryMock.Setup(repo => repo.GetAccounts<InvestmentAccount>(userId, monthStart, _endDate))
            .Returns(new[] { investmentAccount }.ToAsyncEnumerable());

        _investmentValuationServiceMock
            .Setup(x => x.GetAccountValueAsync(It.Is<IReadOnlyCollection<int>>(a => a.Contains(2)), It.IsAny<Currency>(), _endDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, decimal> { [2] = 250m });

        // Act
        var result = await _investmentRateService.GetInvestmentRate(userId, monthStart, _endDate).ToListAsync(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var rate = Assert.Single(result);
        Assert.Equal(0, rate.Salary);
        Assert.Equal(250, rate.InvestmentsChange);
        Assert.Equal(0, rate.GetPercentage());
    }

    [Fact]
    public async Task GetInvestmentRate_MonthWithNoActivity_ReportsZeroRate()
    {
        // The user's most recent transactions are months old: the queried month has no salary and no
        // investment value change. The carried-forward salary must not fabricate a non-zero rate —
        // the month reports 0 %.
        // Arrange
        var userId = 1;
        var salaryLabel = new FinancialLabel { Id = 1, Name = "Salary" };
        _financialLabelsRepositoryMock.Setup(repo => repo.GetLabels(It.IsAny<CancellationToken>())).Returns(new[] { salaryLabel }.ToAsyncEnumerable());

        var monthStart = new DateTime(_endDate.Year, _endDate.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var salaryDate = monthStart.AddMonths(-3).AddDays(9);

        var accountWithOldSalary = new CurrencyAccount(userId, 1, "Currency Account 1", AccountLabel.Cash);
        accountWithOldSalary.Add(new CurrencyAccountEntry(1, 1, salaryDate, 1000, 1000) { Labels = [salaryLabel] }, false);

        _financialAccountRepositoryMock.Setup(repo => repo.GetAccounts<CurrencyAccount>(userId, monthStart, _endDate))
            .Returns(AsyncEnumerable.Empty<CurrencyAccount>());
        _financialAccountRepositoryMock.Setup(repo => repo.GetAccounts<CurrencyAccount>(userId, monthStart.AddMonths(-12), monthStart))
            .Returns(new[] { accountWithOldSalary }.ToAsyncEnumerable());

        var investmentAccount = new InvestmentAccount(userId, 2, "Investments");
        _financialAccountRepositoryMock.Setup(repo => repo.GetAccounts<InvestmentAccount>(userId, monthStart, _endDate))
            .Returns(new[] { investmentAccount }.ToAsyncEnumerable());

        // Portfolio value did not move during the month.
        _investmentValuationServiceMock
            .Setup(x => x.GetAccountValueAsync(It.Is<IReadOnlyCollection<int>>(a => a.Contains(2)), It.IsAny<Currency>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, decimal> { [2] = 100m });

        // Act
        var result = await _investmentRateService.GetInvestmentRate(userId, monthStart, _endDate).ToListAsync(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var rate = Assert.Single(result);
        Assert.Equal(1000, rate.Salary);
        Assert.Equal(0, rate.InvestmentsChange);
        Assert.Equal(0, rate.GetPercentage());
    }

    [Fact]
    public async Task GetInvestmentRate_NoSalaryAndNoInvestmentChange_ReturnsEmpty()
    {
        // Arrange
        var userId = 1;
        var salaryLabel = new FinancialLabel { Id = 1, Name = "Salary" };
        _financialLabelsRepositoryMock.Setup(repo => repo.GetLabels(It.IsAny<CancellationToken>())).Returns(new[] { salaryLabel }.ToAsyncEnumerable());

        // Act
        var result = await _investmentRateService.GetInvestmentRate(userId, _startDate, _endDate).ToListAsync(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(result);
    }
}