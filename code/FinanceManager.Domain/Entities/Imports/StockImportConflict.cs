using FinanceManager.Domain.Entities.Stocks;

namespace FinanceManager.Domain.Entities.Imports;

public record StockImportConflict(int AccountId, StockEntryImport? ImportEntry, StockAccountEntry? ExistingEntry, string Reason)
{
    public bool IsExactMatch =>
        ImportEntry is not null && ExistingEntry is not null &&
        ImportEntry.PostingDate == ExistingEntry.PostingDate &&
        ImportEntry.ValueChange == ExistingEntry.ValueChange &&
        string.Equals(ImportEntry.Ticker, ExistingEntry.Ticker, StringComparison.OrdinalIgnoreCase);

    public DateTime DateTime => ImportEntry?.PostingDate ?? ExistingEntry!.PostingDate;
}