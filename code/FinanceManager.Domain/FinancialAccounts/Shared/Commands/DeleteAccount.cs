using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using System.ComponentModel.DataAnnotations;

namespace FinanceManager.Domain.FinancialAccounts.Shared.Commands;

public record DeleteAccount(
    [Range(1, int.MaxValue)] int AccountId);