using FinanceManager.Domain.FinancialAccounts.Shared.Entities;

namespace FinanceManager.Domain.FinancialAccounts.Shared.Extensions;

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
        var lastEntry = entries.Where(x => x.PostingDate <= date).OrderByDescending(x => x.PostingDate).FirstOrDefault();
        if (lastEntry is null) return [];

        return [lastEntry];
    }

    public static T? GetNextOlder<T>(this IEnumerable<T> entries, DateTime date) where T : FinancialEntryBase
    {
        var lastEntry = entries.Where(x => x.PostingDate < date).OrderByDescending(x => x.PostingDate).FirstOrDefault();
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

}