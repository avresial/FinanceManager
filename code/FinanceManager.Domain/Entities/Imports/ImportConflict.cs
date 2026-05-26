using FinanceManager.Domain.Entities.FinancialAccounts.Currencies;

namespace FinanceManager.Domain.Entities.Imports;

public record ImportConflict(int AccountId, CurrencyEntryImport? ImportEntry, CurrencyAccountEntry? ExistingEntry, string Reason, string? ConflictId = null)
{
    // Posting dates are stored/compared at second precision across the app. Truncating
    // both sides keeps legacy fractional-second DB entries comparable with second-precision
    // CSV imports.
    public bool IsExactMatch =>
        ImportEntry is not null && ExistingEntry is not null &&
        TruncateToSecond(ImportEntry.PostingDate) == TruncateToSecond(ExistingEntry.PostingDate) &&
        ImportEntry.ValueChange == ExistingEntry.ValueChange;

    public DateTime DateTime => ImportEntry?.PostingDate ?? ExistingEntry!.PostingDate;

    private static DateTime TruncateToSecond(DateTime d) =>
        new(d.Year, d.Month, d.Day, d.Hour, d.Minute, d.Second, d.Kind);
};