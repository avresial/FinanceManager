using FinanceManager.Domain.Identity.Entities;
using System.ComponentModel.DataAnnotations;

namespace FinanceManager.Domain.Identity.Commands;

public record UpdateUserRole(
    [Range(1, int.MaxValue)] int UserId,
    UserRole UserRole);