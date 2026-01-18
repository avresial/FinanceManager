using FinanceManager.Domain.Entities.FinancialAccounts.Currencies;

namespace FinanceManager.Domain.Entities.Imports;

public record ImportConflict(int AccountId, CurrencyEntryImport? ImportEntry, CurrencyAccountEntry? ExistingEntry, string Reason)
{
    public bool IsExactMatch =>
        ImportEntry is not null && ExistingEntry is not null &&
        ImportEntry.PostingDate == ExistingEntry.PostingDate &&
        ImportEntry.ValueChange == ExistingEntry.ValueChange;

    public DateTime DateTime => ImportEntry?.PostingDate ?? ExistingEntry!.PostingDate;
};