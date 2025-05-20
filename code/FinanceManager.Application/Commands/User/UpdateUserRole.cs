using FinanceManager.Domain.Enums;

namespace FinanceManager.Application.Commands.User;

public record UpdateUserRole(int UserId, UserRole UserRole);
