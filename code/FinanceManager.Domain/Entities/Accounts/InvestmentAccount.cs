using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Extensions;

namespace FinanceManager.Domain.Entities.Accounts
{
    public class InvestmentAccount : FinancialAccountBase<InvestmentEntry>
    {
        public readonly Dictionary<string, DateTime> OlderThenLoadedEntry = new();
        public readonly Dictionary<string, DateTime> YoungerThenLoadedEntry = new();

        public InvestmentAccount(int id, string name, IEnumerable<InvestmentEntry> entries, Dictionary<string, DateTime>? olderThenLoadedEntry = null, Dictionary<string, DateTime>? youngerThenLoadedEntry = null)
            : base(id, name)
        {
            Entries = entries.ToList();
            OlderThenLoadedEntry = olderThenLoadedEntry ?? [];
            YoungerThenLoadedEntry = youngerThenLoadedEntry;
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
        public override void Add(IEnumerable<InvestmentEntry> entries, bool recalculateValues = true)
        {
            foreach (var entry in entries)
                Add(entry, recalculateValues);
        }
        public int GetNextFreeId()
        {
            var currentMaxId = GetMaxId();
            if (currentMaxId is not null)
                return currentMaxId.Value + 1;
            return 0;
        }
        public void Add(AddInvestmentEntryDto entry)
        {
            Entries ??= new List<InvestmentEntry>();
            var alredyExistingEntry = Entries.FirstOrDefault(x => x.PostingDate == entry.PostingDate && x.Ticker == entry.Ticker && x.ValueChange == entry.ValueChange);
            if (alredyExistingEntry is not null)
            {
                throw new Exception($"WARNING - Entry already exist, can not be added: Id:{alredyExistingEntry.Id}, Posting date{alredyExistingEntry.PostingDate}, " +
                    $"Ticker {alredyExistingEntry.Ticker}, Value change {alredyExistingEntry.ValueChange}");
            }

            var previousEntry = Entries.GetPrevious(entry.PostingDate).FirstOrDefault();
            var index = -1;

            if (previousEntry is not null)
                index = Entries.IndexOf(previousEntry);

            if (index == -1)
            {
                index = Entries.Count();
                Entries.Add(new InvestmentEntry(GetNextFreeId(), entry.PostingDate, entry.ValueChange, entry.ValueChange, entry.Ticker, entry.InvestmentType));
                index -= 1;
            }
            else
            {
                Entries.Insert(index, new InvestmentEntry(GetNextFreeId(), entry.PostingDate, entry.ValueChange, entry.ValueChange, entry.Ticker, entry.InvestmentType));
            }

            RecalculateEntryValues(index);
        }
        public override void Add(InvestmentEntry entry, bool recalculateValues = true)
        {
            Entries ??= new List<InvestmentEntry>();
            var alredyExistingEntry = Entries.FirstOrDefault(x => x.PostingDate == entry.PostingDate && x.Ticker == entry.Ticker && x.ValueChange == entry.ValueChange);
            if (alredyExistingEntry is not null)
            {
                throw new Exception($"WARNING - Entry already exist, can not be added: Id:{alredyExistingEntry.Id}, Posting date{alredyExistingEntry.PostingDate}, " +
                    $"Ticker {alredyExistingEntry.Ticker}, Value change {alredyExistingEntry.ValueChange}");
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
        public override void UpdateEntry(InvestmentEntry entry, bool recalculateValues = true)
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
                RecalculateEntryValues(index);
        }
        public override void Remove(int id)
        {
            if (Entries is null) return;

            var entry = Entries.FirstOrDefault(x => x.Id == id);
            if (entry is null) return;
            var index = Entries.IndexOf(entry);

            Entries.RemoveAt(index);
            RecalculateEntryValues(index - 1);
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
        private new void RecalculateEntryValues(int? startingIndex)
        {
            if (Entries is null) return;
            int startIndex = startingIndex.HasValue ? startingIndex.Value : Entries.Count() - 1;

            for (int i = startIndex; i >= 0; i--)
            {
                if (Entries.Count() < i) continue;

                InvestmentEntry? previousIterationEntry = null; // could be stored in local dictionary to improve speed
                var previousElements = Entries.GetPrevious(Entries[i].PostingDate, Entries[i].Ticker);
                if (previousElements is not null && previousElements.Any())
                    previousIterationEntry = previousElements.FirstOrDefault();

                if (previousIterationEntry is not null)
                {
                    Entries[i].Value = previousIterationEntry.Value + Entries[i].ValueChange;
                }
                else
                {
                    Entries[i].Value = Entries[i].ValueChange;
                }


                previousIterationEntry = Entries[i];
            }
        }
    }
}
