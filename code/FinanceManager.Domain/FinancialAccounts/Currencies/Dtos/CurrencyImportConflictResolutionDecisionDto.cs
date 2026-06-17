using System.ComponentModel.DataAnnotations;

namespace FinanceManager.Domain.FinancialAccounts.Currencies.Dtos;

public record CurrencyImportConflictResolutionDecisionDto(
    [Required, StringLength(128)] string ConflictId,
    bool PickImported,
    bool PickExisting);