using System.ComponentModel.DataAnnotations;

namespace FinanceManager.Application.Commands.User;

public record GetUser(
    [Required, StringLength(256)] string UserName,
    [Required, StringLength(128, MinimumLength = 8)] string Password);
