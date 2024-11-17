using FinanceManager.Core.Entities;
using FinanceManager.Core.Entities.Accounts;
using FinanceManager.Core.Enums;

namespace FinanceManager.Core.Extensions
{
    public static class InvestmentEntryExtension
    {
        public static async Task<List<(DateTime, decimal)>> GetAssets(this IEnumerable<InvestmentEntry> bankAccountEntries,
            DateTime start, DateTime end, Func<string, DateTime, Task<StockPrice>> getStockPrice)
        {
            List<(DateTime, decimal)> result = new();
            if (bankAccountEntries is null || !bankAccountEntries.Any()) return result;
            List<string> tickers = bankAccountEntries.GetStoredTickers();
            for (DateTime i = end; i >= start; i = i.AddDays(-1))
            {
                decimal price = 0;
                foreach (var ticker in tickers)
                {
                    var entries = bankAccountEntries.Get(i);
                    var newestEntry = bankAccountEntries.Get(i).OrderByDescending(x => x.PostingDate).FirstOrDefault(x => x.Ticker == ticker);
                    if (newestEntry is null) continue;
                    var stockPrice = await getStockPrice(newestEntry.Ticker, newestEntry.PostingDate);
                    price += newestEntry.Value * stockPrice.PricePerUnit;
                }
                //var newestEntry = bankAccountEntries.Get(i).OrderByDescending(x => x.PostingDate).FirstOrDefault();
                //if (newestEntry is null) continue;
                //var stockPrice = await getStockPrice(newestEntry.Ticker, newestEntry.PostingDate);
                //result.Add((i, newestEntry.Value * stockPrice.PricePerUnit));
                result.Add((i, price));
            }

            return result;
        }

        public static List<InvestmentType> GetStoredTypes(this IEnumerable<InvestmentEntry> bankAccountEntries)
        {
            if (bankAccountEntries is null) return [];

            return bankAccountEntries.DistinctBy(x => x.InvestmentType).Select(x => x.InvestmentType).ToList();
        }
        public static List<string> GetStoredTickers(this IEnumerable<InvestmentEntry> bankAccountEntries)
        {
            if (bankAccountEntries is null) return [];

            return bankAccountEntries.DistinctBy(x => x.Ticker).Select(x => x.Ticker).ToList();
        }
        public static IEnumerable<InvestmentEntry> GetPrevious(this IEnumerable<InvestmentEntry> entries, DateTime date, string ticker)
        {
            var lastEntry = entries.FirstOrDefault(x => x.PostingDate < date && x.Ticker == ticker);
            if (lastEntry is null) return [];

            return [lastEntry];
        }
        public static IEnumerable<InvestmentEntry> Get(this IEnumerable<InvestmentEntry> bankAccountEntries, DateTime date) // needs to be upgraded
        {
            if (bankAccountEntries is null) return [];

            var entries = bankAccountEntries.Where(x => x.PostingDate.Year == date.Year && x.PostingDate.Month == date.Month && x.PostingDate.Day == date.Day).ToList();
            if (entries.DistinctBy(x => x.Ticker).Count() == bankAccountEntries.GetStoredTickers().Count())
                return entries;

            foreach (var ticker in bankAccountEntries.GetStoredTickers())
            {
                if (entries.Any(x => x.Ticker == ticker)) continue;
                entries.Add(bankAccountEntries.First(x => x.Ticker == ticker));
            }

            return entries;
        }
    }
}
