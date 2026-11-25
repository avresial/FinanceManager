using FinanceManager.Domain.Transactions.Bonds.Entities;

namespace FinanceManager.Domain.Transactions.Bonds.Imports;

public record BondImportConflict(BondEntryImport? ImportEntry, BondEntry? ExistingEntry, string Reason)
{
    // Posting dates are stored/compared at second precision across the app. Truncating
    // both sides keeps legacy fractional-second DB entries comparable with second-precision
    // CSV imports.
    public bool IsExactMatch =>
        ImportEntry is not null && ExistingEntry is not null &&
        TruncateToSecond(ImportEntry.PostingDate) == TruncateToSecond(ExistingEntry.PostingDate) &&
        ImportEntry.ValueChange == ExistingEntry.ValueChange &&
        ImportEntry.BondDetailsId == ExistingEntry.BondDetailsId;

    public DateTime DateTime => ImportEntry?.PostingDate ?? ExistingEntry!.PostingDate;

    private static DateTime TruncateToSecond(DateTime d) =>
        new(d.Year, d.Month, d.Day, d.Hour, d.Minute, d.Second, d.Kind);
}