using FinanceManager.Core.Extensions;

namespace FinanceManager.Core.Entities.Accounts
{
    public class FinancialAccountBase<T> : FinancialAccountBase where T : FinancialEntryBase
    {
        public override DateTime? Start => GetStartDate();
        public override DateTime? End => GetEndDate();

        public List<T>? Entries { get; protected set; }

        public FinancialAccountBase(int id, string name)
        {
            Id = id;
            Name = name;
        }
        public virtual IEnumerable<T> Get()
        {
            if (Entries is null) return [];
            return Entries;
        }
        public virtual IEnumerable<T> Get(DateTime date)
        {
            if (Entries is null) return [];
            return Entries.Get(date);
        }

        public virtual IEnumerable<T> Get(DateTime start, DateTime end)
        {
            if (Entries is null) return [];

            return Entries.Where(x => x.PostingDate >= start && x.PostingDate <= end);
        }


        public virtual void Add(T entry, bool recalculateValues = true)
        {
            Entries ??= new List<T>();
            var alredyExistingEntry = Entries.FirstOrDefault(x => x.PostingDate == entry.PostingDate && x.ValueChange == entry.ValueChange);
            if (alredyExistingEntry is not null)
            {
                Console.WriteLine($"WARNING - Entry already exist, can not be added: Id:{alredyExistingEntry.Id}, Posting date{alredyExistingEntry.PostingDate}, Value change {alredyExistingEntry.ValueChange}");
                return;
            }
            var previousEntry = Entries.GetPrevious(entry.PostingDate).FirstOrDefault();

            if (Entries is null) return;

            var index = Entries.IndexOf(previousEntry);

            if (index < 0)
                Entries.Add(entry);
            else
                Entries.Insert(index, entry);

            if (recalculateValues)
                RecalculateEntryValues(index);
        }

        public virtual void Add(IEnumerable<T> entries, bool recalculateValues = true)
        {
            foreach (var entry in entries)
                Add(entry, recalculateValues);
        }

        public virtual void Update(T entry, bool recalculateValues = true)
        {
            Entries ??= new List<T>();

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

        public virtual void Remove(int id)
        {
            if (Entries is null) return;

            var entry = Entries.FirstOrDefault(x => x.Id == id);
            if (entry is null) return;
            var indexToRemove = Entries.IndexOf(entry);
            Entries.RemoveAt(indexToRemove);

            if (indexToRemove > 0)
                Entries[indexToRemove - 1].Value -= entry.ValueChange;

            RecalculateEntryValues(indexToRemove - 1);
        }
        public int? GetMaxId()
        {
            if (Entries is null || !Entries.Any())
                return null;

            return Entries.Max(x => x.Id);
        }
        public IEnumerable<T> GetDaily()
        {
            throw new NotImplementedException();
        }

        public IEnumerable<T> GetMonthly()
        {
            throw new NotImplementedException();
        }

        public IEnumerable<T> GetYearly()
        {
            throw new NotImplementedException();
        }

        public IEnumerable<T> GetExpenses()
        {
            throw new NotImplementedException();
        }

        public IEnumerable<T> GetEarnings()
        {
            throw new NotImplementedException();
        }

        internal void RecalculateEntryValues(int? startingIndex)
        {
            if (Entries is null || !Entries.Any()) return;

            int startIndex = startingIndex.HasValue ? startingIndex.Value : Entries.Count() - 1;
            for (int i = startIndex; i >= 0; i--)
            {
                if (Entries.Count() - 1 <= i)
                    continue;

                var newValue = Entries[i + 1].Value + Entries[i].ValueChange;
                if (Entries[i].Value != newValue)
                {
                    var difference = Entries[i].Value - newValue;
                }
                Entries[i].Value = newValue;
            }
        }
        private DateTime? GetStartDate()
        {
            if (Entries is null || !Entries.Any()) return null;
            var minDate = Entries.Min(x => x.PostingDate);
            return Entries.First(x => x.PostingDate == minDate).PostingDate;
        }
        private DateTime? GetEndDate()
        {
            if (Entries is null || !Entries.Any()) return null;
            var maxDate = Entries.Max(x => x.PostingDate);
            return Entries.First(x => x.PostingDate == maxDate).PostingDate;
        }
    }
}
