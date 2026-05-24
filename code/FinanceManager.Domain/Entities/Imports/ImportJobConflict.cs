namespace FinanceManager.Domain.Entities.Imports;

public record ImportJobConflict(string ConflictId, ImportConflict Conflict, bool IsResolved = false);