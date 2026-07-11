using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using System.ComponentModel.DataAnnotations;

namespace FinanceManager.Domain.FinancialAccounts.Shared.Commands;

public record AddAccount(
    [Required, StringLength(256)] string AccountName);