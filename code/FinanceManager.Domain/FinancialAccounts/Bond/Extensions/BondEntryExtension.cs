using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Stock.Entities;

namespace FinanceManager.Domain.FinancialAccounts.Bond.Extensions;

public static class BondEntryExtension
{
    public static IEnumerable<BondAccountEntry> GetNextOlder(this IEnumerable<BondAccountEntry> entries, DateTime date, int BondDetailsId)
    {
        var lastEntry = entries.Where(x => x.PostingDate < date && x.BondDetailsId == BondDetailsId).OrderByDescending(x => x.PostingDate).FirstOrDefault();
        if (lastEntry is null) return [];

        return [lastEntry];
    }
    public static BondAccountEntry? GetThisOrNextOlder(this IEnumerable<BondAccountEntry> accountEntries, DateTime date, int BondDetailsId)
    {
        var result = accountEntries.Get(date, BondDetailsId);

        if (result is not null) return result;

        return accountEntries.GetNextOlder(date, BondDetailsId).OrderByDescending(x => x.PostingDate).FirstOrDefault();
    }
    public static BondAccountEntry? Get(this IEnumerable<BondAccountEntry> accountEntries, DateTime date, int BondDetailsId)
    {
        if (accountEntries is null) return default;

        return accountEntries.Where(x => x.PostingDate.Year == date.Year && x.PostingDate.Month == date.Month && x.PostingDate.Day == date.Day &&
         x.BondDetailsId == BondDetailsId).OrderByDescending(x => x.PostingDate).FirstOrDefault();
    }
}