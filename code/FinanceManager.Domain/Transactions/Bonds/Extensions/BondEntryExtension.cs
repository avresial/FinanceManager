using FinanceManager.Domain.Transactions.Bonds.Entities;

namespace FinanceManager.Domain.Transactions.Bonds.Extensions;

public static class BondEntryExtension
{
    public static IEnumerable<BondEntry> GetNextOlder(this IEnumerable<BondEntry> entries, DateTime date, int BondDetailsId)
    {
        var lastEntry = entries.Where(x => x.PostingDate < date && x.BondDetailsId == BondDetailsId).OrderByDescending(x => x.PostingDate).FirstOrDefault();
        if (lastEntry is null) return [];

        return [lastEntry];
    }
    public static BondEntry? GetThisOrNextOlder(this IEnumerable<BondEntry> accountEntries, DateTime date, int BondDetailsId)
    {
        var result = accountEntries.Get(date, BondDetailsId);

        if (result is not null) return result;

        return accountEntries.GetNextOlder(date, BondDetailsId).OrderByDescending(x => x.PostingDate).FirstOrDefault();
    }
    public static BondEntry? Get(this IEnumerable<BondEntry> accountEntries, DateTime date, int BondDetailsId)
    {
        if (accountEntries is null) return default;

        return accountEntries.Where(x => x.PostingDate.Year == date.Year && x.PostingDate.Month == date.Month && x.PostingDate.Day == date.Day &&
         x.BondDetailsId == BondDetailsId).OrderByDescending(x => x.PostingDate).FirstOrDefault();
    }
}