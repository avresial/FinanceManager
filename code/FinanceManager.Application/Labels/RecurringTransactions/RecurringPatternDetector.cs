using FinanceManager.Domain.Labels.Entities;

namespace FinanceManager.Application.Labels.RecurringTransactions;

/// <summary>
/// Groups transactions into merchant/amount clusters and keeps the clusters that repeat on a
/// regular, still-active cadence with a meaningful monthly amount. Pure: no I/O.
/// </summary>
internal static class RecurringPatternDetector
{
    private const decimal _minimumMonthlyAverage = 5m;

    public static List<RecurringPattern> Detect(IEnumerable<RecurringEntry> entries, DateTime asOf, bool isIncome) =>
        Cluster(entries)
            .Select(cluster => BuildPattern(cluster[0].Merchant, cluster, asOf, isIncome))
            .OfType<RecurringPattern>()
            .Where(x => x.Result.MonthlyCost >= _minimumMonthlyAverage)
            .ToList();

    // Entries join the first cluster with a similar merchant whose amounts all stay within tolerance
    // of the cluster's median once the entry is added.
    private static List<List<RecurringEntry>> Cluster(IEnumerable<RecurringEntry> entries)
    {
        var result = new List<List<RecurringEntry>>();

        foreach (var entry in entries.OrderBy(x => x.Date))
        {
            var cluster = result.FirstOrDefault(candidate =>
            {
                if (!MerchantSimilarity.IsSameMerchant(candidate[0].Merchant, entry.Merchant))
                    return false;

                List<RecurringEntry> prospective = [.. candidate, entry];
                var referenceAmount = RecurringAmounts.Median(prospective.Select(x => x.Amount));
                return prospective.All(item => RecurringAmounts.AreSimilar(referenceAmount, item.Amount));
            });

            if (cluster is null)
            {
                result.Add([entry]);
                continue;
            }

            cluster.Add(entry);
        }

        return result;
    }

    private static RecurringPattern? BuildPattern(
        string name,
        List<RecurringEntry> entries,
        DateTime asOf,
        bool isIncome)
    {
        var occurrences = entries
            .GroupBy(x => x.Date.Date)
            .Select(group => new Occurrence(
                group.Key,
                group.Sum(x => x.Amount),
                group.Select(x => new RecurringTransactionEntryReference(x.AccountId, x.EntryId)).ToList()))
            .OrderBy(x => x.Date)
            .ToList();
        if (occurrences.Count < 2) return null;

        var referenceAmount = RecurringAmounts.Median(entries.Select(x => x.Amount));

        var gaps = occurrences.Zip(occurrences.Skip(1), (previous, current) => (current.Date - previous.Date).TotalDays).ToList();
        var cadence = RecurringCadenceRules.Detect(gaps);
        if (cadence is null || occurrences.Count < RecurringCadenceRules.MinimumOccurrences(cadence.Value)) return null;

        var last = occurrences[^1];
        if (asOf.Date - last.Date > RecurringCadenceRules.MaximumAge(cadence.Value)) return null;

        var monthlyCost = RecurringCadenceRules.ToMonthly(occurrences.Average(x => x.Amount), cadence.Value);
        var previous = occurrences[^2];
        var nextCharge = RecurringCadenceRules.Add(last.Date, cadence.Value);
        while (nextCharge <= asOf.Date)
            nextCharge = RecurringCadenceRules.Add(nextCharge, cadence.Value);

        return new(
            MerchantSimilarity.Normalize(name),
            referenceAmount,
            new RecurringTransactionResult
            {
                Name = name,
                Value = monthlyCost,
                Cadence = cadence.Value,
                LastChargeDate = last.Date,
                NextExpectedChargeDate = nextCharge,
                LastAmount = Math.Round(last.Amount, 2),
                PreviousAmount = Math.Round(previous.Amount, 2),
                MonthlyCost = monthlyCost,
                AnnualCost = monthlyCost * 12m,
                IsIncome = isIncome,
                Entries = occurrences.SelectMany(x => x.Entries).ToList()
            });
    }

    private sealed record Occurrence(DateTime Date, decimal Amount, List<RecurringTransactionEntryReference> Entries);
}