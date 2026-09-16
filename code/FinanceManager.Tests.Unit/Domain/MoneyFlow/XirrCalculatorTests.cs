using FinanceManager.Domain.MoneyFlow.Entities;
using FinanceManager.Domain.MoneyFlow.Services;

namespace FinanceManager.Tests.Unit.Domain.MoneyFlow;

[Trait("Category", "Unit")]
public class XirrCalculatorTests
{
    private static readonly DateTime _start = new(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _end = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Calculate_AnnualizesContributionAndEndingValue()
    {
        var result = XirrCalculator.Calculate(
            [new XirrCashFlow(_start, -100m), new XirrCashFlow(_end, 110m)],
            _start,
            _end);

        Assert.Equal(MoneyWeightedReturnStatus.Available, result.Status);
        Assert.Equal(0.1m, result.AnnualizedReturn);
    }

    [Fact]
    public void Calculate_GroupsSameDayFlowsDeterministically()
    {
        var first = XirrCalculator.Calculate(
            [
                new XirrCashFlow(_start, -100m),
                new XirrCashFlow(_start, -50m),
                new XirrCashFlow(_end, 165m),
            ],
            _start,
            _end);
        var second = XirrCalculator.Calculate(
            [
                new XirrCashFlow(_start, -50m),
                new XirrCashFlow(_end, 165m),
                new XirrCashFlow(_start, -100m),
            ],
            _start,
            _end);

        Assert.Equal(MoneyWeightedReturnStatus.Available, first.Status);
        Assert.Equal(0.1m, first.AnnualizedReturn);
        Assert.Equal(first, second);
    }

    [Fact]
    public void Calculate_SupportsWithdrawalsAndNegativeReturns()
    {
        var withWithdrawal = XirrCalculator.Calculate(
            [
                new XirrCashFlow(_start, -100m),
                new XirrCashFlow(_start.AddDays(182), 40m),
                new XirrCashFlow(_end, 50m),
            ],
            _start,
            _end);
        var negative = XirrCalculator.Calculate(
            [new XirrCashFlow(_start, -100m), new XirrCashFlow(_end, 90m)],
            _start,
            _end);

        Assert.Equal(MoneyWeightedReturnStatus.Available, withWithdrawal.Status);
        Assert.True(withWithdrawal.AnnualizedReturn < 0.1m);
        Assert.Equal(-0.1m, negative.AnnualizedReturn);
    }

    [Fact]
    public void Calculate_FiltersFlowsOutsideSelectedRange()
    {
        var result = XirrCalculator.Calculate(
            [
                new XirrCashFlow(_start.AddDays(-1), -1_000m),
                new XirrCashFlow(_start, -100m),
                new XirrCashFlow(_end, 110m),
                new XirrCashFlow(_end.AddDays(1), 1_000m),
            ],
            _start,
            _end);

        Assert.Equal(0.1m, result.AnnualizedReturn);
        Assert.Equal(_start, result.StartDate);
        Assert.Equal(_end, result.EndDate);
    }

    [Fact]
    public void Calculate_ReturnsExplicitStatesForInsufficientDataAndNoSolution()
    {
        var insufficient = XirrCalculator.Calculate([new XirrCashFlow(_start, -100m)], _start, _end);
        var noSolution = XirrCalculator.Calculate(
            [new XirrCashFlow(_start, -100m), new XirrCashFlow(_start, 100m)],
            _start,
            _end);

        Assert.Equal(MoneyWeightedReturnStatus.InsufficientData, insufficient.Status);
        Assert.Equal(MoneyWeightedReturnStatus.NoSolution, noSolution.Status);
        Assert.Null(noSolution.AnnualizedReturn);
    }
}