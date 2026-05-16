namespace FinanceManager.Domain.Dtos;

public record CurrencyImportConflictResolutionDecisionDto(string ConflictId, bool PickImported, bool PickExisting);
