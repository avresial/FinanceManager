using FinanceManager.Domain.FinancialAccounts.Shared.Entities;

namespace FinanceManager.Domain.FinancialAccounts.Shared.Extensions;

/// <summary>Shared operations on newest-first ledger entries; accounts own the balance scope.</summary>
public static class LedgerEntries
{
    public static bool ContainsEntryId<T>(List<T> entries, long entryId) where T : class, ILedgerEntry =>
        entries.Any(entry => entry.EntryId == entryId);

    public static int InsertByPostingDate<T>(List<T> entries, T entry) where T : class, ILedgerEntry
    {
        var previousEntry = entries.GetNextYounger(entry.PostingDate).FirstOrDefault();
        var index = previousEntry is null ? -1 : entries.IndexOf(previousEntry);
        if (index < 0)
        {
            entries.Add(entry);
            return entries.Count - 1;
        }

        entries.Insert(index, entry);
        return index;
    }

    public static DateTime? GetStart<T>(List<T> entries) where T : class, ILedgerEntry =>
        entries.Count == 0 ? null : entries.Min(entry => entry.PostingDate);

    public static DateTime? GetEnd<T>(List<T> entries) where T : class, ILedgerEntry =>
        entries.Count == 0 ? null : entries.Max(entry => entry.PostingDate);

    public static long? GetMaxId<T>(List<T> entries) where T : class, ILedgerEntry =>
        entries.Count == 0 ? null : entries.Max(entry => entry.EntryId);

    /// <summary>Recalculates one balance stream, retaining the older entry's balance for partial updates.</summary>
    public static void RecalculateRunningBalance<T>(List<T> entries, int? startingIndex) where T : class, ILedgerEntry
    {
        if (entries.Count == 0) return;

        for (var i = startingIndex ?? entries.Count - 1; i >= 0; i--)
            entries[i].Value = i >= entries.Count - 1
                ? entries[i].ValueChange
                : entries[i + 1].Value + entries[i].ValueChange;
    }
}