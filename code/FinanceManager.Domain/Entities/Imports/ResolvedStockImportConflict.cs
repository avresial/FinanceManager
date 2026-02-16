using FinanceManager.Domain.Entities.Stocks;
using FinanceManager.Domain.Enums;

namespace FinanceManager.Domain.Entities.Imports;

public class ResolvedStockImportConflict
{
    // Parameterless ctor required for model binding / deserialization
    public ResolvedStockImportConflict() { }

    public ResolvedStockImportConflict(int accountId, bool importIsPicked, StockEntryImport? importData, bool existingIsPicked, int? existingId)
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
    public StockEntryImport? ImportData { get; set; }

    // Existing entry side
    public bool LeaveExisting { get; set; }
    public int? ExistingId { get; set; }

    public StockAccountEntry ToEntry()
    {
        return ImportData is null
            ? throw new ArgumentNullException($"{nameof(ImportData)} is null")
            : new StockAccountEntry(AccountId, 0, ImportData.PostingDate, ImportData.ValueChange, ImportData.ValueChange, ImportData.Ticker, InvestmentType.Stock);
    }
}