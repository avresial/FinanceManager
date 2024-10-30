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
            var entries = Entries.Where(x => x.PostingDate.Year == date.Year && x.PostingDate.Month == date.Month && x.PostingDate.Day == date.Day);
            if (entries.Any()) return entries;

            var lastEntry = Entries.Where(x => x.PostingDate <= date).FirstOrDefault();
            if (lastEntry is null) return [];

            return [lastEntry];
        }

        public virtual IEnumerable<T> Get(DateTime start, DateTime end)
        {
            if (Entries is null) return [];

            return Entries.Where(x => x.PostingDate >= start && x.PostingDate <= end);
        }

        public virtual void Add(T entry)
        {
            Entries ??= new List<T>();
            Entries.Add(entry);
            Entries = Entries.OrderByDescending(x => x.PostingDate).ToList();
        }

        public virtual void Add(IEnumerable<T> entries)
        {
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
