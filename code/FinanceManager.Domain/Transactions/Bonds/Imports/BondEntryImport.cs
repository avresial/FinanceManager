namespace FinanceManager.Domain.Transactions.Bonds.Imports;

public record BondEntryImport(DateTime PostingDate, decimal ValueChange, int BondDetailsId, string Description = "");