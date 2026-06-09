using System.ComponentModel.DataAnnotations;

namespace FinanceManager.Domain.Commands.Account;

public record DeleteAccount(
    [Range(1, int.MaxValue)] int AccountId);