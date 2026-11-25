namespace FinanceManager.Domain.Transactions.Import;

public record ImportJobConflict(string ConflictId, ImportConflict Conflict, bool IsResolved = false);