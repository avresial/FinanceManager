using System.ComponentModel.DataAnnotations;

namespace FinanceManager.Domain.Commands.Account;

public record AddFinancialLabel(
    [Required, StringLength(256)] string Name);