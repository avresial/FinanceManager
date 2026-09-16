using FinanceManager.Domain.MoneyFlow.Entities;

namespace FinanceManager.Domain.MoneyFlow.Services;

/// <summary>Calculates a deterministic annualized return from dated cash flows.</summary>
public static class XirrCalculator
{
    private const double _lowestRate = -0.999999999;
    private const double _initialUpperRate = 0.1;
    private const double _maximumUpperRate = 1_000_000_000d;
    private const double _valueTolerance = 1e-10;
    private const int _maximumIterations = 200;

    public static MoneyWeightedReturnResult Calculate(
        IEnumerable<XirrCashFlow> cashFlows,
        DateTime startDate,
        DateTime endDate)
    {
        var normalizedStart = startDate.Date;
        var normalizedEnd = endDate.Date;
        var inRangeFlows = cashFlows
            .Where(flow => flow.Date.Date >= normalizedStart && flow.Date.Date <= normalizedEnd)
            .Where(flow => flow.Amount != 0m)
            .ToList();

        var groupedFlows = inRangeFlows
            .GroupBy(flow => flow.Date.Date)
            .Select(group => (Date: group.Key, Amount: group.Sum(flow => flow.Amount)))
            .Where(flow => flow.Amount != 0m)
            .OrderBy(flow => flow.Date)
            .ToList();

        if (groupedFlows.Count == 0)
        {
            return HasOppositeSigns(inRangeFlows)
                ? new(null, MoneyWeightedReturnStatus.NoSolution, normalizedStart, normalizedEnd)
                : new(null, MoneyWeightedReturnStatus.InsufficientData, normalizedStart, normalizedEnd);
        }

        if (groupedFlows.Count < 2)
        {
            return groupedFlows.Count == 1 && HasOppositeSigns(inRangeFlows)
                ? new(null, MoneyWeightedReturnStatus.NoSolution, normalizedStart, normalizedEnd)
                : new(null, MoneyWeightedReturnStatus.InsufficientData, normalizedStart, normalizedEnd);
        }

        if (!groupedFlows.Any(flow => flow.Amount < 0m)
            || !groupedFlows.Any(flow => flow.Amount > 0m))
            return new(null, MoneyWeightedReturnStatus.NoSolution, normalizedStart, normalizedEnd);

        if (groupedFlows[0].Date == groupedFlows[^1].Date)
            return new(null, MoneyWeightedReturnStatus.NoSolution, normalizedStart, normalizedEnd);

        var values = groupedFlows
            .Select(flow => (Date: flow.Date, Amount: (double)flow.Amount))
            .ToArray();
        var lower = _lowestRate;
        var lowerValue = Evaluate(values, normalizedStart, lower);
        var zeroValue = Evaluate(values, normalizedStart, 0d);

        if (IsNearZero(zeroValue))
            return Available(0d, normalizedStart, normalizedEnd);

        double upper;
        double upperValue;
        if (HasOppositeSigns(lowerValue, zeroValue))
        {
            upper = 0d;
            upperValue = zeroValue;
        }
        else
        {
            upper = _initialUpperRate;
            upperValue = Evaluate(values, normalizedStart, upper);
            while (!HasOppositeSigns(zeroValue, upperValue) && upper < _maximumUpperRate)
            {
                upper *= 2d;
                upperValue = Evaluate(values, normalizedStart, upper);
            }

            if (!HasOppositeSigns(zeroValue, upperValue))
                return new(null, MoneyWeightedReturnStatus.NoSolution, normalizedStart, normalizedEnd);

            lower = 0d;
            lowerValue = zeroValue;
        }

        var root = Bisection(values, normalizedStart, lower, lowerValue, upper, upperValue);
        return double.IsFinite(root) && root > -1d && root <= (double)decimal.MaxValue
            ? Available(root, normalizedStart, normalizedEnd)
            : new(null, MoneyWeightedReturnStatus.NoSolution, normalizedStart, normalizedEnd);
    }

    private static MoneyWeightedReturnResult Available(double rate, DateTime startDate, DateTime endDate) =>
        new(Math.Round((decimal)rate, 10, MidpointRounding.AwayFromZero), MoneyWeightedReturnStatus.Available, startDate, endDate);

    private static double Bisection(
        IReadOnlyList<(DateTime Date, double Amount)> flows,
        DateTime baseDate,
        double lower,
        double lowerValue,
        double upper,
        double upperValue)
    {
        for (var iteration = 0; iteration < _maximumIterations; iteration++)
        {
            var middle = (lower + upper) / 2d;
            var middleValue = Evaluate(flows, baseDate, middle);
            if (IsNearZero(middleValue) || Math.Abs(upper - lower) < _valueTolerance)
                return middle;

            if (HasOppositeSigns(lowerValue, middleValue))
            {
                upper = middle;
                upperValue = middleValue;
            }
            else
            {
                lower = middle;
                lowerValue = middleValue;
            }
        }

        return (lower + upper) / 2d;
    }

    private static double Evaluate(
        IReadOnlyList<(DateTime Date, double Amount)> flows,
        DateTime baseDate,
        double rate)
    {
        if (rate <= -1d || !double.IsFinite(rate)) return double.NaN;

        var denominator = 1d + rate;
        var value = 0d;
        foreach (var flow in flows)
        {
            var years = (flow.Date - baseDate).TotalDays / 365d;
            value += flow.Amount / Math.Pow(denominator, years);
        }

        return value;
    }

    private static bool HasOppositeSigns(double first, double second) =>
        !double.IsNaN(first)
        && !double.IsNaN(second)
        && ((first < 0d && second > 0d) || (first > 0d && second < 0d));

    private static bool HasOppositeSigns(IEnumerable<XirrCashFlow> flows)
    {
        var hasNegative = flows.Any(flow => flow.Amount < 0m);
        var hasPositive = flows.Any(flow => flow.Amount > 0m);
        return hasNegative && hasPositive;
    }

    private static bool IsNearZero(double value) => double.IsFinite(value) && Math.Abs(value) <= _valueTolerance;
}