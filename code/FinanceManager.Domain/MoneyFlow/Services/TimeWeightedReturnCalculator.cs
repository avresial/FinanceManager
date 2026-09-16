using FinanceManager.Domain.MoneyFlow.Entities;

namespace FinanceManager.Domain.MoneyFlow.Services;

/// <summary>Links dated portfolio sub-period returns after external cash flows are removed.</summary>
public static class TimeWeightedReturnCalculator
{
    private const int _maximumPrecision = 10;

    public static TimeWeightedReturnResult Calculate(
        IEnumerable<TimeWeightedReturnPeriod> periods,
        DateTime startDate,
        DateTime endDate)
    {
        var normalizedStart = startDate.Date;
        var normalizedEnd = endDate.Date;
        if (startDate == default || endDate == default || normalizedEnd < normalizedStart)
            return Result(null, TimeWeightedReturnStatus.InsufficientData, normalizedStart, normalizedEnd);

        var orderedPeriods = periods
            .Where(period => period.Date.Date >= normalizedStart && period.Date.Date <= normalizedEnd)
            .OrderBy(period => period.Date.Date)
            .ToList();
        if (orderedPeriods.Count == 0)
            return Result(null, TimeWeightedReturnStatus.InsufficientData, normalizedStart, normalizedEnd);

        if (orderedPeriods
            .GroupBy(period => period.Date.Date)
            .Any(group => group.Count() > 1))
            return Result(null, TimeWeightedReturnStatus.Unavailable, normalizedStart, normalizedEnd);

        decimal linkedFactor = 1m;
        var hasMeaningfulPeriod = false;

        foreach (var period in orderedPeriods)
        {
            if (period.StartingValue < 0m || period.EndingValue < 0m)
                return Result(null, TimeWeightedReturnStatus.Unavailable, normalizedStart, normalizedEnd);

            if (period.StartingValue == 0m
                && period.ExternalCashFlow == 0m
                && period.EndingValue == 0m)
                continue;

            try
            {
                var adjustedStartingValue = period.StartingValue + period.ExternalCashFlow;
                if (adjustedStartingValue <= 0m)
                    return Result(null, TimeWeightedReturnStatus.Unavailable, normalizedStart, normalizedEnd);

                linkedFactor *= period.EndingValue / adjustedStartingValue;
            }
            catch (OverflowException)
            {
                return Result(null, TimeWeightedReturnStatus.Unavailable, normalizedStart, normalizedEnd);
            }

            hasMeaningfulPeriod = true;
        }

        return hasMeaningfulPeriod
            ? Result(Math.Round(linkedFactor - 1m, _maximumPrecision, MidpointRounding.AwayFromZero), TimeWeightedReturnStatus.Available, normalizedStart, normalizedEnd)
            : Result(null, TimeWeightedReturnStatus.InsufficientData, normalizedStart, normalizedEnd);
    }

    private static TimeWeightedReturnResult Result(
        decimal? totalReturn,
        TimeWeightedReturnStatus status,
        DateTime startDate,
        DateTime endDate) =>
        new(totalReturn, status, startDate, endDate);
}