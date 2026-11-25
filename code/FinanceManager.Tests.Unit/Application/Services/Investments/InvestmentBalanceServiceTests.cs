using FinanceManager.Application.MoneyFlow.Balances;
using FinanceManager.Domain.Currencies.Entities;
using FinanceManager.Domain.Portfolio.Valuation.Services;
using Moq;

namespace FinanceManager.Tests.Unit.Application.Services.Investments;

[Trait("Category", "Unit")]
public class InvestmentBalanceServiceTests
{
    private const int _userId = 1;

    private readonly Mock<IInvestmentValuationService> _valuationService = new();
    private readonly Mock<IInvestmentCapitalSeriesService> _capitalSeriesService = new();
    private readonly InvestmentBalanceService _service;

    public InvestmentBalanceServiceTests()
    {
        _service = new InvestmentBalanceService(_valuationService.Object, _capitalSeriesService.Object);
    }

    [Fact]
    public async Task GetClosingBalance_Unfiltered_UsesThePooledOwnerSeriesAndZeroFillsMissingDays()
    {
        DateTime start = new(2024, 1, 1);
        DateTime end = new(2024, 1, 3);

        // The pooled series omits the middle day (valued to zero); the closing balance still reports that day.
        _valuationService.Setup(x => x.GetUserValueSeriesAsync(_userId, DefaultCurrency.PLN, start.Date, end.Date, It.IsAny<CancellationToken>()))
                         .ReturnsAsync(new Dictionary<DateTime, decimal> { [start] = 105, [end] = 127 });

        var result = await _service.GetClosingBalance(_userId, DefaultCurrency.PLN, start, end);

        Assert.Equal([105m, 0m, 127m], result.Select(x => x.Value));
        // Account lookups are only needed for account-filtered charts.
    }

    [Fact]
    public async Task GetCapital_Unfiltered_UsesThePooledOwnerCapitalSeries()
    {
        DateTime start = new(2024, 1, 1);
        DateTime end = new(2024, 1, 3);

        _capitalSeriesService.Setup(x => x.GetCapitalSeriesAsync(_userId, DefaultCurrency.PLN, start.Date, end.Date, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<DateTime, decimal> { [start] = 125, [start.AddDays(1)] = 130, [end] = 180 });

        var result = await _service.GetCapital(_userId, DefaultCurrency.PLN, start, end);

        Assert.Equal(125, result.Single(x => x.DateTime == start).Value);
        Assert.Equal(130, result.Single(x => x.DateTime == start.AddDays(1)).Value);
        Assert.Equal(180, result.Single(x => x.DateTime == end).Value);
    }

    [Fact]
    public async Task GetCapital_AndClosingBalance_ForAGuestUserWithNegativeId_ReadThePooledSeries()
    {
        DateTime start = new(2024, 1, 1);
        _valuationService.Setup(x => x.GetUserValueSeriesAsync(-7, DefaultCurrency.PLN, start, start, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<DateTime, decimal> { [start] = 10 });
        _capitalSeriesService.Setup(x => x.GetCapitalSeriesAsync(-7, DefaultCurrency.PLN, start, start, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<DateTime, decimal> { [start] = 8 });

        Assert.Equal(10, Assert.Single(await _service.GetClosingBalance(-7, DefaultCurrency.PLN, start, start)).Value);
        Assert.Equal(8, Assert.Single(await _service.GetCapital(-7, DefaultCurrency.PLN, start, start)).Value);
        Assert.Empty(await _service.GetClosingBalance(0, DefaultCurrency.PLN, start, start));
        Assert.Empty(await _service.GetCapital(0, DefaultCurrency.PLN, start, start));
    }

    [Fact]
    public async Task GetClosingBalance_Unfiltered_IsEmpty_WhenTheOwnerHoldsNothing()
    {
        DateTime start = new(2024, 1, 1);
        DateTime end = new(2024, 1, 2);
        _valuationService.Setup(x => x.GetUserValueSeriesAsync(_userId, DefaultCurrency.PLN, start.Date, end.Date, It.IsAny<CancellationToken>()))
                         .ReturnsAsync(new Dictionary<DateTime, decimal>());

        Assert.Empty(await _service.GetClosingBalance(_userId, DefaultCurrency.PLN, start, end));
    }

    [Fact]
    public async Task CashFlow_ReturnsEmpty_ToAvoidDoubleCountingAgainstCashAccount()
    {
        DateTime start = new(2024, 1, 1);
        DateTime end = new(2024, 1, 31);

        Assert.Empty(await _service.GetInflow(_userId, DefaultCurrency.PLN, start, end));
        Assert.Empty(await _service.GetOutflow(_userId, DefaultCurrency.PLN, start, end));
        Assert.Empty(await _service.GetNetCashFlow(_userId, DefaultCurrency.PLN, start, end));

        // No account enumeration or valuation should happen for cash-flow paths.
        _valuationService.VerifyNoOtherCalls();
    }
}