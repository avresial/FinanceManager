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

        public virtual void Add(T entry)
        {
            Entries ??= new List<T>();
            //  Entries.Add(entry);
            // Entries = Entries.OrderByDescending(x => x.PostingDate).ToList();

            var previousEntry = Entries.GetPrevious(entry.PostingDate).FirstOrDefault();

            var index = Entries.IndexOf(previousEntry);
            if (index == -1)
            {
                if (Entries is not null)
                    Entries.Insert(0, entry);
            }
            else
            {
                if (Entries is not null)
                    Entries.Insert(index, entry);

                for (int i = index; i >= 0; i--)
                {
                    if (Entries.Count() < i) continue;

                    Entries[i].Value = Entries[i + 1].Value + Entries[i].ValueChange;
                }
            }


        }

        public virtual void Add(IEnumerable<T> entries)
        {
            foreach (var entry in entries)
                Add(entry);

            return;
            Entries ??= new List<T>();
            Entries.AddRange(entries);
            Entries = Entries.OrderByDescending(x => x.PostingDate).ToList();
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
