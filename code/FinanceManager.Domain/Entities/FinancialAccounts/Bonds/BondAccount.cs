using FinanceManager.Domain.Entities.Shared.Accounts;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Extensions;

namespace FinanceManager.Domain.Entities.Bonds;

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

        var detailsIds = Entries.Select(e => e.BondDetailsId).Distinct().ToList();
        if (!detailsIds.All(id => bondDetails.Any(bd => bd.Id == id)))
            throw new ArgumentException("Not all BondDetails are provided for the entries in this account.");

        List<Dictionary<DateOnly, decimal>> pricesPerDetail = [];
        foreach (var entry in Entries.Where(x => x.PostingDate <= end.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)))
            pricesPerDetail.Add(entry.GetPrice(end, bondDetails.Single(bd => bd.Id == entry.BondDetailsId)));

        foreach (var price in pricesPerDetail.SelectMany(dict => dict).OrderBy(x => x.Key))
        {
            if (result.ContainsKey(price.Key))
                result[price.Key] += price.Value;
            else
                result[price.Key] = price.Value;
        }

        return result;
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
        if (Entries is null) return [];

        return Entries.DistinctBy(x => x.BondDetailsId).Select(x => x.BondDetailsId).ToList();
    }

}