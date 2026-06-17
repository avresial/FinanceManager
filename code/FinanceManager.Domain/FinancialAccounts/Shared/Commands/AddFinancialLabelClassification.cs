using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using System.ComponentModel.DataAnnotations;

namespace FinanceManager.Domain.FinancialAccounts.Shared.Commands;

public record AddFinancialLabelClassification(
    [Range(1, int.MaxValue)] int LabelId,
    [Required, StringLength(64)] string Kind,
    [Required, StringLength(256)] string Value);