using System.ComponentModel.DataAnnotations;

namespace FinanceManager.Application.Commands.Account;

public record DeleteAccount(
    [Range(1, int.MaxValue)] int AccountId);
