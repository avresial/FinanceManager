using System.ComponentModel.DataAnnotations;

namespace FinanceManager.Domain.Identity.Commands;

public record ForgotPasswordRequest(
    [Required, EmailAddress, StringLength(256)] string Login);