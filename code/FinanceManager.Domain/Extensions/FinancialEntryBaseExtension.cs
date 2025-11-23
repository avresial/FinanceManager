using FinanceManager.Domain.Entities.Shared.Accounts;

namespace FinanceManager.Domain.Extensions;

public static class FinancialEntryBaseExtension
{
    public static IEnumerable<T> Get<T>(this IEnumerable<T> entries, DateTime date) where T : FinancialEntryBase
    {
        if (entries is null) return [];
        var result = entries.Where(x => x.PostingDate.Year == date.Year && x.PostingDate.Month == date.Month && x.PostingDate.Day == date.Day);
        if (result.Any()) return result;

        return entries.GetNextYounger(date);
    }

    public static IEnumerable<T> GetNextYounger<T>(this IEnumerable<T> entries, DateTime date) where T : FinancialEntryBase
    {
        var lastEntry = entries.Where(x => x.PostingDate <= date).FirstOrDefault();
        if (lastEntry is null) return [];

        return [lastEntry];
    }

    public static T? GetNextOlder<T>(this IEnumerable<T> entries, DateTime date) where T : FinancialEntryBase
    {
        var lastEntry = entries.FirstOrDefault(x => x.PostingDate < date);
        if (lastEntry is null) return default;

        return lastEntry;
    }
    public static T? GetThisOrNextOlder<T>(this IEnumerable<T> entries, DateTime date) where T : FinancialEntryBase
    {
        var results = entries.Get(date);
        if (results is null || !results.Any()) return default;

        T? result = results.OrderByDescending(x => x.PostingDate).FirstOrDefault();

        if (result is not null) return result;

        return entries.GetNextOlder(date);
    }


    public static List<FinancialEntryBase> GetEntriesMonthlyValue(this IEnumerable<FinancialEntryBase> entries)
    {

        List<FinancialEntryBase> orderedEntries = entries.OrderBy(x => x.PostingDate).ToList();
        if (orderedEntries.Count == 0) return [];

        var beginningDate = orderedEntries.First().PostingDate.Date;
        var endDate = orderedEntries.Last().PostingDate.Date;

        List<FinancialEntryBase> result = [];

        DateTime stepDate = new(beginningDate.Year, beginningDate.Month, 1);

        while (stepDate <= endDate)
        {
            var entriesForStepMonth = orderedEntries.Where(x => x.PostingDate.Year == stepDate.Year && x.PostingDate.Month == stepDate.Month);
            if (entriesForStepMonth is null || !entriesForStepMonth.Any())
            {
                stepDate = stepDate.AddMonths(1);
                continue;
            }

            int entryId = entriesForStepMonth.Last().EntryId;
            int accountId = entriesForStepMonth.Last().AccountId;

            FinancialEntryBase bankAccountEntry = new(accountId, entryId, stepDate.Date, Math.Round(entriesForStepMonth.Average(x => x.Value), 2),
                Math.Round(entriesForStepMonth.Sum(x => x.ValueChange), 2));

            result.Add(bankAccountEntry);
            stepDate = stepDate.AddMonths(1);
        }

        return result;
    }
    public static List<FinancialEntryBase> GetEntriesWeekly(this IEnumerable<FinancialEntryBase> entries)
    {
        List<FinancialEntryBase> result = [];

        var orderedEntries = entries.OrderBy(x => x.PostingDate).ToList();
        if (orderedEntries.Count == 0) return result;

        var beginningDate = orderedEntries.First().PostingDate.Date;
        var endDate = orderedEntries.Last().PostingDate.Date;

        DateTime stepDate = new(beginningDate.Year, beginningDate.Month, 1);
        stepDate = stepDate.AddDays(-(int)stepDate.DayOfWeek + 1);// might skip one day
        while (stepDate <= endDate)
        {
            var entriesForStepMonth = orderedEntries.Where(x => x.PostingDate >= stepDate && x.PostingDate < stepDate.AddDays(7));

            if (entriesForStepMonth is null || !entriesForStepMonth.Any())
            {
                stepDate = stepDate.AddDays(7);
                continue;
            }
            int entryId = entriesForStepMonth.Last().EntryId;
            int accountId = entriesForStepMonth.Last().AccountId;
            FinancialEntryBase bankAccountEntry = new(accountId, entryId, stepDate.Date, Math.Round(entriesForStepMonth.Average(x => x.Value), 2),
                Math.Round(entriesForStepMonth.Sum(x => x.ValueChange), 2));

            result.Add(bankAccountEntry);
            stepDate = stepDate.AddDays(7);
        }

        return result;
    }
}
