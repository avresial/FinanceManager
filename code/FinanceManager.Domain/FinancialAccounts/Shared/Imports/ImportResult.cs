namespace FinanceManager.Domain.FinancialAccounts.Shared.Imports;

public record ImportResult(int Imported, int Failed, IReadOnlyList<string> Errors, IReadOnlyList<ImportConflict> Conflicts);