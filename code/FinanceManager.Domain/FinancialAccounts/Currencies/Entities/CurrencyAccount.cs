using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Extensions;
using FinanceManager.Domain.Identity.Entities;
using System.Diagnostics;
using System.Text.Json.Serialization;

namespace FinanceManager.Domain.FinancialAccounts.Currencies.Entities;

public class CurrencyAccount : IAccount
{
    public readonly CurrencyAccountEntry? NextOlderEntry = null;
    public readonly CurrencyAccountEntry? NextYoungerEntry = null;
    public AccountLabel AccountType { get; set; }
    public int CurrencyId { get; set; }

    public DateTime? Start => LedgerEntries.GetStart(Entries);
    public DateTime? End => LedgerEntries.GetEnd(Entries);
    public List<CurrencyAccountEntry> Entries { get; protected set; } = [];
    public bool ContainsAssets => Entries.Count != 0 && Entries.First().Value > 0;

    public int AccountId { get; set; }
    public int UserId { get; set; }
    public string Name { get; set; } = string.Empty;

    // Multiple constructors preserve the existing account JSON constructor contract.
    [JsonConstructorAttribute]
    public CurrencyAccount(int userId, int accountId, string name, IEnumerable<CurrencyAccountEntry>? entries = null, AccountLabel accountType = AccountLabel.Other,
        CurrencyAccountEntry? nextOlderEntry = null, CurrencyAccountEntry? nextYoungerEntry = null, int currencyId = 0)
    {
        UserId = userId;
        AccountId = accountId;
        Name = name;
        Entries = entries is null ? ([]) : entries.ToList();
        AccountType = accountType;
        CurrencyId = currencyId;
        NextOlderEntry = nextOlderEntry;
        NextYoungerEntry = nextYoungerEntry;
    }
    public CurrencyAccount(int userId, int id, string name, AccountLabel accountType, int currencyId = 0)
    {
        UserId = userId;
        AccountId = id;
        Name = name;
        AccountType = accountType;
        CurrencyId = currencyId;
        Entries = [];
    }

    public CurrencyAccountEntry? GetThisOrNextOlder(DateTime date)
    {
        if (Entries is null) return default;
        var result = Entries.GetThisOrNextOlder(date);

        if (result is not null) return result;

        return NextOlderEntry;
    }
    public void AddEntry(AddCurrencyEntryDto entry, bool recalculateValues = true)
    {
        var alreadyExistingEntry = Entries.FirstOrDefault(x => x.PostingDate == entry.PostingDate && x.ValueChange == entry.ValueChange);
        if (alreadyExistingEntry is not null)
        {
            Debug.WriteLine($"WARNING - Entry already exist, can not be added: Id:{alreadyExistingEntry.EntryId}, Posting date {alreadyExistingEntry.PostingDate}, Value change {alreadyExistingEntry.ValueChange}");
            //throw new Exception($"Entry already exist, can not be added - Posting date: {alreadyExistingEntry.PostingDate}, " +
            //    $"Value change: {alreadyExistingEntry.ValueChange}");
        }

        var newEntry = new CurrencyAccountEntry(AccountId, GetNextFreeId(), entry.PostingDate, entry.ValueChange, entry.ValueChange)
        {
            Description = entry.Description,
            ContractorDetails = entry.ContractorDetails,
            Labels = entry.Labels
        };

        var index = LedgerEntries.InsertByPostingDate(Entries, newEntry);
        // AddEntry historically starts one position earlier when appending an oldest entry.
        if (index == Entries.Count - 1)
            index--;

        if (recalculateValues)
            RecalculateEntryValues(index);
    }

    public IEnumerable<CurrencyAccountEntry> Get() => Entries;
    public IEnumerable<CurrencyAccountEntry> Get(DateTime date) => Entries.Get(date);
    public IEnumerable<CurrencyAccountEntry> Get(DateTime start, DateTime end) =>
        Entries.Where(entry => entry.PostingDate >= start && entry.PostingDate <= end);

    public void Add(CurrencyAccountEntry entry, bool recalculateValues = true)
    {
        if (LedgerEntries.ContainsEntryId(Entries, entry.EntryId))
        {
            var existing = Entries.First(e => e.EntryId == entry.EntryId);
            Console.WriteLine($"WARNING - Entry already exist, can not be added: Id:{existing.EntryId}, Posting date {existing.PostingDate}, Value change {existing.ValueChange}");
            return;
        }

        var index = LedgerEntries.InsertByPostingDate(Entries, entry);
        if (recalculateValues)
            RecalculateEntryValues(index);
    }

    public void Add(IEnumerable<CurrencyAccountEntry> entries, bool recalculateValues = true)
    {
        foreach (var entry in entries)
            Add(entry, recalculateValues);
    }

    public void Remove(long id)
    {
        var entry = Entries.FirstOrDefault(e => e.EntryId == id);
        if (entry is null) return;

        var index = Entries.IndexOf(entry);
        Entries.RemoveAt(index);
        if (index > 0)
            Entries[index - 1].Value -= entry.ValueChange;

        RecalculateEntryValues(index - 1);
    }

    public long? GetMaxId() => LedgerEntries.GetMaxId(Entries);
    public void RecalculateEntryValues(int? startingIndex) => LedgerEntries.RecalculateRunningBalance(Entries, startingIndex);

    public long GetNextFreeId()
    {
        var currentMaxId = GetMaxId();
        if (currentMaxId is not null)
            return currentMaxId.Value + 1;
        return 0;
    }

}