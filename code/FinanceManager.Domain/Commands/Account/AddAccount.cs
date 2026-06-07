using System.ComponentModel.DataAnnotations;

namespace FinanceManager.Domain.Commands.Account;

public record AddAccount(
    [Required, StringLength(256)] string AccountName);
