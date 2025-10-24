namespace FinanceManager.Domain.Entities.Imports;

public record ImportResult(int AccountId, int Imported, int Failed, IReadOnlyList<string> Errors, IReadOnlyList<ImportConflict> Conflicts);
