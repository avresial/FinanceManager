using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using System.ComponentModel.DataAnnotations;

namespace FinanceManager.Domain.FinancialAccounts.Shared.Commands;

public record UpdateFinancialLabel(
    [Range(1, int.MaxValue)] int Id,
    [Required, StringLength(256)] string Name);