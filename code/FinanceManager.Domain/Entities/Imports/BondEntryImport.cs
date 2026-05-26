namespace FinanceManager.Domain.Entities.Imports;

public record BondEntryImport(DateTime PostingDate, decimal ValueChange, int BondDetailsId);