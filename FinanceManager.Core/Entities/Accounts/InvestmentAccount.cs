using FinanceManager.Core.Enums;
using FinanceManager.Core.Extensions;

namespace FinanceManager.Core.Entities.Accounts
{
    public class InvestmentAccount : FinancialAccountBase<InvestmentEntry>
    {
        public InvestmentAccount(int id, string name, IEnumerable<InvestmentEntry> entries) : base(id, name)
        {
            Entries = entries.ToList();
        }
        public InvestmentAccount(int id, string name) : base(id, name)
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
        public virtual void Add(IEnumerable<InvestmentEntry> entries, bool recalculateValues = true)
        {
            foreach (var entry in entries)
                Add(entry, recalculateValues);
        }

        public void Add(AddInvestmentEntryDto entry)
        {
            Entries ??= new List<InvestmentEntry>();
            var alredyExistingEntry = Entries.FirstOrDefault(x => x.PostingDate == entry.PostingDate && x.Ticker == entry.Ticker && x.ValueChange == entry.ValueChange);
            if (alredyExistingEntry is not null)
            {
                Console.WriteLine($"WARNING - Entry already exist, can not be added: Id:{alredyExistingEntry.Id}, Posting date{alredyExistingEntry.PostingDate}, " +
                    $"Ticker {alredyExistingEntry.Ticker}, Value change {alredyExistingEntry.ValueChange}");
                return;
            }

            var previousEntry = Entries.GetPrevious(entry.PostingDate).FirstOrDefault();
            var index = -1;

            if (previousEntry is not null)
                index = Entries.IndexOf(previousEntry);
            InvestmentEntry newEntry = null;
            if (index == -1)
            {
                index = Entries.Count();
                newEntry = new InvestmentEntry(index, entry.PostingDate, entry.ValueChange, entry.ValueChange, entry.Ticker, entry.InvestmentType);
                Entries.Add(newEntry);
                index = Entries.Count() - 1;
            }
            else
            {
                newEntry = new InvestmentEntry(index, entry.PostingDate, entry.ValueChange, entry.ValueChange, entry.Ticker, entry.InvestmentType);
                Entries.Insert(index, newEntry);
            }
            RecalculateEntryValues(index, newEntry);
        }


        public override void Add(InvestmentEntry entry, bool recalculateValues = true)
        {
            Entries ??= new List<InvestmentEntry>();
            var alredyExistingEntry = Entries.FirstOrDefault(x => x.PostingDate == entry.PostingDate && x.Ticker == entry.Ticker && x.ValueChange == entry.ValueChange);
            if (alredyExistingEntry is not null)
            {
                Console.WriteLine($"WARNING - Entry already exist, can not be added: Id:{alredyExistingEntry.Id}, Posting date{alredyExistingEntry.PostingDate}, " +
                    $"Ticker {alredyExistingEntry.Ticker}, Value change {alredyExistingEntry.ValueChange}");
                return;
            }

            var previousEntry = Entries.GetPrevious(entry.PostingDate).FirstOrDefault();
            var index = -1;

            if (previousEntry is not null)
                index = Entries.IndexOf(previousEntry);

            if (index == -1)
            {
                Entries.Add(entry);
                index = Entries.Count() - 1;
            }
            else
            {
                Entries.Insert(index, entry);
            }

            if (recalculateValues)
                RecalculateEntryValues(index);

        }
        public override void Update(InvestmentEntry entry, bool recalculateValues = true)
        {
            Entries ??= new List<InvestmentEntry>();

            var entryToUpdate = Entries.FirstOrDefault(x => x.Id == entry.Id);
            if (entryToUpdate is null) return;

            entryToUpdate.Update(entry);
            Entries.Remove(entryToUpdate);
            var previousEntry = Entries.GetPrevious(entryToUpdate.PostingDate).FirstOrDefault();

            if (previousEntry is null)
            {
                Entries.Add(entryToUpdate);
            }
            else
            {
                var newIndex = Entries.IndexOf(previousEntry);
                Entries.Insert(newIndex, entryToUpdate);
            }

            var index = Entries.IndexOf(entryToUpdate);
            if (recalculateValues)
                RecalculateEntryValues(index, entryToUpdate);
        }
        public override void Remove(int id)
        {
            if (Entries is null) return;

            var entry = Entries.FirstOrDefault(x => x.Id == id);
            if (entry is null) return;
            var index = Entries.IndexOf(entry);

            Entries.RemoveAt(index);
            RecalculateEntryValues(index - 1, entry);
        }

        private void RecalculateEntryValues(int? startingIndex, InvestmentEntry? startEntry)
        {
            if (Entries is null) return;
            InvestmentEntry? previousIterationEntry = startEntry;
            int startIndex = startingIndex.HasValue ? startingIndex.Value : Entries.Count() - 1;

            for (int i = startIndex; i >= 0; i--)
            {
                if (previousIterationEntry is not null && Entries[i].Ticker != previousIterationEntry.Ticker) continue;
                if (Entries.Count() < i) continue;

                if (i == startIndex && Entries[i] is not null)
                {
                    var date = Entries[i].PostingDate;
                    var ticker = Entries[i].Ticker;
                    var previousElements = Entries.GetPrevious(date, ticker);
                    if (previousElements is not null)
                        previousIterationEntry = previousElements.FirstOrDefault();
                }

                if (previousIterationEntry is not null)
                    Entries[i].Value = previousIterationEntry.Value + Entries[i].ValueChange; // error

                previousIterationEntry = Entries[i];
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
