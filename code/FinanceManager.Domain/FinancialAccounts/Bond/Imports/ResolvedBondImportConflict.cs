using FinanceManager.Domain.FinancialAccounts.Bond.Entities;

namespace FinanceManager.Domain.FinancialAccounts.Bond.Imports;

public class ResolvedBondImportConflict
{
    // Parameterless ctor required for model binding / deserialization
    public ResolvedBondImportConflict() { }

    public ResolvedBondImportConflict(int accountId, bool importIsPicked, BondEntryImport? importData, bool existingIsPicked, int? existingId)
    {
        if (importData is not null && existingId is not null && importIsPicked == existingIsPicked)
            throw new Exception("Cannot pick both import entry and existing entry or neither.");

        AccountId = accountId;
        AddImported = importIsPicked;
        ImportData = importData;
        LeaveExisting = existingIsPicked;
        ExistingId = existingId;
    }

    public int AccountId { get; set; }

    // Import side
    public bool AddImported { get; set; }
    public BondEntryImport? ImportData { get; set; }

    // Existing entry side
    public bool LeaveExisting { get; set; }
    public int? ExistingId { get; set; }

    public BondAccountEntry ToEntry()
    {
        return ImportData is null
            ? throw new ArgumentNullException($"{nameof(ImportData)} is null")
            : new BondAccountEntry(AccountId, 0, ImportData.PostingDate, ImportData.ValueChange, ImportData.ValueChange, ImportData.BondDetailsId);
    }
}