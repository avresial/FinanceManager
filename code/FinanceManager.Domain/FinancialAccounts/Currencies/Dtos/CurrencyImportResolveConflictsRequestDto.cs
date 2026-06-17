using System.ComponentModel.DataAnnotations;

namespace FinanceManager.Domain.FinancialAccounts.Currencies.Dtos;

public class CurrencyImportResolveConflictsRequestDto
{
    public Guid JobId { get; set; }

    [Required]
    public IReadOnlyList<CurrencyImportConflictResolutionDecisionDto> Decisions { get; set; } = [];
}