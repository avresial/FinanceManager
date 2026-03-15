using FinanceManager.Application.Services;
using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.Services;
using Moq;

namespace FinanceManager.UnitTests.Application.Services;

[Collection("Application")]
[Trait("Category", "Unit")]
public class BalanceAggregationServiceTests
{
    private readonly Mock<IBalanceServiceTyped> _service1 = new();
    private readonly Mock<IBalanceServiceTyped> _service2 = new();
    private readonly BalanceService _balanceService;

    public BalanceAggregationServiceTests()
    {
        SetupDefaults(_service1);
        SetupDefaults(_service2);
        _balanceService = new([_service1.Object, _service2.Object]);
    }

    private static void SetupDefaults(Mock<IBalanceServiceTyped> mock)
    {
        mock.Setup(x => x.GetInflow(It.IsAny<int>(), It.IsAny<Currency>(), It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync([]);
        mock.Setup(x => x.GetInflow(It.IsAny<int>(), It.IsAny<Currency>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<IReadOnlyCollection<int>>())).ReturnsAsync([]);
        mock.Setup(x => x.GetOutflow(It.IsAny<int>(), It.IsAny<Currency>(), It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync([]);
        mock.Setup(x => x.GetOutflow(It.IsAny<int>(), It.IsAny<Currency>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<IReadOnlyCollection<int>>())).ReturnsAsync([]);
        mock.Setup(x => x.GetNetCashFlow(It.IsAny<int>(), It.IsAny<Currency>(), It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync([]);
        mock.Setup(x => x.GetNetCashFlow(It.IsAny<int>(), It.IsAny<Currency>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<IReadOnlyCollection<int>>())).ReturnsAsync([]);
        mock.Setup(x => x.GetClosingBalance(It.IsAny<int>(), It.IsAny<Currency>(), It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync([]);
        mock.Setup(x => x.GetClosingBalance(It.IsAny<int>(), It.IsAny<Currency>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<IReadOnlyCollection<int>>())).ReturnsAsync([]);
    }

    [Fact]
    public async Task GetClosingBalance_AggregatesValuesFromTypedServices()
    {
        var date = new DateTime(2024, 1, 1);
        _service1.Setup(x => x.GetClosingBalance(It.IsAny<int>(), It.IsAny<Currency>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                 .ReturnsAsync([new(date, 10)]);
        _service2.Setup(x => x.GetClosingBalance(It.IsAny<int>(), It.IsAny<Currency>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                 .ReturnsAsync([new(date, 15)]);

        var result = await _balanceService.GetClosingBalance(1, DefaultCurrency.PLN, date, date);

        Assert.Single(result);
        Assert.Equal(25, result[0].Value);
    }
}