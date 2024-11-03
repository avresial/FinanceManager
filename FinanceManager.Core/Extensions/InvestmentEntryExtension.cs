using FinanceManager.Core.Entities.Accounts;

namespace FinanceManager.Core.Extensions
{
    public static class InvestmentEntryExtension
    {
        public static List<(DateTime, decimal)> GetAssets(this List<InvestmentEntry> bankAccountEntries, DateTime start, DateTime end)
        {
            List<(DateTime, decimal)> result = new();
            if (bankAccountEntries is null || !bankAccountEntries.Any()) return result;

            for (DateTime i = end; i >= start; i = i.AddDays(-1))
            {
                var newestEntry = bankAccountEntries.Get(i).OrderByDescending(x => x.PostingDate).FirstOrDefault();
                if (newestEntry is null) continue;

                result.Add((i, newestEntry.Value));
            }

            return result;
        }
    }
}
