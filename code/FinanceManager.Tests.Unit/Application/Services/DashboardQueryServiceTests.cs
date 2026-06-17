using FinanceManager.Application.Dashboard;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Repositories;
using FinanceManager.Domain.FinancialAccounts.Shared.Services;
using FinanceManager.Domain.Identity.Repositories;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.MoneyFlow.Entities;
using FinanceManager.Domain.MoneyFlow.Services;
using Moq;

namespace FinanceManager.Tests.Unit.Application.Services;

[Collection("Application")]
[Trait("Category", "Unit")]
public class DashboardQueryServiceTests
{
    private const int _userId = 1;
    private readonly DateTime _start = new(2024, 1, 1);
    private readonly DateTime _end = new(2024, 2, 1);

    private readonly Mock<INetWorthService> _netWorthService = new();
    private readonly Mock<ILabelsValueService> _labelsValueService = new();
    private readonly Mock<IBalanceService> _balanceService = new();
    private readonly Mock<IAssetsService> _assetsService = new();
    private readonly Mock<ILiabilitiesService> _liabilitiesService = new();
    private readonly Mock<IExpenseDistributionService> _expenseDistributionService = new();
    private readonly Mock<ICurrencyRepository> _currencyRepository = new();
    private readonly DashboardQueryService _service;

    public DashboardQueryServiceTests()
    {
        _service = new DashboardQueryService(
            _netWorthService.Object,
            _labelsValueService.Object,
            _balanceService.Object,
            _assetsService.Object,
            _liabilitiesService.Object,
            _expenseDistributionService.Object,
            _currencyRepository.Object);
    }

    [Fact]
    public async Task GetOverview_ComposesAllFirstPaintData()
    {
        // Arrange
        var currency = DefaultCurrency.PLN;
        _currencyRepository.Setup(r => r.GetCurrency(currency.Id, It.IsAny<CancellationToken>())).ReturnsAsync(currency);

        var netWorth = new Dictionary<DateTime, decimal>
        {
            { _end, 200 },
            { _start, 100 },
        };
        _netWorthService.Setup(s => s.GetNetWorth(_userId, currency, _start, _end)).ReturnsAsync(netWorth);
        _balanceService.Setup(s => s.GetNetCashFlow(_userId, currency, _start, _end)).ReturnsAsync([new(_start, 50)]);
        _balanceService.Setup(s => s.GetClosingBalance(_userId, currency, _start, _end)).ReturnsAsync([new(_start, 75)]);
        _liabilitiesService.Setup(s => s.GetEndLiabilitiesPerType(_userId, _start, _end)).Returns(new[] { new NameValueResult("Mortgage", -1000) }.ToAsyncEnumerable());
        _liabilitiesService.Setup(s => s.GetEndLiabilitiesPerAccount(_userId, _start, _end)).Returns(new[] { new NameValueResult("Bank A", -1000) }.ToAsyncEnumerable());
        _labelsValueService.Setup(s => s.GetLabelsValue(_userId, _start, _end)).ReturnsAsync([new("Salary", 5000)]);
        _assetsService.Setup(s => s.GetEndAssetsPerType(_userId, currency, _end)).Returns(new[] { new NameValueResult("Cash", 3000) }.ToAsyncEnumerable());
        _assetsService.Setup(s => s.GetEndAssetsPerAccount(_userId, currency, _end)).Returns(new[] { new NameValueResult("Bank A", 3000) }.ToAsyncEnumerable());
        _expenseDistributionService.Setup(s => s.GetExpenseDistribution(_userId, currency, _start, _end)).ReturnsAsync([new("Food", 400)]);

        // Act
        var result = await _service.GetOverview(_userId, currency.Id, _start, _end, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(_userId, result.UserId);
        Assert.Equal(currency.Id, result.CurrencyId);
        Assert.Equal(_start, result.StartDate);
        Assert.Equal(_end, result.EndDate);

        // Net worth dictionary is projected and ordered ascending by date.
        Assert.Equal([_start, _end], result.NetWorthSeries.Select(x => x.DateTime));
        Assert.Equal([100, 200], result.NetWorthSeries.Select(x => x.Value));

        Assert.Single(result.NetCashFlowSeries);
        Assert.Single(result.ClosingBalanceSeries);
        Assert.Single(result.LiabilitiesPerType);
        Assert.Single(result.LiabilitiesPerAccount);
        Assert.Single(result.LabelsValue);
        Assert.Single(result.AssetsPerType);
        Assert.Single(result.AssetsPerAccount);
        Assert.Single(result.ExpenseDistribution);
        Assert.Equal("Salary", result.LabelsValue[0].Name);
        Assert.Equal("Food", result.ExpenseDistribution[0].Name);
    }

    [Fact]
    public async Task GetOverview_ReturnsNull_WhenCurrencyNotFound()
    {
        // Arrange
        _currencyRepository.Setup(r => r.GetCurrency(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync((Currency?)null);

        // Act
        var result = await _service.GetOverview(_userId, 999, _start, _end, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(result);
        _netWorthService.Verify(s => s.GetNetWorth(It.IsAny<int>(), It.IsAny<Currency>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()), Times.Never);
    }
}