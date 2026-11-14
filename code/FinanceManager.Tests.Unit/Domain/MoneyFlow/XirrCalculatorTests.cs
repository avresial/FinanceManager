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

    // .NET 11 made decimal<->double conversions correctly rounded. XIRR converts amounts to double
    // for the solver and the root back to decimal, so these known answers guard that round trip.
    // The tolerance stays above the solver tolerance (1e-10) so platform Math.Pow ulp differences
    // cannot flip the result.
    private const decimal _knownAnswerTolerance = 0.000000001m;

    public static TheoryData<decimal> ExcelExampleScales => [1m, 1_000_000_000m];

    [Theory]
    [MemberData(nameof(ExcelExampleScales))]
    public void Calculate_MatchesExcelDocumentedExample_AtAnyScale(decimal scale)
    {
        // Microsoft's documented XIRR example. Excel reports 0.373362535 (37.34%), which is only
        // accurate to its own iteration tolerance; 0.3733625335 is this solver's value on both
        // .NET 10 and .NET 11.
        var start = new DateTime(2008, 1, 1);
        var end = new DateTime(2009, 4, 1);

        var result = XirrCalculator.Calculate(
            [
                new XirrCashFlow(start, -10_000m * scale),
                new XirrCashFlow(new DateTime(2008, 3, 1), 2_750m * scale),
                new XirrCashFlow(new DateTime(2008, 10, 30), 4_250m * scale),
                new XirrCashFlow(new DateTime(2009, 2, 15), 3_250m * scale),
                new XirrCashFlow(end, 2_750m * scale),
            ],
            start,
            end);

        AssertAnnualizedReturn(0.3733625335m, result);
    }

    [Fact]
    public void Calculate_ResolvesNearZeroReturn()
    {
        var result = XirrCalculator.Calculate(
            [new XirrCashFlow(_start, -1_000m), new XirrCashFlow(_end, 1_000.0001m)],
            _start,
            _end);

        AssertAnnualizedReturn(0.0000001m, result);
    }

    [Fact]
    public void Calculate_ResolvesCentSizedFlows()
    {
        var start = new DateTime(2022, 1, 3);
        var end = new DateTime(2023, 1, 2);

        var result = XirrCalculator.Calculate(
            [
                new XirrCashFlow(start, -0.01m),
                new XirrCashFlow(new DateTime(2022, 7, 1), -0.02m),
                new XirrCashFlow(end, 0.04m),
            ],
            start,
            end);

        AssertAnnualizedReturn(0.5248874784m, result);
    }

    private static void AssertAnnualizedReturn(decimal expected, MoneyWeightedReturnResult result)
    {
        Assert.Equal(MoneyWeightedReturnStatus.Available, result.Status);
        var actual = Assert.NotNull(result.AnnualizedReturn);
        Assert.InRange(actual, expected - _knownAnswerTolerance, expected + _knownAnswerTolerance);
    }
}