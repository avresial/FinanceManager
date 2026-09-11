using FinanceManager.Domain.FinancialAccounts.Bond.Extensions;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Extensions;
using FinanceManager.Domain.Identity.Entities;

namespace FinanceManager.Domain.FinancialAccounts.Bond.Entities;

public class BondAccount : FinancialAccountBase<BondAccountEntry>
{
    public readonly Dictionary<int, BondAccountEntry> NextOlderEntries = [];
    public readonly Dictionary<int, BondAccountEntry> NextYoungerEntries = [];

    public AccountLabel AccountType { get; set; }

    public BondAccount(int userId, int accountId, string name, IEnumerable<BondAccountEntry>? entries = null, AccountLabel accountType = AccountLabel.Other,
        Dictionary<int, BondAccountEntry>? nextOlderEntries = null, Dictionary<int, BondAccountEntry>? nextYoungerEntries = null) : base(userId, accountId, name)
    {
        UserId = userId;
        Entries = entries is null ? [] : [.. entries];
        AccountType = accountType;
        NextOlderEntries = nextOlderEntries ?? [];
        NextYoungerEntries = nextYoungerEntries ?? [];
    }

    public BondAccount(int userId, int id, string name, AccountLabel accountType) : base(userId, id, name)
    {
        AccountType = accountType;
        Entries = [];
    }

    public override void Add(BondAccountEntry entry, bool recalculateValues = true)
    {
        var alreadyExistingEntry = Entries.FirstOrDefault(x => x.EntryId == entry.EntryId);
        if (alreadyExistingEntry is not null)
        {
            Console.WriteLine($"WARNING - Entry already exist, can not be added: Id:{alreadyExistingEntry.EntryId}, Posting date {alreadyExistingEntry.PostingDate}, Value change {alreadyExistingEntry.ValueChange}");
            return;
        }

        // Find the correct position in the list (youngest entry older than the new one)
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
        {
            // Recalculate only entries with the same BondDetailsId
            RecalculateEntriesForBond(entry.BondDetailsId, index);
        }
    }

    private void RecalculateEntriesForBond(int bondDetailsId, int startingIndex)
    {
        // Recalculate values for all entries with the same BondDetailsId starting from startingIndex going backwards (to younger entries)
        for (int i = startingIndex; i >= 0; i--)
        {
            if (Entries[i].BondDetailsId != bondDetailsId)
                continue;

            // Find the next older entry with the same BondDetailsId
            var nextOlderEntry = Entries
                .Skip(i + 1)
                .FirstOrDefault(e => e.BondDetailsId == bondDetailsId);

            if (nextOlderEntry != null)
            {
                Entries[i].Value = nextOlderEntry.Value + Entries[i].ValueChange;
            }
            else
            {
                Entries[i].Value = Entries[i].ValueChange;
            }
        }
    }

    public override void UpdateEntry(BondAccountEntry entry, bool recalculateValues = true)
    {
        var entryToUpdate = Entries.FirstOrDefault(x => x.EntryId == entry.EntryId);
        if (entryToUpdate is null) return;

        var oldBondDetailsId = entryToUpdate.BondDetailsId;
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
        {
            var index = Entries.IndexOf(entryToUpdate);
            RecalculateEntriesForBond(entryToUpdate.BondDetailsId, index);

            // If BondDetailsId changed, also recalculate the old bond's entries
            if (oldBondDetailsId != entryToUpdate.BondDetailsId)
            {
                var oldBondEntries = Entries.Where(e => e.BondDetailsId == oldBondDetailsId).ToList();
                if (oldBondEntries.Any())
                {
                    var oldBondStartIndex = Entries.IndexOf(oldBondEntries.First());
                    RecalculateEntriesForBond(oldBondDetailsId, oldBondStartIndex);
                }
            }
        }
    }

    public override void Remove(int id)
    {
        var entry = Entries.FirstOrDefault(x => x.EntryId == id);
        if (entry is null) return;

        var bondDetailsId = entry.BondDetailsId;
        var indexToRemove = Entries.IndexOf(entry);
        Entries.RemoveAt(indexToRemove);

        // Find the next younger entry with the same BondDetailsId and adjust its value
        var nextYoungerWithSameBond = Entries
            .Take(indexToRemove)
            .Where(e => e.BondDetailsId == bondDetailsId)
            .OrderBy(e => e.PostingDate)
            .FirstOrDefault();

        if (nextYoungerWithSameBond != null)
        {
            var youngerIndex = Entries.IndexOf(nextYoungerWithSameBond);
            RecalculateEntriesForBond(bondDetailsId, youngerIndex);
        }
    }

