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
        ImportIsPicked = importIsPicked;
        ImportData = importData;
        ExistingIsPicked = existingIsPicked;
        ExistingId = existingId;
    }

    public int AccountId { get; set; }

    // Import side
    public bool ImportIsPicked { get; set; }
    public BankEntryImport? ImportData { get; set; }

    // Existing entry side
    public bool ExistingIsPicked { get; set; }
    public int? ExistingId { get; set; }
}