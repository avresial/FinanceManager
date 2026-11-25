using System.ComponentModel.DataAnnotations;

namespace FinanceManager.Domain.Transactions.Cash.Dtos;

public class CurrencyImportResolveConflictsRequestDto
{
    public Guid JobId { get; set; }

    [Required]
    public IReadOnlyList<CurrencyImportConflictResolutionDecisionDto> Decisions { get; set; } = [];
}