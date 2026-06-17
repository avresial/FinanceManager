using FinanceManager.Domain.FinancialAccounts.Stock.Entities;

namespace FinanceManager.Domain.FinancialAccounts.Stock.Imports;

public record StockImportConflict(int AccountId, StockEntryImport? ImportEntry, StockAccountEntry? ExistingEntry, string Reason)
{
    // Posting dates are stored/compared at second precision across the app. Truncating
    // both sides keeps legacy fractional-second DB entries comparable with second-precision
    // CSV imports.
    public bool IsExactMatch =>
        ImportEntry is not null && ExistingEntry is not null &&
        TruncateToSecond(ImportEntry.PostingDate) == TruncateToSecond(ExistingEntry.PostingDate) &&
        ImportEntry.ValueChange == ExistingEntry.ValueChange &&
        string.Equals(ImportEntry.Isin, ExistingEntry.Isin, StringComparison.OrdinalIgnoreCase);

    public DateTime DateTime => ImportEntry?.PostingDate ?? ExistingEntry!.PostingDate;

    private static DateTime TruncateToSecond(DateTime d) =>
        new(d.Year, d.Month, d.Day, d.Hour, d.Minute, d.Second, d.Kind);
}