using FinanceManager.Core.Enums;

namespace FinanceManager.Core.Entities.Accounts
{
    public class InvestmentAccount : FinancialAccountBase<InvestmentEntry>
    {
        public InvestmentAccount(string name, IEnumerable<InvestmentEntry> entries) : base(name)
        {
            Entries = entries.ToList();
        }
        public InvestmentAccount(string name) : base(name)
        {
            Entries = new List<InvestmentEntry>();
        }

        public List<InvestmentType> GetStoredTypes()
        {
            if (Entries is null) return [];

            return Entries.DistinctBy(x => x.InvestmentType).Select(x => x.InvestmentType).ToList();
        }
        public override IEnumerable<InvestmentEntry> Get(DateTime date) // needs to be upgraded
        {
            if (Entries is null) return [];

            var entries = Entries.Where(x => x.PostingDate.Year == date.Year && x.PostingDate.Month == date.Month && x.PostingDate.Day == date.Day).ToList();
            if (entries.DistinctBy(x => x.Ticker).Count() == GetStoredTickers().Count())
                return entries;

            foreach (var ticker in GetStoredTickers())
            {
                if (entries.Any(x => x.Ticker == ticker)) continue;
                entries.Add(Entries.First(x => x.Ticker == ticker));
            }

            return entries;
        }

        public List<string> GetStoredTickers()
        {
            if (Entries is null) return [];

            return Entries.DistinctBy(x => x.Ticker).Select(x => x.Ticker).ToList();
        }
        public async Task<Dictionary<DateOnly, decimal>> GetDailyPrice(Func<string, DateTime, Task<StockPrice>> getStockPrice)
        {
            var result = new Dictionary<DateOnly, decimal>();
            if (Entries is null || Start is null || End is null) return result;

            DateOnly index = DateOnly.FromDateTime(Start.Value.Date);

            Dictionary<string, decimal> lastTickerValue = new Dictionary<string, decimal>();

            while (index <= DateOnly.FromDateTime(End.Value))
            {
                var entriesOfTheDay = Entries.Where(x => DateOnly.FromDateTime(x.PostingDate) == index);
                decimal dailyPrice = 0;

                var countedTicker = GetStoredTickers();
                foreach (var entry in entriesOfTheDay)
                {
                    countedTicker.RemoveAll(x => x == entry.Ticker);
                    var price = entry.Value * (await getStockPrice(entry.Ticker, index.ToDateTime(new TimeOnly()))).PricePerUnit;
                    if (!lastTickerValue.ContainsKey(entry.Ticker))
                        lastTickerValue.Add(entry.Ticker, price);
                    else
                        lastTickerValue[entry.Ticker] = price;
                    dailyPrice += price;
                }

                foreach (var item in countedTicker)
                {
                    if (!lastTickerValue.ContainsKey(item)) continue;
                    dailyPrice += lastTickerValue[item];
                }

                result.Add(index, dailyPrice);
                index = index.AddDays(+1);
            }

            return result;
        }
    }
}
