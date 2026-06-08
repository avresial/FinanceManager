using FinanceManager.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace FinanceManager.Domain.Commands.User;

public record UpdateUserRole(
    [Range(1, int.MaxValue)] int UserId,
    UserRole UserRole);