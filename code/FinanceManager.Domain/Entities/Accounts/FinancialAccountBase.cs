using FinanceManager.Domain.Extensions;

namespace FinanceManager.Domain.Entities.Accounts;

public class FinancialAccountBase<T> : BasicAccountInformation where T : FinancialEntryBase
{
    public override DateTime? Start => GetStartDate();
    public override DateTime? End => GetEndDate();

    public List<T> Entries { get; protected set; } = [];

    public FinancialAccountBase(int userId, int accountId, string name)
    {
        UserId = userId;
        AccountId = accountId;
        Name = name;
    }
    public virtual IEnumerable<T> Get() => Entries;
    public virtual IEnumerable<T> Get(DateTime date) => Entries.Get(date);
    public virtual IEnumerable<T> Get(DateTime start, DateTime end) => Entries.Where(x => x.PostingDate >= start && x.PostingDate <= end);

    public virtual bool ContainsAssets => Entries.Count != 0 && Entries.First().Value > 0;
    public virtual void Add(T entry, bool recalculateValues = true)
    {
        var alreadyExistingEntry = Entries.FirstOrDefault(x => x.EntryId == entry.EntryId);
        if (alreadyExistingEntry is not null)
        {
            Console.WriteLine($"WARNING - Entry already exist, can not be added: Id:{alreadyExistingEntry.EntryId}, Posting date {alreadyExistingEntry.PostingDate}, Value change {alreadyExistingEntry.ValueChange}");
            return;
        }
        var previousEntry = Entries.GetNextYounger(entry.PostingDate).FirstOrDefault();

        var index = -1;
        if (previousEntry is not null)
            index = Entries.IndexOf(previousEntry);

        if (index < 0)
        {
            Entries.Add(entry);
            index = Entries.Count - 1;
        }
        else
        {
            Entries.Insert(index, entry);
        }

        if (recalculateValues)
            RecalculateEntryValues(index);
    }
    public virtual void Add(IEnumerable<T> entries, bool recalculateValues = true)
    {
        foreach (var entry in entries)
            Add(entry, recalculateValues);
    }

    public virtual void UpdateEntry(T entry, bool recalculateValues = true)
    {
        var entryToUpdate = Entries.FirstOrDefault(x => x.EntryId == entry.EntryId);
        if (entryToUpdate is null) return;

        entryToUpdate.Update(entry);
        Entries.Remove(entryToUpdate);
        var previousEntry = Entries.GetNextYounger(entryToUpdate.PostingDate).FirstOrDefault();

        if (previousEntry is null)
        {
            Entries.Add(entryToUpdate);
        }
        else
        {
            var newIndex = Entries.IndexOf(previousEntry);
            Entries.Insert(newIndex, entryToUpdate);
        }

        if (recalculateValues)
            RecalculateEntryValues(Entries.IndexOf(entryToUpdate));
    }

    public virtual void Remove(int id)
    {
        var entry = Entries.FirstOrDefault(x => x.EntryId == id);
        if (entry is null) return;

        var indexToRemove = Entries.IndexOf(entry);
        Entries.RemoveAt(indexToRemove);

        if (indexToRemove > 0)
            Entries[indexToRemove - 1].Value -= entry.ValueChange;

        RecalculateEntryValues(indexToRemove - 1);
    }
    public int? GetMaxId() => Entries.Count == 0 ? null : Entries.Max(x => x.EntryId);
    public IEnumerable<T> GetDaily() => throw new NotImplementedException();
    public IEnumerable<T> GetMonthly() => throw new NotImplementedException();
    public IEnumerable<T> GetYearly() => throw new NotImplementedException();
    public IEnumerable<T> GetExpenses() => throw new NotImplementedException();
    public IEnumerable<T> GetEarnings() => throw new NotImplementedException();
    internal void RecalculateEntryValues(int? startingIndex)
    {
        if (Entries.Count == 0) return;

        int startIndex = startingIndex ?? Entries.Count - 1;
        for (int i = startIndex; i >= 0; i--)
        {
            if (Entries.Count - 1 <= i)
            {
                Entries[i].Value = Entries[i].ValueChange;
                continue;
            }

            var newValue = Entries[i + 1].Value + Entries[i].ValueChange;
            Entries[i].Value = newValue;
        }
    }
    private DateTime? GetStartDate()
    {
        if (Entries.Count == 0) return null;

        var minDate = Entries.Min(x => x.PostingDate);
        return Entries.First(x => x.PostingDate == minDate).PostingDate;
    }
    private DateTime? GetEndDate()
    {
        if (Entries.Count == 0) return null;

        var maxDate = Entries.Max(x => x.PostingDate);
        return Entries.First(x => x.PostingDate == maxDate).PostingDate;
    }
}
