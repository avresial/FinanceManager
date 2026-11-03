namespace FinanceManager.Application.Labels.RecurringTransactions;

internal static class RecurringAmounts
{
    private const decimal _tolerance = 0.05m;

    /// <summary>True when <paramref name="candidateAmount"/> is within 5% of <paramref name="referenceAmount"/>.</summary>
    public static bool AreSimilar(decimal referenceAmount, decimal candidateAmount) =>
        Math.Abs(candidateAmount - referenceAmount) <= referenceAmount * _tolerance;

    public static decimal Median(IEnumerable<decimal> values)
    {
        var ordered = values.Order().ToList();
        var middle = ordered.Count / 2;
        return ordered.Count % 2 == 0
            ? (ordered[middle - 1] + ordered[middle]) / 2m
            : ordered[middle];
    }
}