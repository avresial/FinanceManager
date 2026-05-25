using FinanceManager.Domain.Entities.Shared.Accounts;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Extensions;
using System.Diagnostics;
using System.Text.Json.Serialization;

namespace FinanceManager.Domain.Entities.FinancialAccounts.Currencies;

public class CurrencyAccount : FinancialAccountBase<CurrencyAccountEntry>
{
    public readonly CurrencyAccountEntry? NextOlderEntry = null;
    public readonly CurrencyAccountEntry? NextYoungerEntry = null;
    public AccountLabel AccountType { get; set; }

    [JsonConstructorAttribute]
    public CurrencyAccount(int userId, int accountId, string name, IEnumerable<CurrencyAccountEntry>? entries = null, AccountLabel accountType = AccountLabel.Other,
        CurrencyAccountEntry? nextOlderEntry = null, CurrencyAccountEntry? nextYoungerEntry = null) : base(userId, accountId, name)
    {
        this.UserId = userId;
        Entries = entries is null ? ([]) : entries.ToList();
        AccountType = accountType;
        NextOlderEntry = nextOlderEntry;
        NextYoungerEntry = nextYoungerEntry;
    }
    public CurrencyAccount(int userId, int id, string name, AccountLabel accountType) : base(userId, id, name)
    {
        AccountType = accountType;
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

        var previousEntry = Entries.GetNextYounger(entry.PostingDate).FirstOrDefault();
        var index = -1;

        if (previousEntry is not null)
            index = Entries.IndexOf(previousEntry);

        var newEntry = new CurrencyAccountEntry(AccountId, GetNextFreeId(), entry.PostingDate, entry.ValueChange, entry.ValueChange)
        {
            Description = entry.Description,
            ContractorDetails = entry.ContractorDetails,
            Labels = entry.Labels
        };


        if (index == -1)
        {
            index = Entries.Count;
            Entries.Add(newEntry);
            index -= 1;
        }
        else
        {
            Entries.Insert(index, newEntry);
        }

        if (recalculateValues)
            RecalculateEntryValues(index);
    }
    public override void UpdateEntry(CurrencyAccountEntry entry, bool recalculateValues = true)
    {
        Entries ??= [];

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

        var index = Entries.IndexOf(entryToUpdate);
        if (recalculateValues)
            RecalculateEntryValues(index);
    }

    public int GetNextFreeId()
    {
        var currentMaxId = GetMaxId();
        if (currentMaxId is not null)
            return currentMaxId.Value + 1;
        return 0;
    }

}