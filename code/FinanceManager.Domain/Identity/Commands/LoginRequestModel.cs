using FinanceManager.Domain.Identity.Validation;
using System.ComponentModel.DataAnnotations;

namespace FinanceManager.Application.Commands.Login;

public record LoginRequestModel(
    [Required, LoginIdentifier, StringLength(256)] string UserName,
    [Required, StringLength(128, MinimumLength = 8)] string Password);