namespace FinanceManager.Domain.Entities.Imports;

public class ResolvedImportConflict
{
    public int AccountId { get; }
    public (bool IsPicked, BankEntryImport? Data) ImportEntry { get; }
    public (bool IsPicked, int? Data) ExistingEntry { get; }

    public ResolvedImportConflict(int accountId, (bool IsPicked, BankEntryImport? Data) importEntry, (bool IsPicked, int? Data) existingEntry)
    {
        if (importEntry.Data is not null && existingEntry.Data is not null && importEntry.IsPicked == existingEntry.IsPicked)
            throw new Exception("Cannot pick both import entry and existing entry or neither.");

        this.AccountId = accountId;
        this.ImportEntry = importEntry;
        this.ExistingEntry = existingEntry;
    }
}