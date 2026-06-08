using FinanceManager.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace FinanceManager.Domain.Commands.User;

public record AddUser(
    [Required, EmailAddress, StringLength(256)] string UserName,
    [Required, StringLength(128, MinimumLength = 8)] string Password,
    PricingLevel PricingLevel,
    [StringLength(256)] string? FirstName = null,
    [StringLength(256)] string? LastName = null);