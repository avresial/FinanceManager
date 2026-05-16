namespace FinanceManager.Domain.Entities.Imports;

public record BondImportResult(int AccountId, int Imported, int Failed, IReadOnlyList<string> Errors, IReadOnlyList<BondImportConflict> Conflicts);
