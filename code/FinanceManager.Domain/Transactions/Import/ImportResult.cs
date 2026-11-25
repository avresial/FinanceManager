namespace FinanceManager.Domain.Transactions.Import;

public record ImportResult(int Imported, int Failed, IReadOnlyList<string> Errors, IReadOnlyList<ImportConflict> Conflicts);