using System.ComponentModel.DataAnnotations;

namespace FinanceManager.Domain.Commands.User;

public record ForgotPasswordRequest(
    [Required, EmailAddress, StringLength(256)] string Login);