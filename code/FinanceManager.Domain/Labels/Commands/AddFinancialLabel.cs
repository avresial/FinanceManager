using System.ComponentModel.DataAnnotations;

namespace FinanceManager.Domain.Labels.Commands;

public record AddFinancialLabel(
    [Required, StringLength(256)] string Name);