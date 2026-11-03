using FinanceManager.Domain.Labels.Entities;

namespace FinanceManager.Application.Labels.RecurringTransactions;

/// <summary>
/// Gap ranges, minimum occurrence counts, staleness limits and monthly normalisation for each
/// <see cref="RecurringCadence"/>.
/// </summary>
internal static class RecurringCadenceRules
{
    /// <summary>
    /// The cadence whose gap range contains the median gap, provided at least 70% of all gaps fall
    /// inside that range; otherwise null.
    /// </summary>
    public static RecurringCadence? Detect(IReadOnlyList<double> gaps)
    {
        var ordered = gaps.Order().ToList();
        var median = ordered[ordered.Count / 2];
        RecurringCadence? cadence = median switch
        {
            >= 5 and <= 10 => RecurringCadence.Weekly,
            >= 20 and <= 45 => RecurringCadence.Monthly,
            >= 70 and <= 110 => RecurringCadence.Quarterly,
            >= 330 and <= 400 => RecurringCadence.Annual,
            _ => null
        };
        if (cadence is null) return null;

        var regularGaps = gaps.Count(gap => cadence.Value switch
        {
            RecurringCadence.Weekly => gap is >= 5 and <= 10,
            RecurringCadence.Monthly => gap is >= 20 and <= 45,
            RecurringCadence.Quarterly => gap is >= 70 and <= 110,
            RecurringCadence.Annual => gap is >= 330 and <= 400,
            _ => false
        });

        return regularGaps * 10 >= gaps.Count * 7 ? cadence : null;
    }

    public static int MinimumOccurrences(RecurringCadence cadence) =>
        cadence is RecurringCadence.Weekly ? 4 : cadence is RecurringCadence.Monthly ? 3 : 2;

    public static DateTime Add(DateTime date, RecurringCadence cadence) =>
        cadence switch
        {
            RecurringCadence.Weekly => date.AddDays(7),
            RecurringCadence.Monthly => date.AddMonths(1),
            RecurringCadence.Quarterly => date.AddMonths(3),
            RecurringCadence.Annual => date.AddYears(1),
            _ => date
        };

    /// <summary>How long after the last occurrence a pattern still counts as active.</summary>
    public static TimeSpan MaximumAge(RecurringCadence cadence) =>
        cadence switch
        {
            RecurringCadence.Weekly => TimeSpan.FromDays(21),
            RecurringCadence.Monthly => TimeSpan.FromDays(60),
            RecurringCadence.Quarterly => TimeSpan.FromDays(150),
            RecurringCadence.Annual => TimeSpan.FromDays(450),
            _ => TimeSpan.Zero
        };

    public static decimal ToMonthly(decimal amountPerOccurrence, RecurringCadence cadence) =>
        Math.Round(cadence switch
        {
            RecurringCadence.Weekly => amountPerOccurrence * 52m / 12m,
            RecurringCadence.Monthly => amountPerOccurrence,
            RecurringCadence.Quarterly => amountPerOccurrence / 3m,
            RecurringCadence.Annual => amountPerOccurrence / 12m,
            _ => amountPerOccurrence
        }, 2);
}