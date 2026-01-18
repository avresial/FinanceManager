using FinanceManager.Domain.Entities.FinancialAccounts.Currency;

namespace FinanceManager.Domain.Extensions;

public static class CurrencyAccountEntryExtension
{
    public static List<(DateTime, decimal)> GetAssets(this List<CurrencyAccountEntry> currencyAccountEntries, DateTime start, DateTime end)
    {
        List<(DateTime, decimal)> result = [];
        if (currencyAccountEntries is null || !currencyAccountEntries.Any()) return result;

        for (DateTime i = end; i >= start; i = i.AddDays(-1))
        {
            var newestEntry = currencyAccountEntries.Get(i).OrderByDescending(x => x.PostingDate).FirstOrDefault();
            if (newestEntry is null) continue;

            result.Add((i, newestEntry.Value));
        }

        return result;
    }
}