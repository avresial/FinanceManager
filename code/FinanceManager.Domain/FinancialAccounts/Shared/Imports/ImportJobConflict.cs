namespace FinanceManager.Domain.FinancialAccounts.Shared.Imports;

public record ImportJobConflict(string ConflictId, ImportConflict Conflict, bool IsResolved = false);