    public Dictionary<DateOnly, decimal> GetDailyPrice(DateOnly start, DateOnly end, List<BondDetails> bondDetails)
    {
        var result = new Dictionary<DateOnly, decimal>();
        if (Entries is null || start > end) return result;

        var detailsById = bondDetails.ToDictionary(x => x.Id);

        var detailsIds = Entries.Select(e => e.BondDetailsId)
            .Concat(NextOlderEntries.Keys)
            .Distinct()
            .ToList();
        var missingDetailIds = detailsIds.Where(id => !detailsById.ContainsKey(id)).ToList();
        if (missingDetailIds.Count != 0)
            throw new InvalidOperationException($"Bond valuation requires details for bond ids: {string.Join(", ", missingDetailIds)}.");

        var entriesByBond = new Dictionary<int, List<(BondAccountEntry Entry, int SourceIndex)>>();
        for (var i = 0; i < Entries.Count; i++)
        {
            var entry = Entries[i];
            if (entriesByBond.TryGetValue(entry.BondDetailsId, out var bondEntries))
                bondEntries.Add((entry, i));
            else
                entriesByBond[entry.BondDetailsId] = [(entry, i)];
        }

        // Sort so that for equal posting dates the entry lowest in the Entries list comes last,
        // matching the stable OrderByDescending selection in BondEntryExtension.GetThisOrNextOlder.
        foreach (var bondEntries in entriesByBond.Values)
            bondEntries.Sort((a, b) => a.Entry.PostingDate != b.Entry.PostingDate
                ? a.Entry.PostingDate.CompareTo(b.Entry.PostingDate)
                : b.SourceIndex.CompareTo(a.SourceIndex));

        var totals = new Dictionary<DateOnly, decimal>();
        foreach (var detailId in detailsIds)
        {
            var details = detailsById[detailId];
            var bondEntries = entriesByBond.GetValueOrDefault(detailId);

            AddBondEntryValues(totals, start, end, bondEntries, details);

            NextOlderEntries.TryGetValue(detailId, out var carriedEntry);
            AddCarriedEntryValues(totals, start, end, bondEntries, details, carriedEntry);
        }

        for (var date = start; date <= end; date = date.AddDays(1))
        {
            if (totals.TryGetValue(date, out var total) && total != 0)
                result[date] = total;
        }

        return result;
    }

    private static void AddBondEntryValues(
        Dictionary<DateOnly, decimal> totals, DateOnly start, DateOnly end,
        List<(BondAccountEntry Entry, int SourceIndex)>? bondEntries, BondDetails details)
    {
        if (bondEntries is null || bondEntries.Count == 0) return;

        for (var i = 0; i < bondEntries.Count; i++)
        {
            var entry = bondEntries[i].Entry;
            var entryDay = DateOnly.FromDateTime(entry.PostingDate);
            var intervalEnd = i + 1 < bondEntries.Count
                ? DateOnly.FromDateTime(bondEntries[i + 1].Entry.PostingDate).AddDays(-1)
                : end;

            var windowStart = entryDay > start ? entryDay : start;
            var windowEnd = intervalEnd < end ? intervalEnd : end;
            if (windowStart > windowEnd) continue;

            var series = entry.GetPrice(windowEnd, details);
            for (var date = windowStart; date <= windowEnd; date = date.AddDays(1))
            {
                if (series.TryGetValue(date, out var value))
                    totals[date] = totals.GetValueOrDefault(date) + value;
            }
        }
    }

    private static void AddCarriedEntryValues(
        Dictionary<DateOnly, decimal> totals, DateOnly start, DateOnly end,
        List<(BondAccountEntry Entry, int SourceIndex)>? bondEntries, BondDetails details, BondAccountEntry? carriedEntry)
    {
        if (carriedEntry is null) return;

        var intervalEnd = bondEntries is { Count: > 0 }
            ? DateOnly.FromDateTime(bondEntries[0].Entry.PostingDate).AddDays(-1)
            : end;
        if (intervalEnd > end) intervalEnd = end;

        var carriedDay = DateOnly.FromDateTime(carriedEntry.PostingDate);
        var windowStart = carriedDay > start ? carriedDay : start;
        if (windowStart > intervalEnd) return;

        var series = carriedEntry.GetPrice(intervalEnd, details);
        for (var date = windowStart; date <= intervalEnd; date = date.AddDays(1))
        {
            if (series.TryGetValue(date, out var value))
                totals[date] = totals.GetValueOrDefault(date) + value;
        }
    }

    public BondAccountEntry? GetThisOrNextOlder(DateTime date, int bondDetailsId)
    {
        if (Entries is null) return default;
        var result = Entries.GetThisOrNextOlder(date, bondDetailsId);

        if (result is not null) return result;
        if (!NextOlderEntries.ContainsKey(bondDetailsId)) return default;

        return NextOlderEntries[bondDetailsId];
    }

    public List<int> GetStoredBondsIds()
    {
        return Entries.Select(x => x.BondDetailsId)
            .Concat(NextOlderEntries.Keys)
            .Distinct()
            .ToList();
    }

}