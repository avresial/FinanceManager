using FinanceManager.Core.Enums;
using FinanceManager.Core.Extensions;

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

            return Entries.GetStoredTypes();
        }
        public override IEnumerable<InvestmentEntry> Get(DateTime date)
        {
            if (Entries is null) return [];
            return Entries.Get(date);
        }
        public override void Add(InvestmentEntry entry)
        {
            Entries ??= new List<InvestmentEntry>();
            var previousEntry = Entries.GetPrevious(entry.PostingDate, entry.Ticker).FirstOrDefault();
            var index = -1;

            if (previousEntry is not null)
                index = Entries.IndexOf(previousEntry);

            if (index == -1)
            {
                entry.Value = entry.ValueChange;
                if (Entries is not null)
                    Entries.Insert(0, entry);
            }
            else
            {
                if (Entries is not null)
                    Entries.Insert(index, entry);

                InvestmentEntry previousIterationEntry = null;

                for (int i = index; i >= 0; i--)
                {
                    if (Entries[i].Ticker != entry.Ticker) continue;
                    if (Entries.Count() < i) continue;

                    if (i == index) previousIterationEntry = Entries.GetPrevious(Entries[i].PostingDate, Entries[i].Ticker).FirstOrDefault();

                    if (previousIterationEntry is null) continue;

                    Entries[i].Value = previousIterationEntry.Value + Entries[i].ValueChange; // error
                    previousIterationEntry = Entries[i];
                }
            }
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
