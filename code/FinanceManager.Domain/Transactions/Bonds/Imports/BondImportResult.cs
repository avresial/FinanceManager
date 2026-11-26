namespace FinanceManager.Domain.Transactions.Bonds.Imports;

public record BondImportResult(int Imported, int Failed, IReadOnlyList<string> Errors, IReadOnlyList<BondImportConflict> Conflicts);