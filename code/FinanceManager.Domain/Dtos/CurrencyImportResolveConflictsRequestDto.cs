namespace FinanceManager.Domain.Dtos;

public class CurrencyImportResolveConflictsRequestDto
{
    public Guid JobId { get; set; }
    public IReadOnlyList<CurrencyImportConflictResolutionDecisionDto> Decisions { get; set; } = [];
}