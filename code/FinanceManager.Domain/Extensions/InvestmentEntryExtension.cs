using FinanceManager.Domain.Entities;
using FinanceManager.Domain.Entities.Accounts.Entries;
using FinanceManager.Domain.Enums;

namespace FinanceManager.Domain.Extensions
{
    public static class InvestmentEntryExtension
    {
        public static async Task<List<(DateTime, decimal)>> GetAssets(this IEnumerable<StockAccountEntry> accountEntries,
            DateTime start, DateTime end, Func<string, DateTime, Task<StockPrice>> getStockPrice)
        {
            List<(DateTime, decimal)> result = new();
            if (accountEntries is null || !accountEntries.Any()) return result;
            List<string> tickers = accountEntries.GetStoredTickers();
            for (DateTime i = end; i >= start; i = i.AddDays(-1))
            {
                decimal price = 0;
                foreach (var ticker in tickers)
                {
                    var entries = accountEntries.Get(i);
                    var newestEntry = accountEntries.Get(i).OrderByDescending(x => x.PostingDate).FirstOrDefault(x => x.Ticker == ticker);
                    if (newestEntry is null) continue;
                    var stockPrice = await getStockPrice(newestEntry.Ticker, newestEntry.PostingDate);
                    price += newestEntry.Value * stockPrice.PricePerUnit;
                }
                result.Add((i, price));
            }

            return result;
        }

        public static List<InvestmentType> GetStoredTypes(this IEnumerable<StockAccountEntry> accountEntries)
        {
            if (accountEntries is null) return [];

            return accountEntries.DistinctBy(x => x.InvestmentType).Select(x => x.InvestmentType).ToList();
        }
        public static List<string> GetStoredTickers(this IEnumerable<StockAccountEntry> accountEntries)
        {
            if (accountEntries is null) return [];

            return accountEntries.DistinctBy(x => x.Ticker).Select(x => x.Ticker).ToList();
        }
        public static IEnumerable<StockAccountEntry> GetPrevious(this IEnumerable<StockAccountEntry> entries, DateTime date, string ticker)
        {
            var lastEntry = entries.FirstOrDefault(x => x.PostingDate < date && x.Ticker == ticker);
            if (lastEntry is null) return [];

            return [lastEntry];
        }
        public static IEnumerable<StockAccountEntry> Get(this IEnumerable<StockAccountEntry> accountEntries, DateTime date) // needs to be upgraded
        {
            if (accountEntries is null) return [];

            var entries = accountEntries.Where(x => x.PostingDate.Year == date.Year && x.PostingDate.Month == date.Month && x.PostingDate.Day == date.Day).ToList();
            if (entries.DistinctBy(x => x.Ticker).Count() == accountEntries.GetStoredTickers().Count())
                return entries;

            foreach (var ticker in accountEntries.GetStoredTickers())
            {
                if (entries.Any(x => x.Ticker == ticker)) continue;
                var newEntry = accountEntries.FirstOrDefault(x => x.Ticker == ticker && x.PostingDate <= date);

                if (newEntry is not null)
                    entries.Add(newEntry);
            }

            return entries;
        }
    }
}
