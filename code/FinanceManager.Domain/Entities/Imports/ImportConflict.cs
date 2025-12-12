using FinanceManager.Domain.Entities.Cash;

namespace FinanceManager.Domain.Entities.Imports;

public record ImportConflict(int AccountId, BankEntryImport? ImportEntry, BankAccountEntry? ExistingEntry, string Reason)
{
    public bool IsExactMatch =>
        ImportEntry is not null && ExistingEntry is not null &&
        ImportEntry.PostingDate == ExistingEntry.PostingDate &&
        ImportEntry.ValueChange == ExistingEntry.ValueChange;

    public DateTime DateTime => ImportEntry?.PostingDate ?? ExistingEntry!.PostingDate;
};