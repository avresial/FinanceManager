using FinanceManager.Domain.Entities.Cash;
using FinanceManager.Domain.Entities.Shared.Accounts;

namespace FinanceManager.Domain.Entities.Imports;

public class ResolvedImportConflict
{
    // Parameterless ctor required for model binding / deserialization
    public ResolvedImportConflict() { }

    public ResolvedImportConflict(int accountId, bool importIsPicked, BankEntryImport? importData, bool existingIsPicked, int? existingId)
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
    public BankEntryImport? ImportData { get; set; }

    // Existing entry side
    public bool LeaveExisting { get; set; }
    public int? ExistingId { get; set; }

    public BankAccountEntry ToEntry(string description = "", ICollection<FinancialLabel>? labels = null)
    {
        return ImportData is null
            ? throw new ArgumentNullException($"{nameof(ImportData)} is null")
            : new(AccountId, 0, ImportData.PostingDate, ImportData.ValueChange, ImportData.ValueChange)
            {
                Description = description,
                Labels = labels ?? []
            };
    }
}