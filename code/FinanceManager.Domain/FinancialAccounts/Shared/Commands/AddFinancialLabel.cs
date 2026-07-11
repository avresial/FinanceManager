using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using System.ComponentModel.DataAnnotations;

namespace FinanceManager.Domain.FinancialAccounts.Shared.Commands;

public record AddFinancialLabel(
    [Required, StringLength(256)] string Name);