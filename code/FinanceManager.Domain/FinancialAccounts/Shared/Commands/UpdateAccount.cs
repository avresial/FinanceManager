using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.Identity.Entities;
using System.ComponentModel.DataAnnotations;

namespace FinanceManager.Domain.FinancialAccounts.Shared.Commands;

public record UpdateAccount(
    [Range(1, int.MaxValue)] int AccountId,
    [Required, StringLength(256)] string AccountName,
    AccountLabel AccountType);