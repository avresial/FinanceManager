using System.ComponentModel.DataAnnotations;

namespace FinanceManager.Application.Commands.Account;

public record AddFinancialLabel(
    [Required, StringLength(256)] string Name);
