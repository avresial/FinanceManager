using FinanceManager.Domain.Entities.Bonds;

namespace FinanceManager.Domain.Entities.Imports;

public record BondImportConflict(int AccountId, BondEntryImport? ImportEntry, BondAccountEntry? ExistingEntry, string Reason)
{
    public bool IsExactMatch =>
        ImportEntry is not null && ExistingEntry is not null &&
        ImportEntry.PostingDate == ExistingEntry.PostingDate &&
        ImportEntry.ValueChange == ExistingEntry.ValueChange &&
        ImportEntry.BondDetailsId == ExistingEntry.BondDetailsId;

    public DateTime DateTime => ImportEntry?.PostingDate ?? ExistingEntry!.PostingDate;
}
