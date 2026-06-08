using System.ComponentModel.DataAnnotations;

namespace FinanceManager.Application.Commands.User;

public record ForgotPasswordRequest(
    [Required, EmailAddress, StringLength(256)] string Login);