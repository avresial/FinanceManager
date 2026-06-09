using System.ComponentModel.DataAnnotations;

namespace FinanceManager.Domain.Commands.User;

public record ResetPasswordRequest(
    [Required, StringLength(512)] string Token,
    [Required, StringLength(128, MinimumLength = 8)] string NewPassword);