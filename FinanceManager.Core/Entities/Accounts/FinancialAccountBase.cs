﻿using FinanceManager.Core.Extensions;

namespace FinanceManager.Core.Entities.Accounts
{
    public class FinancialAccountBase<T> : FinancialAccountBase where T : FinancialEntryBase
    {
        public override DateTime? Start => GetStartDate();
        public override DateTime? End => GetEndDate();

        public List<T>? Entries { get; protected set; }

        public FinancialAccountBase(string name)
        {
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

            var previousEntry = Entries.GetPrevious(entry.PostingDate).FirstOrDefault();

            var index = Entries.IndexOf(previousEntry);
            if (index < 0)
            {
                if (Entries is not null)
                    Entries.Add(entry);
            }
            else
            {
                if (Entries is not null)
                    Entries.Insert(index, entry);

                if (recalculateValues)
                    RecalculateEntryValues(index);
            }
        }



        public virtual void Add(IEnumerable<T> entries, bool recalculateValues = true)
        {
            foreach (var entry in entries)
                Add(entry, recalculateValues);
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

        private void RecalculateEntryValues(int? startingIndex)
        {
            if (Entries is null || !Entries.Any()) return;

            int startIndex = startingIndex.HasValue ? startingIndex.Value : Entries.Count() - 1;
            for (int i = startIndex; i >= 0; i--)
            {
                if (Entries.Count() < i) continue;
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
