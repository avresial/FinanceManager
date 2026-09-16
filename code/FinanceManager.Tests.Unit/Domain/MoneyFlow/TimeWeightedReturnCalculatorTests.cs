using FinanceManager.Domain.MoneyFlow.Entities;
using FinanceManager.Domain.MoneyFlow.Services;

namespace FinanceManager.Tests.Unit.Domain.MoneyFlow;

[Trait("Category", "Unit")]
public class TimeWeightedReturnCalculatorTests
{
    private static readonly DateTime _start = new(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _end = new(2023, 1, 3, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Calculate_NeutralizesContribution()
    {
        var result = TimeWeightedReturnCalculator.Calculate(
            [new(_start, 100m, 100m, 220m)],
            _start,
            _start);

        Assert.Equal(TimeWeightedReturnStatus.Available, result.Status);
        Assert.Equal(0.1m, result.TotalReturn);
    }

    [Fact]
    public void Calculate_NeutralizesWithdrawal()
    {
        var result = TimeWeightedReturnCalculator.Calculate(
            [new(_start, 200m, -50m, 165m)],
            _start,
            _start);

        Assert.Equal(TimeWeightedReturnStatus.Available, result.Status);
        Assert.Equal(0.1m, result.TotalReturn);
    }

    [Fact]
    public void Calculate_LinksMultiplePeriodsGeometricallyIncludingNegativeReturn()
    {
        var result = TimeWeightedReturnCalculator.Calculate(
            [
                new(_start, 100m, 0m, 90m),
                new(_start.AddDays(1), 90m, 10m, 110m),
                new(_start.AddDays(2), 110m, 0m, 121m),
            ],
            _start,
            _end);

        Assert.Equal(TimeWeightedReturnStatus.Available, result.Status);
        Assert.Equal(0.089m, result.TotalReturn);
    }

    [Fact]
    public void Calculate_ReturnsExplicitStatesForMissingAndInvalidData()
    {
        var insufficient = TimeWeightedReturnCalculator.Calculate([], _start, _end);
        var unavailable = TimeWeightedReturnCalculator.Calculate(
            [new(_start, 100m, -100m, 0m)],
            _start,
            _end);
        var duplicateDates = TimeWeightedReturnCalculator.Calculate(
            [new(_start, 100m, 0m, 100m), new(_start, 100m, 0m, 100m)],
            _start,
            _end);

        Assert.Equal(TimeWeightedReturnStatus.InsufficientData, insufficient.Status);
        Assert.Equal(TimeWeightedReturnStatus.Unavailable, unavailable.Status);
        Assert.Equal(TimeWeightedReturnStatus.Unavailable, duplicateDates.Status);
    }
}