using System.ComponentModel.DataAnnotations;

namespace FinanceManager.Application.Commands.Account;

public record UpdateFiancialLabel(
    [Range(1, int.MaxValue)] int Id,
    [Required, StringLength(256)] string Name);
