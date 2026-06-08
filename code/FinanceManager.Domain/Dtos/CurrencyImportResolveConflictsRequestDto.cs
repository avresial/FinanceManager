using System.ComponentModel.DataAnnotations;

namespace FinanceManager.Domain.Dtos;

public class CurrencyImportResolveConflictsRequestDto
{
    public Guid JobId { get; set; }

    [Required]
    public IReadOnlyList<CurrencyImportConflictResolutionDecisionDto> Decisions { get; set; } = [];
}