using System.ComponentModel.DataAnnotations;

namespace FinanceManager.Domain.Dtos;

public record CurrencyImportConflictResolutionDecisionDto(
    [Required, StringLength(128)] string ConflictId,
    bool PickImported,
    bool PickExisting);
