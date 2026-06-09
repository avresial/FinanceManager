using FinanceManager.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace FinanceManager.Domain.Commands.Account;

public record UpdateAccount(
    [Range(1, int.MaxValue)] int AccountId,
    [Required, StringLength(256)] string AccountName,
    AccountLabel AccountType);