using System.ComponentModel.DataAnnotations;

namespace FinanceManager.Application.Commands.Account;

public record AddFinancialLabelClassification(
    [Range(1, int.MaxValue)] int LabelId,
    [Required, StringLength(64)] string Kind,
    [Required, StringLength(256)] string Value);
